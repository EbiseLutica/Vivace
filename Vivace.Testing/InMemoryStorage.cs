using System.Collections.Immutable;
using Vivace.Core.Domain;
using Vivace.Core.Storage;

namespace Vivace.Testing;

/// <summary>
/// メモリの上の保存先（段階2 の最小の実装、段階4 で捨てる前提）。記録と、最後にコミットした状態を持つ。
/// </summary>
public sealed class InMemoryStorage : IStorage
{
    private readonly List<LoggedEvent> _events = [];

    /// <summary>これまでの記録（追加された順）。</summary>
    public IReadOnlyList<LoggedEvent> Events => _events;

    /// <summary>最後にコミットした状態。まだなければ null。</summary>
    public SimState? Latest { get; private set; }

    /// <inheritdoc />
    public Task CommitAsync(StorageBatch batch, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _events.AddRange(batch.Events);
        Latest = batch.State;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
