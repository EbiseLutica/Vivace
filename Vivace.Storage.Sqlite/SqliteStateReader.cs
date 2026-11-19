using System.Collections.Immutable;
using Microsoft.Data.Sqlite;
using Vivace.Core;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Serialization;

namespace Vivace.Storage.Sqlite;

/// <summary>
/// 状態の行から SimState を組み立てる。カーソルは、適用番号が最大のイベントの位置（events から引く）。
/// 適用の記録が1件もなければ null。
/// </summary>
internal static class SqliteStateReader
{
    /// <summary>適用番号が最大のイベントの位置。</summary>
    private const string CursorSql = """
        SELECT time_utc, event_type, seq, apply_no
        FROM events
        WHERE apply_no IS NOT NULL
        ORDER BY apply_no DESC
        LIMIT 1
        """;

    /// <summary>ペルソナの行を、読み終えた後に組み立てるための素の値。</summary>
    /// <param name="Id">ID。</param>
    /// <param name="DefId">定義の ID。</param>
    /// <param name="World">ワールドの ID。</param>
    /// <param name="Status">状態の名前。</param>
    /// <param name="Location">居場所の JSON。</param>
    /// <param name="Activity">行動の JSON。なければ null。</param>
    /// <param name="ActivityVersion">行動のバージョン。</param>
    /// <param name="Overlay">副行動の JSON。なければ null。</param>
    /// <param name="Vitals">状態変数の JSON。</param>
    /// <param name="Conditions">状態の JSON。</param>
    /// <param name="Plan">プランの JSON。なければ null。</param>
    /// <param name="PlanVersion">プランのバージョン。</param>
    /// <param name="Usage">利用の記録の JSON。</param>
    /// <param name="Rng">乱数の位置の JSON。</param>
    /// <param name="Posting">投稿の状態の JSON。</param>
    /// <param name="LastWake">最後に起きた時刻（Unix ミリ秒）。なければ null。</param>
    /// <param name="Deciding">決定タイミングの処理中かどうかの値（0 か 1）。</param>
    /// <param name="LastBlockId">最後に始めたブロックの ID。</param>
    /// <param name="PlannedFor">Planner を起こした期間のキー。</param>
    private sealed record PersonaRow(
        string Id,
        string DefId,
        string World,
        string Status,
        string Location,
        string? Activity,
        string? Overlay,
        long ActivityVersion,
        string Vitals,
        string Conditions,
        string? Plan,
        long PlanVersion,
        string Usage,
        string Rng,
        string Posting,
        long? LastWake,
        long Deciding,
        string? LastBlockId,
        string? PlannedFor);

    /// <summary>状態を読む。</summary>
    /// <param name="connection">接続。</param>
    /// <param name="ct">キャンセル。</param>
    public static async Task<SimState?> ReadAsync(SqliteConnection connection, CancellationToken ct)
    {
        var cursor = await ReadCursorAsync(connection, ct);
        if (cursor is null)
        {
            return null;
        }

        var worlds = await ReadWorldsAsync(connection, ct);
        var personas = await ReadPersonasAsync(connection, ct);
        return new SimState(cursor.Value, worlds, personas);
    }

    /// <summary>適用番号が最大のイベントから、カーソルを作る。</summary>
    private static async Task<ApplyCursor?> ReadCursorAsync(SqliteConnection connection, CancellationToken ct)
    {
        var rows = await SqliteValues.QueryAsync(connection, CursorSql, _ => { }, reader => new ApplyCursor(
            SqliteValues.FromMs(reader.GetInt64(0)),
            EventTypes.StageOf(reader.GetString(1)),
            reader.GetInt64(2),
            reader.GetInt64(3)), ct);
        return rows.Count == 0 ? null : rows[0];
    }

    /// <summary>ワールドを、生えた場所、辞書、出来事と合わせて読む。</summary>
    private static async Task<ImmutableDictionary<WorldId, WorldState>> ReadWorldsAsync(SqliteConnection connection, CancellationToken ct)
    {
        var rows = await SqliteValues.QueryAsync(connection, "SELECT id, def_id, seed, status, graph_version FROM worlds ORDER BY id", _ => { }, reader => (
            Id: reader.GetString(0),
            DefId: reader.GetString(1),
            Seed: reader.GetInt64(2),
            Status: reader.GetString(3),
            GraphVersion: reader.GetInt64(4)), ct);

        var worlds = new List<WorldState>();
        foreach (var row in rows)
        {
            var id = new WorldId(row.Id);
            worlds.Add(new WorldState(
                id,
                new WorldDefId(row.DefId),
                unchecked((ulong)row.Seed),
                Enum.Parse<WorldStatus>(row.Status),
                await ReadPlacesAsync(connection, id, ct),
                await ReadTermsAsync(connection, id, ct),
                await ReadIncidentsAsync(connection, id, ct),
                row.GraphVersion));
        }

        return worlds.ToImmutableDictionary(world => world.Id);
    }

    /// <summary>ワールドの生えた場所を読む。</summary>
    private static async Task<ImmutableDictionary<PlaceId, RuntimePlace>> ReadPlacesAsync(SqliteConnection connection, WorldId world, CancellationToken ct)
    {
        var places = await SqliteValues.QueryAsync(connection,
            "SELECT place_id, name, tags, links, grp, status, created_seq, updated_seq FROM world_places WHERE world_id = $world ORDER BY place_id",
            command => SqliteValues.Bind(command, "$world", world.Value),
            reader => new RuntimePlace(
                new PlaceId(reader.GetString(0)),
                reader.GetString(1),
                SqliteValues.ReadJson(reader.GetString(2), VivaceJsonContext.Default.ImmutableArrayString),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                SqliteValues.ReadJson(reader.GetString(3), VivaceJsonContext.Default.ImmutableArrayPlaceLink),
                Enum.Parse<PlaceStatus>(reader.GetString(5)),
                reader.GetInt64(6),
                reader.GetInt64(7)),
            ct);
        return places.ToImmutableDictionary(place => place.Id);
    }

    /// <summary>ワールドの変換の辞書を読む。</summary>
    private static async Task<ImmutableDictionary<string, TermEntry>> ReadTermsAsync(SqliteConnection connection, WorldId world, CancellationToken ct)
    {
        var terms = await SqliteValues.QueryAsync(connection,
            "SELECT term, verdict, mapped, category, created_seq FROM world_terms WHERE world_id = $world ORDER BY term",
            command => SqliteValues.Bind(command, "$world", world.Value),
            reader => new TermEntry(
                reader.GetString(0),
                Enum.Parse<TermVerdict>(reader.GetString(1)),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetInt64(4)),
            ct);
        return terms.ToImmutableDictionary(term => term.Term);
    }

    /// <summary>ワールドの発生中の出来事を読む。キーは発生のイベントの連番。</summary>
    private static async Task<ImmutableDictionary<long, ActiveIncident>> ReadIncidentsAsync(SqliteConnection connection, WorldId world, CancellationToken ct)
    {
        var incidents = await SqliteValues.QueryAsync(connection,
            "SELECT id, def_id, persona_id, effects, started_utc, ends_utc FROM incidents WHERE world_id = $world ORDER BY id",
            command => SqliteValues.Bind(command, "$world", world.Value),
            reader => new ActiveIncident(
                reader.GetInt64(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : new PersonaId(reader.GetString(2)),
                SqliteValues.FromMs(reader.GetInt64(4)),
                reader.IsDBNull(5) ? null : SqliteValues.FromMs(reader.GetInt64(5)),
                SqliteValues.ReadJson(reader.GetString(3), VivaceJsonContext.Default.ResolvedIncidentEffects)),
            ct);
        return incidents.ToImmutableDictionary(incident => incident.Seq);
    }

    /// <summary>ペルソナを、受信と未送信の操作と合わせて読む。</summary>
    private static async Task<ImmutableDictionary<PersonaId, PersonaState>> ReadPersonasAsync(SqliteConnection connection, CancellationToken ct)
    {
        var rows = await SqliteValues.QueryAsync(connection, """
            SELECT id, def_id, world_id, status, location, activity, overlay, activity_version, vitals, conditions,
                   plan, plan_version, usage, rng, posting, last_wake_utc, deciding, last_block_id, planned_for
            FROM personas
            ORDER BY id
            """, _ => { }, reader => new PersonaRow(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.GetInt64(7),
            reader.GetString(8),
            reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.GetInt64(11),
            reader.GetString(12),
            reader.GetString(13),
            reader.GetString(14),
            reader.IsDBNull(15) ? null : reader.GetInt64(15),
            reader.GetInt64(16),
            reader.IsDBNull(17) ? null : reader.GetString(17),
            reader.IsDBNull(18) ? null : reader.GetString(18)), ct);

        var personas = new List<PersonaState>();
        foreach (var row in rows)
        {
            var id = new PersonaId(row.Id);
            personas.Add(new PersonaState(
                id,
                new PersonaDefId(row.DefId),
                new WorldId(row.World),
                Enum.Parse<PersonaStatus>(row.Status),
                SqliteValues.ReadJson(row.Location, VivaceJsonContext.Default.Location),
                row.Activity is null ? null : SqliteValues.ReadJson(row.Activity, VivaceJsonContext.Default.Activity),
                row.ActivityVersion,
                row.Overlay is null ? null : SqliteValues.ReadJson(row.Overlay, VivaceJsonContext.Default.Activity),
                SqliteValues.ReadJson(row.Vitals, VivaceJsonContext.Default.Vitals),
                SqliteValues.ReadJson(row.Conditions, VivaceJsonContext.Default.ImmutableArrayAttachedCondition),
                row.Plan is null ? null : SqliteValues.ReadJson(row.Plan, VivaceJsonContext.Default.DayPlan),
                row.PlanVersion,
                SqliteValues.ReadJson(row.Usage, VivaceJsonContext.Default.ImmutableDictionaryStringActionUsage),
                SqliteValues.ReadJson(row.Rng, VivaceJsonContext.Default.ImmutableDictionaryStringUInt64),
                SqliteValues.ReadJson(row.Posting, VivaceJsonContext.Default.PostingState),
                await ReadInboxAsync(connection, id, ct),
                await ReadOutboundAsync(connection, id, ct),
                SqliteValues.FromMsOrNull(row.LastWake),
                row.Deciding != 0,
                row.LastBlockId,
                row.PlannedFor));
        }

        return personas.ToImmutableDictionary(persona => persona.Id);
    }

    /// <summary>ペルソナの受信を、到着の順（id の昇順）に読む。</summary>
    private static async Task<ImmutableList<InboxItem>> ReadInboxAsync(SqliteConnection connection, PersonaId persona, CancellationToken ct)
    {
        var items = await SqliteValues.QueryAsync(connection,
            "SELECT item FROM inbox WHERE persona_id = $persona ORDER BY id",
            command => SqliteValues.Bind(command, "$persona", persona.Value),
            reader => SqliteValues.ReadJson(reader.GetString(0), VivaceJsonContext.Default.InboxItem),
            ct);
        return items.ToImmutableList();
    }

    /// <summary>ペルソナの未送信の操作を、連番の順に読む。</summary>
    private static async Task<ImmutableList<OutboundOp>> ReadOutboundAsync(SqliteConnection connection, PersonaId persona, CancellationToken ct)
    {
        var operations = await SqliteValues.QueryAsync(connection,
            "SELECT id, channel, op_kind, is_speech, payload FROM outbound WHERE persona_id = $persona ORDER BY id",
            command => SqliteValues.Bind(command, "$persona", persona.Value),
            reader => new OutboundOp(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt64(3) != 0,
                SqliteValues.ReadElement(reader.GetString(4))),
            ct);
        return operations.ToImmutableList();
    }
}
