using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;
using Vivace.Core.Storage;
using Vivace.Core;
using Vivace.Testing;

namespace Vivace.Tests.Engine;

/// <summary>適用したイベントの購読。適用の記録と同じ順に流れ、遅い購読はエンジンを止めずに切断される。</summary>
public class SubscribeTests
{
    /// <summary>購読に流れるのは適用したイベントだけで、順は適用の記録の Applied の順（連番と適用番号）と一致する。破棄で購読は終わる。</summary>
    [Fact]
    public async Task SubscriptionMatchesAppliedLogInOrder()
    {
        var engine = await Scenario.CreateAsync();
        var feed = engine.Subscribe(TestContext.Current.CancellationToken);
        var collect = Collect(feed);

        await engine.RunUntilAsync(Scenario.Start.AddDays(1), CancellationToken.None);
        await engine.DisposeAsync();
        var received = await collect.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        var applied = engine.Log.Where(entry => entry.Status == EventStatus.Applied).ToList();
        Assert.NotEmpty(applied);
        Assert.True(engine.Log.Count > applied.Count, "捨てたか却下したイベントがなく、Applied だけを流すことを確かめられない");
        Assert.Equal(
            applied.Select(entry => (entry.Event.Seq, entry.ApplyNo)),
            received.Select(entry => (entry.Event.Seq, entry.ApplyNo)));
        Assert.All(received, entry => Assert.Equal(EventStatus.Applied, entry.Status));
    }

    /// <summary>
    /// 読まない購読（容量 4）は、5 件目で切断される。それまでの 4 件は、ワールドの生成（連番 1）、
    /// その日の始まり（連番 4。段が先）、香織（2）、栞（3）。エンジンは最後まで回り、ほかの購読は全件を受け取る。
    /// </summary>
    [Fact]
    public async Task SlowSubscriberIsDisconnectedAndEngineKeepsRunning()
    {
        var engine = await Scenario.CreateAsync();
        var slow = engine.Subscribe(4, TestContext.Current.CancellationToken);
        var healthy = Collect(engine.Subscribe(TestContext.Current.CancellationToken));
        var until = Scenario.Start.AddDays(1);

        await engine.RunUntilAsync(until, CancellationToken.None);
        await engine.DisposeAsync();

        var applied = engine.Log.Where(entry => entry.Status == EventStatus.Applied).ToList();
        Assert.Contains(applied, entry => entry.Event.Payload is DayStartedPayload { Date.Day: 2 });
        Assert.Equal(applied.Count, (await healthy.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)).Count);

        var received = new List<LoggedEvent>();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var entry in slow)
            {
                received.Add(entry);
            }
        });
        Assert.Contains("購読が追いつかず", error.Message, StringComparison.Ordinal);
        Assert.Equal([1L, 4L, 2L, 3L], received.Select(entry => entry.Event.Seq));
        Assert.Equal([1L, 2L, 3L, 4L], received.Select(entry => entry.ApplyNo!.Value));
    }

    /// <summary>購読を最後まで読み、受け取ったものを返す。</summary>
    private static Task<List<LoggedEvent>> Collect(IAsyncEnumerable<LoggedEvent> feed) => Task.Run(async () =>
    {
        var received = new List<LoggedEvent>();
        await foreach (var entry in feed)
        {
            received.Add(entry);
        }

        return received;
    });
}
