using Microsoft.Data.Sqlite;
using Vivace.Core;
using Vivace.Core.Serialization;
using Vivace.Core.Storage;

namespace Vivace.Storage.Sqlite;

/// <summary>適用の記録の1件を、events の行として挿入する。</summary>
internal static class SqliteEventWriter
{
    /// <summary>events への挿入の SQL。</summary>
    private const string InsertSql = """
        INSERT INTO events (seq, time_utc, event_type, world_id, persona_id, status, status_reason, apply_no,
                            batch_seq, cause_seq, payload, guard, origin, trace, real_time_utc, enqueued_utc, applied_utc)
        VALUES ($seq, $time, $type, $world, $persona, $status, $reason, $applyNo,
                $batch, $cause, $payload, $guard, $origin, $trace, $realTime, $enqueued, $applied)
        """;

    /// <summary>1件の記録を挿入する。enqueued_utc はイベントの時刻、applied_utc は適用した時刻（適用のときだけ）。</summary>
    /// <param name="connection">接続。</param>
    /// <param name="transaction">トランザクション。</param>
    /// <param name="logged">記録の1件。</param>
    /// <param name="appliedAt">コミットした実時刻。適用の記録にだけ入れる。</param>
    /// <param name="ct">キャンセル。</param>
    public static Task InsertAsync(SqliteConnection connection, SqliteTransaction transaction, LoggedEvent logged, DateTimeOffset appliedAt, CancellationToken ct)
    {
        var ev = logged.Event;
        return SqliteValues.ExecuteAsync(connection, transaction, InsertSql, command =>
        {
            SqliteValues.Bind(command, "$seq", ev.Seq);
            SqliteValues.Bind(command, "$time", SqliteValues.ToMs(ev.Time));
            SqliteValues.Bind(command, "$type", ev.EventType);
            SqliteValues.Bind(command, "$world", ev.WorldId?.Value);
            SqliteValues.Bind(command, "$persona", ev.PersonaId?.Value);
            SqliteValues.Bind(command, "$status", SqliteNames.ToColumn(logged.Status));
            SqliteValues.Bind(command, "$reason", logged.Reason);
            SqliteValues.Bind(command, "$applyNo", logged.ApplyNo);
            SqliteValues.Bind(command, "$batch", ev.BatchSeq);
            SqliteValues.Bind(command, "$cause", ev.CauseSeq);
            SqliteValues.Bind(command, "$payload", PayloadJson.Serialize(ev.EventType, ev.Payload));
            SqliteValues.Bind(command, "$guard", ev.Guard is { } guard ? SqliteValues.Json(guard, VivaceJsonContext.Default.Guard) : null);
            SqliteValues.Bind(command, "$origin", SqliteValues.Json(ev.Origin, VivaceJsonContext.Default.Origin));
            SqliteValues.Bind(command, "$trace", ev.Trace is { } trace ? trace.GetRawText() : null);
            SqliteValues.Bind(command, "$realTime", ev.RealTime is { } real ? (object)SqliteValues.ToMs(real) : null);
            SqliteValues.Bind(command, "$enqueued", SqliteValues.ToMs(ev.Time));
            SqliteValues.Bind(command, "$applied", logged.Status == EventStatus.Applied ? (object)SqliteValues.ToMs(appliedAt) : null);
        }, ct);
    }
}
