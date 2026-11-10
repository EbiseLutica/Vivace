namespace Vivace.Core.Storage;

/// <summary>状態とイベントの保存の口。</summary>
public interface IStorage : IAsyncDisposable
{
    /// <summary>1つのトランザクションとして書く。</summary>
    /// <param name="batch">書くもの。</param>
    /// <param name="ct">キャンセル。</param>
    Task CommitAsync(StorageBatch batch, CancellationToken ct);
}
