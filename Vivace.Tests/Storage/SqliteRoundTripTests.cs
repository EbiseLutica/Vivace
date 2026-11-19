using Vivace.Core;
using Vivace.Core.Serialization;
using Vivace.Core.Storage;
using Vivace.Storage.Sqlite;
using Vivace.Testing;

namespace Vivace.Tests.Storage;

/// <summary>
/// テスト用の町を SQLite に書いて、開き直して読み戻すと、記録と状態が書く前と一致する（往復）。
/// </summary>
public class SqliteRoundTripTests
{
    /// <summary>シナリオの記録と最後の状態は、SQLite に書いて開き直しても同じ。</summary>
    [Fact]
    public async Task SevenDaysReadBackEqualToTheEngine()
    {
        using var temp = new TempDatabase();
        var ct = TestContext.Current.CancellationToken;
        string[] expectedEvents;
        string[] expectedApplied;
        string[] expectedState;

        await using (var database = await SqliteDatabase.OpenAsync(temp.FilePath, ct: ct))
        await using (var storage = new SqliteStorage(database))
        await using (var engine = await Scenario.CreateAsync(storage: storage, llm: ScriptedPosts()))
        {
            await engine.RunUntilAsync(Scenario.Start.AddDays(7), ct);
            expectedEvents = engine.Log.OrderBy(logged => logged.Event.Seq).Select(RoundTripDump.EventLine).ToArray();
            expectedApplied = engine.Log.Where(logged => logged.Status == EventStatus.Applied).Select(RoundTripDump.EventLine).ToArray();
            expectedState = RoundTripDump.StateLines(engine.State);
        }

        await using var reopened = await SqliteDatabase.OpenAsync(temp.FilePath, ct: ct);
        await using var restored = new SqliteStorage(reopened);
        var events = await restored.ReadEventsAsync(ct);
        var state = await restored.LoadStateAsync(ct);

        Assert.NotEmpty(expectedEvents);
        Assert.Equal(expectedEvents, events.Select(RoundTripDump.EventLine).ToArray());
        Assert.Equal(expectedApplied, events.Where(logged => logged.Status == EventStatus.Applied).OrderBy(logged => logged.ApplyNo).Select(RoundTripDump.EventLine).ToArray());
        Assert.NotNull(state);
        Assert.Equal(expectedState, RoundTripDump.StateLines(state));
    }

    /// <summary>知らないイベントタイプの行を読むと、例外で止まる（握りつぶさない）。</summary>
    [Fact]
    public async Task UnknownEventTypeInTheLogStopsTheRead()
    {
        using var temp = new TempDatabase();
        var ct = TestContext.Current.CancellationToken;

        await using (var database = await SqliteDatabase.OpenAsync(temp.FilePath, ct: ct))
        {
            await using var command = database.Connection.CreateCommand();
            command.CommandText = "INSERT INTO events (seq, time_utc, event_type, status, payload, origin, enqueued_utc) VALUES (1, 0, 'future.thing', 'applied', '{\"v\":1}', '{\"Kind\":0}', 0)";
            await command.ExecuteNonQueryAsync(ct);
        }

        await using var reopened = await SqliteDatabase.OpenAsync(temp.FilePath, ct: ct);
        await using var storage = new SqliteStorage(reopened);

        var error = await Assert.ThrowsAsync<UnknownEventTypeException>(() => storage.ReadEventsAsync(ct));
        Assert.Equal("future.thing", error.EventType);
    }

    /// <summary>テスト用のシナリオと同じ、投稿の文面を返す LLM。</summary>
    private static ScriptedLlm ScriptedPosts() => new ScriptedLlm().Reply("post.compose", "今日の授業眠い");
}
