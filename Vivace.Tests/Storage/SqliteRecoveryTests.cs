using System.Collections.Immutable;
using System.IO;
using Vivace.Core;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Storage;
using Vivace.Storage.Sqlite;

namespace Vivace.Tests.Storage;

/// <summary>未適用と処理中の行の書き込みと、起動のための読み出し（小さな例で）。</summary>
public class SqliteRecoveryTests
{
    /// <summary>積んだイベントは未適用の行として、適用の記録には出ず、起動のときの未適用の一覧に出る。</summary>
    [Fact]
    public async Task EnqueuedEventIsPendingUntilApplied()
    {
        using var temp = new TempDatabase();
        var ct = TestContext.Current.CancellationToken;

        await using var database = await SqliteDatabase.OpenAsync(temp.FilePath, ct: ct);
        var storage = new SqliteStorage(database);
        await storage.CommitAsync(new StorageBatch([DayStart(3)], [], EmptyState()), ct);

        Assert.Empty(await storage.ReadEventsAsync(ct));
        var recovered = await storage.LoadAsync(ct);
        Assert.Null(recovered.State);
        Assert.Equal(3L, recovered.MaxSeq);
        var open = Assert.Single(recovered.Open);
        Assert.Equal(EventRowStatus.Pending, open.Status);
        Assert.Equal(3L, open.Event.Seq);
        Assert.Equal(new DayStartedPayload(new DateOnly(2026, 11, 1)), open.Event.Payload);
    }

    /// <summary>
    /// 処理中の行は、適用番号を持って起動のときの一覧に出る。出力を取り込むと適用済みになり、適用番号は残る。
    /// </summary>
    [Fact]
    public async Task ProcessingRowKeepsItsApplyNoUntilTheOutputIsApplied()
    {
        using var temp = new TempDatabase();
        var ct = TestContext.Current.CancellationToken;

        await using var database = await SqliteDatabase.OpenAsync(temp.FilePath, ct: ct);
        var storage = new SqliteStorage(database);
        await storage.CommitAsync(new StorageBatch([DayStart(1)], [], EmptyState()), ct);
        await storage.CommitAsync(new StorageBatch([], [new EventRowUpdate(1, EventRowStatus.Processing, 1, null)], EmptyState()), ct);

        var processing = Assert.Single((await storage.LoadAsync(ct)).Open);
        Assert.Equal(EventRowStatus.Processing, processing.Status);
        Assert.Equal(1L, processing.ApplyNo);

        await storage.CommitAsync(new StorageBatch([], [new EventRowUpdate(1, EventRowStatus.Applied, null, null)], EmptyState()), ct);

        Assert.Empty((await storage.LoadAsync(ct)).Open);
        var logged = Assert.Single(await storage.ReadEventsAsync(ct));
        Assert.Equal(EventStatus.Applied, logged.Status);
        Assert.Equal(1L, logged.ApplyNo);
        await using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT applied_utc FROM events WHERE seq = 1";
        Assert.NotNull(await command.ExecuteScalarAsync(ct));
    }

    /// <summary>行の更新は、対象の行がなければ例外にする（書き漏れを隠さない）。</summary>
    [Fact]
    public async Task UpdatingAMissingRowFails()
    {
        using var temp = new TempDatabase();
        var ct = TestContext.Current.CancellationToken;

        await using var database = await SqliteDatabase.OpenAsync(temp.FilePath, ct: ct);
        var storage = new SqliteStorage(database);

        await Assert.ThrowsAsync<InvalidDataException>(() => storage.CommitAsync(new StorageBatch([], [new EventRowUpdate(9, EventRowStatus.Applied, 1, null)], EmptyState()), ct));
    }

    /// <summary>テスト用の日の始まりのイベント（ペルソナもワールドもなし）。</summary>
    /// <param name="seq">連番。</param>
    private static SimEvent DayStart(long seq) =>
        new(seq, new DateTimeOffset(2026, 11, 1, 14, 0, 0, TimeSpan.Zero), EventTypes.DayStarted, null, null, new DayStartedPayload(new DateOnly(2026, 11, 1)), null, null, null, Origin.Test, null, null);

    /// <summary>空の状態（ワールドもペルソナもない）。</summary>
    private static SimState EmptyState() =>
        new(new ApplyCursor(DateTimeOffset.UnixEpoch, EventStage.DayTurn, 0, 0), ImmutableDictionary<WorldId, WorldState>.Empty, ImmutableDictionary<PersonaId, PersonaState>.Empty);
}
