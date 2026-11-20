using System.Collections.Immutable;
using Microsoft.Data.Sqlite;
using Vivace.Core;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Serialization;

namespace Vivace.Storage.Sqlite;

/// <summary>
/// 状態を組み立てる。ワールドとペルソナは state の JSON だけから戻す（写しの表は読まない）。
/// カーソルは、適用番号が最大のイベントの位置（events から引く）。適用の記録が1件もなければ null。
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

    /// <summary>ワールドを、state の JSON から読む。</summary>
    private static async Task<ImmutableDictionary<WorldId, WorldState>> ReadWorldsAsync(SqliteConnection connection, CancellationToken ct)
    {
        var worlds = await SqliteValues.QueryAsync(connection,
            "SELECT state FROM worlds ORDER BY id",
            _ => { },
            reader => SqliteValues.ReadJson(reader.GetString(0), VivaceJsonContext.Default.WorldState),
            ct);
        return worlds.ToImmutableDictionary(world => world.Id);
    }

    /// <summary>ペルソナを、state の JSON から読む。</summary>
    private static async Task<ImmutableDictionary<PersonaId, PersonaState>> ReadPersonasAsync(SqliteConnection connection, CancellationToken ct)
    {
        var personas = await SqliteValues.QueryAsync(connection,
            "SELECT state FROM personas ORDER BY id",
            _ => { },
            reader => SqliteValues.ReadJson(reader.GetString(0), VivaceJsonContext.Default.PersonaState),
            ct);
        return personas.ToImmutableDictionary(persona => persona.Id);
    }
}
