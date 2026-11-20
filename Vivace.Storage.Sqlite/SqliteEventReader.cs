using Microsoft.Data.Sqlite;
using Vivace.Core;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Serialization;
using Vivace.Core.Storage;

namespace Vivace.Storage.Sqlite;

/// <summary>events の行を読む。知らないイベントタイプの行があれば例外。</summary>
internal static class SqliteEventReader
{
    /// <summary>行の全列。読む順は、呼び出し側が WHERE と ORDER BY で決める。</summary>
    private const string Columns = """
        seq, time_utc, event_type, world_id, persona_id, status, status_reason, apply_no,
        batch_seq, cause_seq, payload, guard, origin, trace, real_time_utc
        """;

    /// <summary>条件に合う行を、seq の昇順で読む。</summary>
    /// <param name="connection">接続。</param>
    /// <param name="where">WHERE から始まる条件（定数だけを渡す）。</param>
    /// <param name="ct">キャンセル。</param>
    public static Task<List<EventRow>> ReadRowsAsync(SqliteConnection connection, string where, CancellationToken ct) =>
        SqliteValues.QueryAsync(connection, $"SELECT {Columns} FROM events {where} ORDER BY seq", _ => { }, Map, ct);

    /// <summary>1行を、行の1件にする。</summary>
    private static EventRow Map(SqliteDataReader reader)
    {
        var eventType = reader.GetString(2);
        var ev = new SimEvent(
            reader.GetInt64(0),
            SqliteValues.FromMs(reader.GetInt64(1)),
            eventType,
            reader.IsDBNull(3) ? null : new WorldId(reader.GetString(3)),
            reader.IsDBNull(4) ? null : new PersonaId(reader.GetString(4)),
            PayloadJson.Deserialize(eventType, reader.GetString(10)),
            reader.IsDBNull(11) ? null : SqliteValues.ReadJson(reader.GetString(11), VivaceJsonContext.Default.Guard),
            reader.IsDBNull(8) ? null : reader.GetInt64(8),
            reader.IsDBNull(9) ? null : reader.GetInt64(9),
            SqliteValues.ReadJson(reader.GetString(12), VivaceJsonContext.Default.Origin),
            reader.IsDBNull(13) ? null : SqliteValues.ReadElement(reader.GetString(13)),
            reader.IsDBNull(14) ? null : SqliteValues.FromMs(reader.GetInt64(14)));
        var status = SqliteNames.FromColumn<EventRowStatus>(reader.GetString(5));
        var applyNo = reader.IsDBNull(7) ? (long?)null : reader.GetInt64(7);
        var reason = reader.IsDBNull(6) ? null : reader.GetString(6);
        return new EventRow(ev, status, applyNo, reason);
    }
}
