using Vivace.Core.Engine;

namespace Vivace.Tests.Engine;

/// <summary>レーンは、同じレーンの仕事を順番に、違うレーンの仕事を並列に動かす（D1 1.5）。</summary>
public class LanesTests
{
    /// <summary>同じレーンの2つ目の仕事は、1つ目が終わるまで始まらない。別のレーンは、待たずに動く。</summary>
    [Fact]
    public async Task JobsInOneLaneRunInOrderAndOtherLanesRunAlongside()
    {
        var lanes = new Lanes();
        var release = new TaskCompletionSource();
        var events = new List<string>();
        var sync = new object();
        var secondStarted = new TaskCompletionSource();
        var otherLaneDone = new TaskCompletionSource();

        lanes.Post("decide:a", async () =>
        {
            lock (sync) events.Add("a1 start");
            await release.Task;
            lock (sync) events.Add("a1 end");
        });
        lanes.Post("decide:a", () =>
        {
            lock (sync) events.Add("a2 start");
            secondStarted.SetResult();
            return Task.CompletedTask;
        });
        lanes.Post("plan:a", () =>
        {
            lock (sync) events.Add("plan start");
            otherLaneDone.SetResult();
            return Task.CompletedTask;
        });

        await otherLaneDone.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(secondStarted.Task.IsCompleted);

        release.SetResult();
        await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        lanes.Complete();

        lock (sync)
        {
            Assert.True(events.IndexOf("a1 end") < events.IndexOf("a2 start"));
            Assert.True(events.IndexOf("plan start") < events.IndexOf("a1 end"));
        }
    }

    /// <summary>止めたレーンに仕事を入れると、黙って捨てず、例外にする。</summary>
    [Fact]
    public void PostingToCompletedLaneThrows()
    {
        var lanes = new Lanes();
        lanes.Post("decide:a", () => Task.CompletedTask);
        lanes.Complete();

        Assert.Throws<InvalidOperationException>(() => lanes.Post("decide:a", () => Task.CompletedTask));
    }
}
