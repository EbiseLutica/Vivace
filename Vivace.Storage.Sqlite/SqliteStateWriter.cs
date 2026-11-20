using Microsoft.Data.Sqlite;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Serialization;

namespace Vivace.Storage.Sqlite;

/// <summary>
/// 状態を書く。前にコミットした状態と参照を比べ、変わった行だけを書く（イミュータブルなので参照で判る）。
/// ワールドとペルソナの行は、状態の全体を state の JSON にする。子の表（生えた場所、辞書、出来事、受信、未送信の操作）は写しで、
/// 一覧の参照が変わったら所属の行を消して入れ直す。
/// </summary>
internal static class SqliteStateWriter
{
    /// <summary>ワールドの行の挿入と更新。作成の連番は world.created のイベントから引く。</summary>
    private const string WorldUpsertSql = """
        INSERT INTO worlds (id, def_id, status, state, created_seq)
        VALUES ($id, $def, $status, $state,
                COALESCE((SELECT MIN(seq) FROM events WHERE world_id = $id AND event_type = 'world.created'), 0))
        ON CONFLICT(id) DO UPDATE SET def_id = excluded.def_id, status = excluded.status, state = excluded.state
        """;

    /// <summary>生えた場所の挿入と更新（写し）。</summary>
    private const string PlaceUpsertSql = """
        INSERT INTO world_places (world_id, place_id, name, tags, links, grp, status, created_seq, updated_seq)
        VALUES ($world, $place, $name, $tags, $links, $grp, $status, $created, $updated)
        ON CONFLICT(world_id, place_id) DO UPDATE SET name = excluded.name, tags = excluded.tags, links = excluded.links,
                                                      grp = excluded.grp, status = excluded.status, updated_seq = excluded.updated_seq
        """;

    /// <summary>変換の辞書の挿入と更新（写し）。</summary>
    private const string TermUpsertSql = """
        INSERT INTO world_terms (world_id, term, verdict, mapped, category, created_seq)
        VALUES ($world, $term, $verdict, $mapped, $category, $created)
        ON CONFLICT(world_id, term) DO UPDATE SET verdict = excluded.verdict, mapped = excluded.mapped, category = excluded.category
        """;

    /// <summary>ワールドの出来事の、所属の行の削除（写し）。</summary>
    private const string IncidentDeleteSql = "DELETE FROM incidents WHERE world_id = $world";

    /// <summary>出来事の挿入（写し）。</summary>
    private const string IncidentInsertSql = """
        INSERT INTO incidents (id, world_id, persona_id, def_id, effects, started_utc, ends_utc)
        VALUES ($id, $world, $persona, $def, $effects, $started, $ends)
        """;

    /// <summary>ペルソナの行の挿入と更新。作成の連番は persona.created のイベントから引く。</summary>
    private const string PersonaUpsertSql = """
        INSERT INTO personas (id, def_id, world_id, status, state, created_seq, updated_seq)
        VALUES ($id, $def, $world, $status, $state,
                COALESCE((SELECT MIN(seq) FROM events WHERE persona_id = $id AND event_type = 'persona.created'), $updated),
                $updated)
        ON CONFLICT(id) DO UPDATE SET def_id = excluded.def_id, world_id = excluded.world_id, status = excluded.status,
                                      state = excluded.state, updated_seq = excluded.updated_seq
        """;

    /// <summary>受信の、所属の行の削除（写し）。</summary>
    private const string InboxDeleteSql = "DELETE FROM inbox WHERE persona_id = $persona";

    /// <summary>受信の挿入（写し）。状態の写しなので、未処理の unread だけを書く（J-188）。</summary>
    private const string InboxInsertSql = """
        INSERT INTO inbox (id, persona_id, channel, kind, sender, audience, body, original_arrived_utc, status, handled_seq, outcome)
        VALUES ($id, $persona, $channel, $kind, $sender, $audience, $body, $arrived, 'unread', NULL, NULL)
        """;

    /// <summary>未送信の操作の、所属の行の削除（写し）。</summary>
    private const string OutboundDeleteSql = "DELETE FROM outbound WHERE persona_id = $persona";

    /// <summary>未送信の操作の挿入（写し）。送信前の操作として入れる。</summary>
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

    /// <summary>ワールドの行（state ごと）と、生えた場所、辞書、出来事の写しを書く。変わった所だけ。</summary>
    private static async Task WriteWorldAsync(SqliteConnection connection, SqliteTransaction transaction, WorldState? before, WorldState world, CancellationToken ct)
    {
        if (!ReferenceEquals(before, world))
        {
            await SqliteValues.ExecuteAsync(connection, transaction, WorldUpsertSql, command =>
            {
                SqliteValues.Bind(command, "$id", world.Id.Value);
                SqliteValues.Bind(command, "$def", world.DefId.Value);
                SqliteValues.Bind(command, "$status", SqliteNames.ToColumn(world.Status));
                SqliteValues.Bind(command, "$state", SqliteValues.Json(world, VivaceJsonContext.Default.WorldState));
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

    /// <summary>生えた場所の1件を写しとして書く。</summary>
    private static Task WritePlaceAsync(SqliteConnection connection, SqliteTransaction transaction, WorldState world, RuntimePlace place, CancellationToken ct) =>
        SqliteValues.ExecuteAsync(connection, transaction, PlaceUpsertSql, command =>
        {
            SqliteValues.Bind(command, "$world", world.Id.Value);
            SqliteValues.Bind(command, "$place", place.Id.Value);
            SqliteValues.Bind(command, "$name", place.Name);
            SqliteValues.Bind(command, "$tags", SqliteValues.Json(place.Tags, VivaceJsonContext.Default.ImmutableArrayString));
            SqliteValues.Bind(command, "$links", SqliteValues.Json(place.Links, VivaceJsonContext.Default.ImmutableArrayPlaceLink));
            SqliteValues.Bind(command, "$grp", place.Group);
            SqliteValues.Bind(command, "$status", SqliteNames.ToColumn(place.Status));
            SqliteValues.Bind(command, "$created", place.CreatedSeq);
            SqliteValues.Bind(command, "$updated", place.UpdatedSeq);
        }, ct);

    /// <summary>変換の辞書の1件を写しとして書く。</summary>
    private static Task WriteTermAsync(SqliteConnection connection, SqliteTransaction transaction, WorldState world, TermEntry term, CancellationToken ct) =>
        SqliteValues.ExecuteAsync(connection, transaction, TermUpsertSql, command =>
        {
            SqliteValues.Bind(command, "$world", world.Id.Value);
            SqliteValues.Bind(command, "$term", term.Term);
            SqliteValues.Bind(command, "$verdict", SqliteNames.ToColumn(term.Verdict));
            SqliteValues.Bind(command, "$mapped", term.Mapped);
            SqliteValues.Bind(command, "$category", term.Category);
            SqliteValues.Bind(command, "$created", term.CreatedSeq);
        }, ct);

    /// <summary>発生中の出来事の1件を写しとして書く。</summary>
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

    /// <summary>ペルソナの行（state ごと）と、受信、未送信の操作の写しを書く。変わった所だけ。</summary>
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

    /// <summary>ペルソナの行を書く（state ごと）。</summary>
    private static Task WritePersonaRowAsync(SqliteConnection connection, SqliteTransaction transaction, PersonaState persona, long updatedSeq, CancellationToken ct) =>
        SqliteValues.ExecuteAsync(connection, transaction, PersonaUpsertSql, command =>
        {
            SqliteValues.Bind(command, "$id", persona.Id.Value);
            SqliteValues.Bind(command, "$def", persona.DefId.Value);
            SqliteValues.Bind(command, "$world", persona.World.Value);
            SqliteValues.Bind(command, "$status", SqliteNames.ToColumn(persona.Status));
            SqliteValues.Bind(command, "$state", SqliteValues.Json(persona, VivaceJsonContext.Default.PersonaState));
            SqliteValues.Bind(command, "$updated", updatedSeq);
        }, ct);

    /// <summary>受信の1件を写しとして書く。</summary>
    private static Task WriteInboxItemAsync(SqliteConnection connection, SqliteTransaction transaction, PersonaState persona, InboxItem item, CancellationToken ct) =>
        SqliteValues.ExecuteAsync(connection, transaction, InboxInsertSql, command =>
        {
            SqliteValues.Bind(command, "$id", item.Seq);
            SqliteValues.Bind(command, "$persona", persona.Id.Value);
            SqliteValues.Bind(command, "$channel", item.Channel);
            SqliteValues.Bind(command, "$kind", SqliteNames.ToColumn(item.Kind));
            SqliteValues.Bind(command, "$sender", SqliteValues.Json(item.Sender, VivaceJsonContext.Default.Sender));
            SqliteValues.Bind(command, "$audience", SqliteNames.ToColumn(item.Audience));
            SqliteValues.Bind(command, "$body", item.Text ?? string.Empty);
            SqliteValues.Bind(command, "$arrived", SqliteValues.ToMsOrNull(item.OriginalArrivedAt));
        }, ct);

    /// <summary>未送信の操作の1件を写しとして書く。</summary>
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
