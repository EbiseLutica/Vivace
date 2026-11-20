using System.IO;
using Microsoft.Data.Sqlite;
using Vivace.Core;
using Vivace.Core.Events;
using Vivace.Core.Serialization;
using Vivace.Core.Storage;

namespace Vivace.Storage.Sqlite;

/// <summary>events の行を書く。積んだイベントは未適用の行として挿入し、状態の変化は既存の行を更新する。</summary>
internal static class SqliteEventWriter
{
    /// <summary>未適用の行の挿入の SQL。適用番号と適用の時刻は、適用のときに入れる。</summary>
    private const string InsertSql = """
        INSERT INTO events (seq, time_utc, event_type, world_id, persona_id, status, status_reason, apply_no,
                            batch_seq, cause_seq, payload, guard, origin, trace, real_time_utc, enqueued_utc, applied_utc)
        VALUES ($seq, $time, $type, $world, $persona, 'pending', NULL, NULL,
                $batch, $cause, $payload, $guard, $origin, $trace, $realTime, $enqueued, NULL)
        """;

    /// <summary>行の状態の更新の SQL。適用番号を振るときだけ、適用の時刻を入れる（既にあれば残す）。</summary>
    private const string UpdateSql = """
        UPDATE events
        SET status = $status, status_reason = $reason, apply_no = COALESCE($applyNo, apply_no),
            applied_utc = CASE WHEN $applyNo IS NULL THEN applied_utc ELSE COALESCE(applied_utc, $applied) END
        WHERE seq = $seq
        """;

    /// <summary>積んだイベントを、未適用の行として挿入する。enqueued_utc はイベントの時刻。</summary>
    /// <param name="connection">接続。</param>
    /// <param name="transaction">トランザクション。</param>
    /// <param name="ev">積んだイベント。</param>
    /// <param name="ct">キャンセル。</param>
    public static Task InsertPendingAsync(SqliteConnection connection, SqliteTransaction transaction, SimEvent ev, CancellationToken ct) =>
        SqliteValues.ExecuteAsync(connection, transaction, InsertSql, command =>
        {
            SqliteValues.Bind(command, "$seq", ev.Seq);
            SqliteValues.Bind(command, "$time", SqliteValues.ToMs(ev.Time));
            SqliteValues.Bind(command, "$type", ev.EventType);
            SqliteValues.Bind(command, "$world", ev.WorldId?.Value);
            SqliteValues.Bind(command, "$persona", ev.PersonaId?.Value);
            SqliteValues.Bind(command, "$batch", ev.BatchSeq);
            SqliteValues.Bind(command, "$cause", ev.CauseSeq);
            SqliteValues.Bind(command, "$payload", PayloadJson.Serialize(ev.EventType, ev.Payload));
            SqliteValues.Bind(command, "$guard", ev.Guard is { } guard ? SqliteValues.Json(guard, VivaceJsonContext.Default.Guard) : null);
            SqliteValues.Bind(command, "$origin", SqliteValues.Json(ev.Origin, VivaceJsonContext.Default.Origin));
            SqliteValues.Bind(command, "$trace", ev.Trace is { } trace ? trace.GetRawText() : null);
            SqliteValues.Bind(command, "$realTime", ev.RealTime is { } real ? (object)SqliteValues.ToMs(real) : null);
            SqliteValues.Bind(command, "$enqueued", SqliteValues.ToMs(ev.Time));
        }, ct);

    /// <summary>既存の行の状態を更新する。対象の行がなければ例外にする（書き漏れを隠さない）。</summary>
    /// <param name="connection">接続。</param>
    /// <param name="transaction">トランザクション。</param>
    /// <param name="update">更新の内容。</param>
    /// <param name="appliedAt">適用の時刻（適用番号を振るときだけ使う）。</param>
    /// <param name="ct">キャンセル。</param>
    public static async Task UpdateAsync(SqliteConnection connection, SqliteTransaction transaction, EventRowUpdate update, DateTimeOffset appliedAt, CancellationToken ct)
    {
        var changed = await SqliteValues.ExecuteAsync(connection, transaction, UpdateSql, command =>
        {
            SqliteValues.Bind(command, "$seq", update.Seq);
            SqliteValues.Bind(command, "$status", SqliteNames.ToColumn(update.Status));
            SqliteValues.Bind(command, "$reason", update.Reason);
            SqliteValues.Bind(command, "$applyNo", update.ApplyNo);
            SqliteValues.Bind(command, "$applied", SqliteValues.ToMs(appliedAt));
        }, ct);
        if (changed != 1)
        {
            throw new InvalidDataException($"更新する events の行がありません: seq {update.Seq}");
        }
    }
}
