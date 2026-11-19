using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Vivace.Core;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Serialization;
using Vivace.Core.Storage;

namespace Vivace.Storage.Sqlite;

/// <summary>events の行を、記録の順（seq の昇順）に読む。</summary>
internal static class SqliteEventReader
{
    /// <summary>記録の全件の読み出し。</summary>
    private const string Sql = """
        SELECT seq, time_utc, event_type, world_id, persona_id, status, status_reason, apply_no,
               batch_seq, cause_seq, payload, guard, origin, trace, real_time_utc
        FROM events
        ORDER BY seq
        """;

    /// <summary>記録の全件を、seq の昇順で読む。知らないイベントタイプの行があれば例外。</summary>
    /// <param name="connection">接続。</param>
    /// <param name="ct">キャンセル。</param>
    public static Task<List<LoggedEvent>> ReadAsync(SqliteConnection connection, CancellationToken ct) =>
        SqliteValues.QueryAsync(connection, Sql, _ => { }, Map, ct);

    /// <summary>1行を記録の1件にする。</summary>
    private static LoggedEvent Map(SqliteDataReader reader)
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
        var status = ParseStatus(reader.GetString(5));
        var applyNo = reader.IsDBNull(7) ? (long?)null : reader.GetInt64(7);
        var reason = reader.IsDBNull(6) ? null : reader.GetString(6);
        return new LoggedEvent(ev, status, applyNo, reason);
    }

    /// <summary>結果の名前を読む（events の status 列）。</summary>
    private static EventStatus ParseStatus(string text) => text switch
    {
        "applied" => EventStatus.Applied,
        "skipped" => EventStatus.Skipped,
        "rejected" => EventStatus.Rejected,
        _ => throw new InvalidDataException($"知らない結果の名前です: {text}"),
    };
}
