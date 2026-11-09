namespace Vivace.Core.Storage;

/// <summary>
/// 状態とイベントの保存の口（D4 4.2）。段階2 では最小の形（書き込みだけ）。読み込みと記憶は段階4 と段階7 で足す。
/// </summary>
public interface IStorage : IAsyncDisposable
{
    /// <summary>1つのトランザクションとして書く。</summary>
    /// <param name="batch">書くもの。</param>
    /// <param name="ct">キャンセル。</param>
    Task CommitAsync(StorageBatch batch, CancellationToken ct);
}
