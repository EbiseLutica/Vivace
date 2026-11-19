using Vivace.Core.Domain.State;
using Vivace.Core.Storage;

namespace Vivace.Storage.Sqlite;

/// <summary>
/// SQLite に記録と状態を書く保存先。コミットは1つのトランザクションで行い、失敗したら何も残さない。
/// 破棄すると、データベースの接続も閉じる。
/// </summary>
public sealed class SqliteStorage : IStorage
{
    /// <summary>接続を持つデータベース。</summary>
    private readonly SqliteDatabase _database;

    /// <summary>適用の時刻に使う実時刻。</summary>
    private readonly TimeProvider _time;

    /// <summary>前にコミットした（または読んだ）状態。変わった行を判る基準に使う。読んでいなければ null。</summary>
    private SimState? _committed;

    /// <summary>開いたデータベースを使う保存先を作る。</summary>
    /// <param name="database">開いたデータベース。</param>
    /// <param name="time">実時刻（適用の記録の時刻に使う）。省略すると TimeProvider.System。</param>
    public SqliteStorage(SqliteDatabase database, TimeProvider? time = null)
    {
        _database = database;
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task CommitAsync(StorageBatch batch, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var connection = _database.Connection;
        var appliedAt = _time.GetUtcNow();
        await using var transaction = connection.BeginTransaction();
        foreach (var logged in batch.Events)
        {
            await SqliteEventWriter.InsertAsync(connection, transaction, logged, appliedAt, ct);
        }

        var updatedSeq = batch.Events.IsEmpty ? 0 : batch.Events.Max(logged => logged.Event.Seq);
        await SqliteStateWriter.WriteAsync(connection, transaction, _committed, batch.State, updatedSeq, ct);
        await transaction.CommitAsync(ct);
        _committed = batch.State;
    }

    /// <summary>記録の全件を、seq の昇順で読む。</summary>
    /// <param name="ct">キャンセル。</param>
    public async Task<IReadOnlyList<LoggedEvent>> ReadEventsAsync(CancellationToken ct) =>
        await SqliteEventReader.ReadAsync(_database.Connection, ct);

    /// <summary>
    /// 状態の行から SimState を組み立てる。組み立てた状態は、次のコミットで変わった行を判る基準になる。
    /// 適用の記録が1件もなければ null。
    /// </summary>
    /// <param name="ct">キャンセル。</param>
    public async Task<SimState?> LoadStateAsync(CancellationToken ct)
    {
        var state = await SqliteStateReader.ReadAsync(_database.Connection, ct);
        _committed = state;
        return state;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _database.DisposeAsync();
}
