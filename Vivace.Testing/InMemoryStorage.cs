using System.Collections.Immutable;
using Vivace.Core;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Storage;

namespace Vivace.Testing;

/// <summary>メモリの上の保存先。行の状態と、適用の記録（適用された順）と、最後にコミットした状態を持つ。</summary>
public sealed class InMemoryStorage : IStorage
{
    /// <summary>行の状態。連番で引く。</summary>
    private readonly Dictionary<long, Row> _rows = [];

    /// <summary>適用の記録（結果が決まった順）。処理中の行は、適用番号を振ったときに入る。</summary>
    private readonly List<LoggedEvent> _events = [];

    /// <summary>これまでの記録（適用、捨て、却下の順）。</summary>
    public IReadOnlyList<LoggedEvent> Events => _events;

    /// <summary>最後にコミットした状態。まだなければ null。</summary>
    public SimState? Latest { get; private set; }

    /// <inheritdoc />
    public Task CommitAsync(StorageBatch batch, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        foreach (var ev in batch.Enqueued)
        {
            _rows[ev.Seq] = new Row(ev, EventRowStatus.Pending, null, null);
        }

        foreach (var update in batch.Updates)
        {
            var row = _rows[update.Seq];
            _rows[update.Seq] = row with { Status = update.Status, ApplyNo = update.ApplyNo ?? row.ApplyNo, Reason = update.Reason };
            if (update.ApplyNo is { } applyNo)
            {
                _events.Add(new LoggedEvent(row.Event, EventStatus.Applied, applyNo, null));
            }
            else if (update.Status is EventRowStatus.Skipped or EventRowStatus.Rejected)
            {
                _events.Add(new LoggedEvent(row.Event, update.Status == EventRowStatus.Skipped ? EventStatus.Skipped : EventStatus.Rejected, null, update.Reason));
            }
        }

        Latest = batch.State;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<RecoveredRecords> LoadAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var open = _rows.Values
            .Where(row => row.Status is EventRowStatus.Pending or EventRowStatus.Processing)
            .OrderBy(row => row.Event.Seq)
            .Select(row => new EventRow(row.Event, row.Status, row.ApplyNo, row.Reason))
            .ToImmutableArray();
        var maxSeq = _rows.Keys.DefaultIfEmpty(0).Max();
        return Task.FromResult(new RecoveredRecords(Latest, open, maxSeq));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>行の中身。</summary>
    /// <param name="Event">イベント。</param>
    /// <param name="Status">行の状態。</param>
    /// <param name="ApplyNo">適用番号。</param>
    /// <param name="Reason">理由。</param>
    private sealed record Row(SimEvent Event, EventRowStatus Status, long? ApplyNo, string? Reason);
}
