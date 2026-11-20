using System.Collections.Immutable;
using Vivace.Core;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Storage;

namespace Vivace.Tests.Storage;

/// <summary>適用の記録の一覧を、コミットの形（挿入と行の更新）にする。テストで手で書くときに使う。</summary>
internal static class LoggedBatch
{
    /// <summary>記録を、積んだイベントの挿入と、結果の更新にする。</summary>
    /// <param name="log">記録の一覧。</param>
    /// <param name="state">コミットの後の状態。</param>
    public static StorageBatch From(IEnumerable<LoggedEvent> log, SimState state)
    {
        var entries = log.ToList();
        var enqueued = entries.Select(entry => entry.Event).ToImmutableArray();
        var updates = entries.Select(entry => new EventRowUpdate(entry.Event.Seq, RowStatus(entry.Status), entry.ApplyNo, entry.Reason)).ToImmutableArray();
        return new StorageBatch(enqueued, updates, state);
    }

    /// <summary>記録の結果を、行の状態にする。</summary>
    /// <param name="status">記録の結果。</param>
    private static EventRowStatus RowStatus(EventStatus status) => status switch
    {
        EventStatus.Applied => EventRowStatus.Applied,
        EventStatus.Skipped => EventRowStatus.Skipped,
        _ => EventRowStatus.Rejected,
    };
}
