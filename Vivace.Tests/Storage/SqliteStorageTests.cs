using System.Collections.Immutable;
using Vivace.Core;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Storage;
using Vivace.Storage.Sqlite;

namespace Vivace.Tests.Storage;

/// <summary>SqliteStorage の書き込みと読み出し（小さな例で）。</summary>
public class SqliteStorageTests
{
    /// <summary>適用の記録は、書いたとおりに読み戻せる（イベント、結果、適用番号、理由）。</summary>
    [Fact]
    public async Task CommittedEventReadsBackWithItsStatus()
    {
        using var temp = new TempDatabase();
        var ct = TestContext.Current.CancellationToken;
        var time = new DateTimeOffset(2026, 11, 1, 14, 0, 0, TimeSpan.Zero);
        var ev = new SimEvent(1, time, EventTypes.DayStarted, null, null, new DayStartedPayload(new DateOnly(2026, 11, 1)), null, null, null, Origin.Test, null, null);
        var applied = new LoggedEvent(ev, EventStatus.Applied, 1, null);
        var skipped = new LoggedEvent(ev with { Seq = 2 }, EventStatus.Skipped, null, "古いバージョン");

        await using var database = await SqliteDatabase.OpenAsync(temp.FilePath, ct: ct);
        await using var storage = new SqliteStorage(database);
        await storage.CommitAsync(LoggedBatch.From([applied, skipped], EmptyState(new ApplyCursor(time, EventStage.DayTurn, 1, 1))), ct);

        var read = await storage.ReadEventsAsync(ct);

        Assert.Equal(2, read.Count);
        Assert.Equal(1, read[0].Event.Seq);
        Assert.Equal(EventStatus.Applied, read[0].Status);
        Assert.Equal(1, read[0].ApplyNo);
        Assert.Null(read[0].Reason);
        Assert.Equal(new DayStartedPayload(new DateOnly(2026, 11, 1)), read[0].Event.Payload);
        Assert.Equal(EventStatus.Skipped, read[1].Status);
        Assert.Null(read[1].ApplyNo);
        Assert.Equal("古いバージョン", read[1].Reason);
    }

    /// <summary>適用の記録がなければ、状態は読めない（null）。</summary>
    [Fact]
    public async Task LoadStateIsNullBeforeAnyAppliedEvent()
    {
        using var temp = new TempDatabase();
        var ct = TestContext.Current.CancellationToken;

        await using var database = await SqliteDatabase.OpenAsync(temp.FilePath, ct: ct);
        await using var storage = new SqliteStorage(database);

        Assert.Null(await storage.LoadStateAsync(ct));
    }

    /// <summary>読んだ状態のカーソルは、適用番号が最大のイベントの位置になる（時刻はオフセット 0 で戻る）。</summary>
    [Fact]
    public async Task LoadStateRestoresCursorOfLastAppliedEvent()
    {
        using var temp = new TempDatabase();
        var ct = TestContext.Current.CancellationToken;
        var time = new DateTimeOffset(2026, 11, 1, 23, 0, 0, TimeSpan.FromHours(9));
        var ev = new SimEvent(5, time, EventTypes.DayStarted, null, null, new DayStartedPayload(new DateOnly(2026, 11, 1)), null, null, null, Origin.Sim, null, null);

        await using (var database = await SqliteDatabase.OpenAsync(temp.FilePath, ct: ct))
        await using (var storage = new SqliteStorage(database))
        {
            await storage.CommitAsync(LoggedBatch.From([new LoggedEvent(ev, EventStatus.Applied, 3, null)], EmptyState(new ApplyCursor(time, EventStage.DayTurn, 5, 3))), ct);
        }

        await using var reopened = await SqliteDatabase.OpenAsync(temp.FilePath, ct: ct);
        await using var restored = new SqliteStorage(reopened);
        var state = await restored.LoadStateAsync(ct);

        Assert.NotNull(state);
        Assert.Equal(new ApplyCursor(time.ToUniversalTime(), EventStage.DayTurn, 5, 3), state.Cursor);
    }

    /// <summary>空の状態（ワールドもペルソナもない）を、適用の位置つきで作る。</summary>
    private static SimState EmptyState(ApplyCursor cursor) =>
        new(cursor, ImmutableDictionary<WorldId, WorldState>.Empty, ImmutableDictionary<PersonaId, PersonaState>.Empty);
}
