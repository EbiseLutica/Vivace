using Microsoft.Data.Sqlite;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Serialization;

namespace Vivace.Storage.Sqlite;

/// <summary>
/// 状態の行を書く。前にコミットした状態と参照を比べ、変わった行だけを書く（イミュータブルなので参照で判る）。
/// 子の一覧（出来事、受信、未送信の操作）は、一覧の参照が変わったら所属の行を消して入れ直す。
/// </summary>
internal static class SqliteStateWriter
{
    /// <summary>ワールドの行の挿入と更新。作成の連番は world.created のイベントから引く。</summary>
    private const string WorldUpsertSql = """
        INSERT INTO worlds (id, def_id, seed, status, graph_version, created_seq)
        VALUES ($id, $def, $seed, $status, $graph,
                COALESCE((SELECT MIN(seq) FROM events WHERE world_id = $id AND event_type = 'world.created'), 0))
        ON CONFLICT(id) DO UPDATE SET def_id = excluded.def_id, seed = excluded.seed,
                                      status = excluded.status, graph_version = excluded.graph_version
        """;

    /// <summary>生えた場所の挿入と更新。</summary>
    private const string PlaceUpsertSql = """
        INSERT INTO world_places (world_id, place_id, name, tags, links, grp, status, created_seq, updated_seq)
        VALUES ($world, $place, $name, $tags, $links, $grp, $status, $created, $updated)
        ON CONFLICT(world_id, place_id) DO UPDATE SET name = excluded.name, tags = excluded.tags, links = excluded.links,
                                                      grp = excluded.grp, status = excluded.status, updated_seq = excluded.updated_seq
        """;

    /// <summary>変換の辞書の挿入と更新。</summary>
    private const string TermUpsertSql = """
        INSERT INTO world_terms (world_id, term, verdict, mapped, category, created_seq)
        VALUES ($world, $term, $verdict, $mapped, $category, $created)
        ON CONFLICT(world_id, term) DO UPDATE SET verdict = excluded.verdict, mapped = excluded.mapped, category = excluded.category
        """;

    /// <summary>ワールドの出来事の、所属の行の削除。</summary>
    private const string IncidentDeleteSql = "DELETE FROM incidents WHERE world_id = $world";

    /// <summary>出来事の挿入。</summary>
    private const string IncidentInsertSql = """
        INSERT INTO incidents (id, world_id, persona_id, def_id, effects, started_utc, ends_utc)
        VALUES ($id, $world, $persona, $def, $effects, $started, $ends)
        """;

    /// <summary>ペルソナの行の挿入と更新。作成の連番は persona.created のイベントから引く。</summary>
    private const string PersonaUpsertSql = """
        INSERT INTO personas (id, def_id, world_id, status, location, activity, overlay, activity_version, vitals, conditions,
                              plan, plan_version, usage, rng, posting, last_wake_utc, created_seq, updated_seq,
                              deciding, last_block_id, planned_for)
        VALUES ($id, $def, $world, $status, $location, $activity, $overlay, $activityVersion, $vitals, $conditions,
                $plan, $planVersion, $usage, $rng, $posting, $lastWake,
                COALESCE((SELECT MIN(seq) FROM events WHERE persona_id = $id AND event_type = 'persona.created'), $updated),
                $updated, $deciding, $lastBlock, $plannedFor)
        ON CONFLICT(id) DO UPDATE SET def_id = excluded.def_id, world_id = excluded.world_id, status = excluded.status,
                                      location = excluded.location, activity = excluded.activity, overlay = excluded.overlay,
                                      activity_version = excluded.activity_version, vitals = excluded.vitals, conditions = excluded.conditions,
                                      plan = excluded.plan, plan_version = excluded.plan_version, usage = excluded.usage, rng = excluded.rng,
                                      posting = excluded.posting, last_wake_utc = excluded.last_wake_utc, updated_seq = excluded.updated_seq,
                                      deciding = excluded.deciding, last_block_id = excluded.last_block_id, planned_for = excluded.planned_for
        """;

    /// <summary>受信の、所属の行の削除。</summary>
    private const string InboxDeleteSql = "DELETE FROM inbox WHERE persona_id = $persona";

    /// <summary>受信の挿入。未処理の受信として入れる（処理の結果は、後の段で書く）。</summary>
    private const string InboxInsertSql = """
        INSERT INTO inbox (id, persona_id, channel, kind, sender, audience, body, original_arrived_utc, status, handled_seq, outcome, item)
        VALUES ($id, $persona, $channel, $kind, $sender, $audience, $body, $arrived, 'unread', NULL, NULL, $item)
        """;

    /// <summary>未送信の操作の、所属の行の削除。</summary>
    private const string OutboundDeleteSql = "DELETE FROM outbound WHERE persona_id = $persona";

    /// <summary>未送信の操作の挿入。送信前の操作として入れる。</summary>
    private const string OutboundInsertSql = """
        INSERT INTO outbound (id, persona_id, channel, op_kind, is_speech, payload, status, external_id, result_seq, sent_utc, error)
        VALUES ($id, $persona, $channel, $kind, $speech, $payload, 'pending', NULL, NULL, NULL, NULL)
        """;

    /// <summary>コミットの後の状態を書く。前の状態（previous、なければ null）と比べて変わった行だけ書く。</summary>
    /// <param name="connection">接続。</param>
    /// <param name="transaction">トランザクション。</param>
    /// <param name="previous">前にコミットした状態。読んでいなければ null（すべての行を書く）。</param>
    /// <param name="state">コミットの後の状態。</param>
    /// <param name="updatedSeq">この状態を変えた最後の連番（ペルソナの更新の連番）。</param>
    /// <param name="ct">キャンセル。</param>
    public static async Task WriteAsync(SqliteConnection connection, SqliteTransaction transaction, SimState? previous, SimState state, long updatedSeq, CancellationToken ct)
    {
        foreach (var world in state.Worlds.Values.OrderBy(world => world.Id.Value, StringComparer.Ordinal))
        {
            WorldState? before = null;
            if (previous is not null && previous.Worlds.TryGetValue(world.Id, out var found))
            {
                before = found;
            }

            await WriteWorldAsync(connection, transaction, before, world, ct);
        }

        foreach (var persona in state.Personas.Values.OrderBy(persona => persona.Id.Value, StringComparer.Ordinal))
        {
            PersonaState? before = null;
            if (previous is not null && previous.Personas.TryGetValue(persona.Id, out var found))
            {
                before = found;
            }

            await WritePersonaAsync(connection, transaction, before, persona, updatedSeq, ct);
        }
    }

    /// <summary>ワールドの行、生えた場所、辞書、出来事を書く。変わった所だけ。</summary>
    private static async Task WriteWorldAsync(SqliteConnection connection, SqliteTransaction transaction, WorldState? before, WorldState world, CancellationToken ct)
    {
        if (!ReferenceEquals(before, world))
        {
            await SqliteValues.ExecuteAsync(connection, transaction, WorldUpsertSql, command =>
            {
                SqliteValues.Bind(command, "$id", world.Id.Value);
                SqliteValues.Bind(command, "$def", world.DefId.Value);
                SqliteValues.Bind(command, "$seed", unchecked((long)world.Seed));
                SqliteValues.Bind(command, "$status", world.Status.ToString());
                SqliteValues.Bind(command, "$graph", world.GraphVersion);
            }, ct);
        }

        foreach (var place in world.GeneratedPlaces.Values.OrderBy(place => place.Id.Value, StringComparer.Ordinal))
        {
            if (before is null || !before.GeneratedPlaces.TryGetValue(place.Id, out var old) || !ReferenceEquals(old, place))
            {
                await WritePlaceAsync(connection, transaction, world, place, ct);
            }
        }

        foreach (var term in world.Terms.Values.OrderBy(term => term.Term, StringComparer.Ordinal))
        {
            if (before is null || !before.Terms.TryGetValue(term.Term, out var old) || !ReferenceEquals(old, term))
            {
                await WriteTermAsync(connection, transaction, world, term, ct);
            }
        }

        if (before is null || !ReferenceEquals(before.Incidents, world.Incidents))
        {
            await SqliteValues.ExecuteAsync(connection, transaction, IncidentDeleteSql, command => SqliteValues.Bind(command, "$world", world.Id.Value), ct);
            foreach (var incident in world.Incidents.Values.OrderBy(incident => incident.Seq))
            {
                await WriteIncidentAsync(connection, transaction, world, incident, ct);
            }
        }
    }

    /// <summary>生えた場所の1件を書く。</summary>
    private static Task WritePlaceAsync(SqliteConnection connection, SqliteTransaction transaction, WorldState world, RuntimePlace place, CancellationToken ct) =>
        SqliteValues.ExecuteAsync(connection, transaction, PlaceUpsertSql, command =>
        {
            SqliteValues.Bind(command, "$world", world.Id.Value);
            SqliteValues.Bind(command, "$place", place.Id.Value);
            SqliteValues.Bind(command, "$name", place.Name);
            SqliteValues.Bind(command, "$tags", SqliteValues.Json(place.Tags, VivaceJsonContext.Default.ImmutableArrayString));
            SqliteValues.Bind(command, "$links", SqliteValues.Json(place.Links, VivaceJsonContext.Default.ImmutableArrayPlaceLink));
            SqliteValues.Bind(command, "$grp", place.Group);
            SqliteValues.Bind(command, "$status", place.Status.ToString());
            SqliteValues.Bind(command, "$created", place.CreatedSeq);
            SqliteValues.Bind(command, "$updated", place.UpdatedSeq);
        }, ct);

    /// <summary>変換の辞書の1件を書く。</summary>
    private static Task WriteTermAsync(SqliteConnection connection, SqliteTransaction transaction, WorldState world, TermEntry term, CancellationToken ct) =>
        SqliteValues.ExecuteAsync(connection, transaction, TermUpsertSql, command =>
        {
            SqliteValues.Bind(command, "$world", world.Id.Value);
            SqliteValues.Bind(command, "$term", term.Term);
            SqliteValues.Bind(command, "$verdict", term.Verdict.ToString());
            SqliteValues.Bind(command, "$mapped", term.Mapped);
            SqliteValues.Bind(command, "$category", term.Category);
            SqliteValues.Bind(command, "$created", term.CreatedSeq);
        }, ct);

    /// <summary>発生中の出来事の1件を書く。</summary>
    private static Task WriteIncidentAsync(SqliteConnection connection, SqliteTransaction transaction, WorldState world, ActiveIncident incident, CancellationToken ct) =>
        SqliteValues.ExecuteAsync(connection, transaction, IncidentInsertSql, command =>
        {
            SqliteValues.Bind(command, "$id", incident.Seq);
            SqliteValues.Bind(command, "$world", world.Id.Value);
            SqliteValues.Bind(command, "$persona", incident.Persona?.Value);
            SqliteValues.Bind(command, "$def", incident.DefId);
            SqliteValues.Bind(command, "$effects", SqliteValues.Json(incident.Effects, VivaceJsonContext.Default.ResolvedIncidentEffects));
            SqliteValues.Bind(command, "$started", SqliteValues.ToMs(incident.StartedAt));
            SqliteValues.Bind(command, "$ends", SqliteValues.ToMsOrNull(incident.EndsAt));
        }, ct);

    /// <summary>ペルソナの行、受信、未送信の操作を書く。変わった所だけ。</summary>
    private static async Task WritePersonaAsync(SqliteConnection connection, SqliteTransaction transaction, PersonaState? before, PersonaState persona, long updatedSeq, CancellationToken ct)
    {
        if (!ReferenceEquals(before, persona))
        {
            await WritePersonaRowAsync(connection, transaction, persona, updatedSeq, ct);
        }

        if (before is null || !ReferenceEquals(before.Inbox, persona.Inbox))
        {
            await SqliteValues.ExecuteAsync(connection, transaction, InboxDeleteSql, command => SqliteValues.Bind(command, "$persona", persona.Id.Value), ct);
            foreach (var item in persona.Inbox)
            {
                await WriteInboxItemAsync(connection, transaction, persona, item, ct);
            }
        }

        if (before is null || !ReferenceEquals(before.Outbound, persona.Outbound))
        {
            await SqliteValues.ExecuteAsync(connection, transaction, OutboundDeleteSql, command => SqliteValues.Bind(command, "$persona", persona.Id.Value), ct);
            foreach (var operation in persona.Outbound)
            {
                await WriteOutboundAsync(connection, transaction, persona, operation, ct);
            }
        }
    }

    /// <summary>ペルソナの行を書く。</summary>
    private static Task WritePersonaRowAsync(SqliteConnection connection, SqliteTransaction transaction, PersonaState persona, long updatedSeq, CancellationToken ct) =>
        SqliteValues.ExecuteAsync(connection, transaction, PersonaUpsertSql, command =>
        {
            SqliteValues.Bind(command, "$id", persona.Id.Value);
            SqliteValues.Bind(command, "$def", persona.DefId.Value);
            SqliteValues.Bind(command, "$world", persona.World.Value);
            SqliteValues.Bind(command, "$status", persona.Status.ToString());
            SqliteValues.Bind(command, "$location", SqliteValues.Json(persona.Location, VivaceJsonContext.Default.Location));
            SqliteValues.Bind(command, "$activity", SqliteValues.JsonOrNull(persona.Activity, VivaceJsonContext.Default.Activity));
            SqliteValues.Bind(command, "$overlay", SqliteValues.JsonOrNull(persona.Overlay, VivaceJsonContext.Default.Activity));
            SqliteValues.Bind(command, "$activityVersion", persona.ActivityVersion);
            SqliteValues.Bind(command, "$vitals", SqliteValues.Json(persona.Vitals, VivaceJsonContext.Default.Vitals));
            SqliteValues.Bind(command, "$conditions", SqliteValues.Json(persona.Conditions, VivaceJsonContext.Default.ImmutableArrayAttachedCondition));
            SqliteValues.Bind(command, "$plan", SqliteValues.JsonOrNull(persona.Plan, VivaceJsonContext.Default.DayPlan));
            SqliteValues.Bind(command, "$planVersion", persona.PlanVersion);
            SqliteValues.Bind(command, "$usage", SqliteValues.Json(persona.Usage, VivaceJsonContext.Default.ImmutableDictionaryStringActionUsage));
            SqliteValues.Bind(command, "$rng", SqliteValues.Json(persona.Rng, VivaceJsonContext.Default.ImmutableDictionaryStringUInt64));
            SqliteValues.Bind(command, "$posting", SqliteValues.Json(persona.Posting, VivaceJsonContext.Default.PostingState));
            SqliteValues.Bind(command, "$lastWake", SqliteValues.ToMsOrNull(persona.LastWake));
            SqliteValues.Bind(command, "$updated", updatedSeq);
            SqliteValues.Bind(command, "$deciding", persona.Deciding ? 1 : 0);
            SqliteValues.Bind(command, "$lastBlock", persona.LastBlockId);
            SqliteValues.Bind(command, "$plannedFor", persona.PlannedFor);
        }, ct);

    /// <summary>受信の1件を書く。</summary>
    private static Task WriteInboxItemAsync(SqliteConnection connection, SqliteTransaction transaction, PersonaState persona, InboxItem item, CancellationToken ct) =>
        SqliteValues.ExecuteAsync(connection, transaction, InboxInsertSql, command =>
        {
            SqliteValues.Bind(command, "$id", item.Seq);
            SqliteValues.Bind(command, "$persona", persona.Id.Value);
            SqliteValues.Bind(command, "$channel", item.Channel);
            SqliteValues.Bind(command, "$kind", item.Kind.ToString());
            SqliteValues.Bind(command, "$sender", SqliteValues.Json(item.Sender, VivaceJsonContext.Default.Sender));
            SqliteValues.Bind(command, "$audience", item.Audience.ToString());
            SqliteValues.Bind(command, "$body", item.Text ?? string.Empty);
            SqliteValues.Bind(command, "$arrived", SqliteValues.ToMsOrNull(item.OriginalArrivedAt));
            SqliteValues.Bind(command, "$item", SqliteValues.Json(item, VivaceJsonContext.Default.InboxItem));
        }, ct);

    /// <summary>未送信の操作の1件を書く。</summary>
    private static Task WriteOutboundAsync(SqliteConnection connection, SqliteTransaction transaction, PersonaState persona, OutboundOp operation, CancellationToken ct) =>
        SqliteValues.ExecuteAsync(connection, transaction, OutboundInsertSql, command =>
        {
            SqliteValues.Bind(command, "$id", operation.Seq);
            SqliteValues.Bind(command, "$persona", persona.Id.Value);
            SqliteValues.Bind(command, "$channel", operation.Channel);
            SqliteValues.Bind(command, "$kind", operation.OperationKind);
            SqliteValues.Bind(command, "$speech", operation.IsSpeech ? 1 : 0);
            SqliteValues.Bind(command, "$payload", operation.Payload.GetRawText());
        }, ct);
}
