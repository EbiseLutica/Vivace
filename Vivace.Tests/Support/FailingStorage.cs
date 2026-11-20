using Vivace.Core.Storage;
using Vivace.Testing;

namespace Vivace.Tests.Support;

/// <summary>決めた回数目のコミットで例外を投げる保存先。それより前のコミットはメモリに残す。</summary>
internal sealed class FailingStorage : IStorage
{
    /// <summary>成功したコミットを残す保存先。</summary>
    private readonly InMemoryStorage _inner = new();

    /// <summary>例外を投げるコミットの回数目（1 から数える）。</summary>
    private readonly int _failAt;

    /// <summary>これまでのコミットの回数。</summary>
    private int _commits;

    /// <summary>例外を投げる回数目を決めて作る。</summary>
    /// <param name="failAt">例外を投げるコミットの回数目（1 から数える）。</param>
    public FailingStorage(int failAt)
    {
        _failAt = failAt;
    }

    /// <summary>成功したコミットの記録。</summary>
    public IReadOnlyList<LoggedEvent> Events => _inner.Events;

    /// <summary>決めた回数目なら例外を投げ、それ以外はメモリに書く。</summary>
    public Task CommitAsync(StorageBatch batch, CancellationToken ct)
    {
        if (++_commits == _failAt)
        {
            throw new IOException("わざと失敗させたコミット");
        }

        return _inner.CommitAsync(batch, ct);
    }

    /// <summary>成功したコミットの内容を読み出す。</summary>
    public Task<RecoveredRecords> LoadAsync(CancellationToken ct) => _inner.LoadAsync(ct);

    /// <summary>何もしない。</summary>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
