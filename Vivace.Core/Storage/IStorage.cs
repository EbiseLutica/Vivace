namespace Vivace.Core.Storage;

/// <summary>状態とイベントの保存のインターフェース。</summary>
public interface IStorage : IAsyncDisposable
{
    /// <summary>1つのトランザクションとして書く。</summary>
    /// <param name="batch">書くもの。</param>
    /// <param name="ct">キャンセル。</param>
    Task CommitAsync(StorageBatch batch, CancellationToken ct);

    /// <summary>起動のために、最後の状態と、未適用と処理中の行を読む。</summary>
    /// <param name="ct">キャンセル。</param>
    Task<RecoveredRecords> LoadAsync(CancellationToken ct);
}
