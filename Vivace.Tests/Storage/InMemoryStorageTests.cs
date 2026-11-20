using System.Collections.Immutable;
using Vivace.Core;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Storage;
using Vivace.Testing;

namespace Vivace.Tests.Storage;

/// <summary>メモリの保存先の読み出し（起動のための未適用と処理中の行、最後の状態）。</summary>
public class InMemoryStorageTests
{
    /// <summary>未適用と処理中の行を連番の順に返し、適用済みの行は含めない。最後の状態と、連番の最大も返す。</summary>
    [Fact]
    public async Task LoadReturnsOpenRowsInSeqOrderAndTheLatestState()
    {
        var ct = TestContext.Current.CancellationToken;
        var storage = new InMemoryStorage();
        var first = new SimState(new ApplyCursor(DateTimeOffset.UnixEpoch, EventStage.DayTurn, 0, 0), ImmutableDictionary<WorldId, WorldState>.Empty, ImmutableDictionary<PersonaId, PersonaState>.Empty);
        var latest = first with { Cursor = new ApplyCursor(DateTimeOffset.UnixEpoch.AddHours(1), EventStage.DayTurn, 3, 2) };
        await storage.CommitAsync(new StorageBatch([DayStart(1), DayStart(2), DayStart(3)], [], first), ct);
        await storage.CommitAsync(new StorageBatch([], [new EventRowUpdate(1, EventRowStatus.Processing, 1, null)], latest), ct);
        await storage.CommitAsync(new StorageBatch([], [new EventRowUpdate(3, EventRowStatus.Applied, 2, null)], latest), ct);

        var recovered = await storage.LoadAsync(ct);

        Assert.Same(latest, recovered.State);
        Assert.Equal(3L, recovered.MaxSeq);
        Assert.Equal([1L, 2L], recovered.Open.Select(row => row.Event.Seq));
        Assert.Equal([EventRowStatus.Processing, EventRowStatus.Pending], recovered.Open.Select(row => row.Status));
        Assert.Equal(1L, recovered.Open[0].ApplyNo);
    }

    /// <summary>テスト用の日の始まりのイベント。</summary>
    /// <param name="seq">連番。</param>
    private static SimEvent DayStart(long seq) =>
        new(seq, new DateTimeOffset(2026, 11, 1, 14, 0, 0, TimeSpan.Zero), EventTypes.DayStarted, null, null, new DayStartedPayload(new DateOnly(2026, 11, 1)), null, null, null, Origin.Test, null, null);
}
