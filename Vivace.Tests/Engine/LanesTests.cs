using Vivace.Core;

namespace Vivace.Tests.Engine;

/// <summary>レーンは、同じレーンのジョブを順番に、違うレーンのジョブを並列に動かす。ジョブが例外を投げたレーンは閉じ、終わりは待てる。</summary>
public class LanesTests
{
    /// <summary>同じレーンの2つ目のジョブは、1つ目が終わるまで始まらない。別のレーンは、待たずに動く。</summary>
    [Fact]
    public async Task JobsInOneLaneRunInOrderAndOtherLanesRunAlongside()
    {
        var lanes = new Lanes();
        var release = new TaskCompletionSource();
        var events = new List<string>();
        var sync = new Lock();
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
        await lanes.CompleteAsync();
        Assert.False(lanes.Failure.IsCompleted);

        lock (sync)
        {
            Assert.True(events.IndexOf("a1 end") < events.IndexOf("a2 start"));
            Assert.True(events.IndexOf("plan start") < events.IndexOf("a1 end"));
        }
    }

    /// <summary>止めたレーンにジョブを入れると、黙って捨てず、例外にする。</summary>
    [Fact]
    public async Task PostingToCompletedLaneThrows()
    {
        var lanes = new Lanes();
        lanes.Post("decide:a", () => Task.CompletedTask);
        await lanes.CompleteAsync();

        Assert.Throws<InvalidOperationException>(() => lanes.Post("decide:a", () => Task.CompletedTask));
    }

    /// <summary>終わらせた後は、まだないレーンへの投入も例外にし、新しいレーンを作らない。</summary>
    [Fact]
    public async Task PostingToNewLaneAfterCompletionThrows()
    {
        var lanes = new Lanes();
        await lanes.CompleteAsync();

        Assert.Throws<InvalidOperationException>(() => lanes.Post("plan:b", () => Task.CompletedTask));
    }

    /// <summary>ジョブが例外を投げたレーンは閉じ、後に積んだジョブを実行しない。終わりを待つと、その例外が出る。</summary>
    [Fact]
    public async Task FailingJobClosesLaneAndSurfacesOnCompletion()
    {
        var lanes = new Lanes();
        var release = new TaskCompletionSource();
        var failure = new InvalidOperationException("boom");
        var laterRan = false;

        lanes.Post("decide:a", async () =>
        {
            await release.Task;
            throw failure;
        });
        lanes.Post("decide:a", () =>
        {
            laterRan = true;
            return Task.CompletedTask;
        });
        release.SetResult();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(lanes.CompleteAsync);
        Assert.Same(failure, thrown);
        Assert.False(laterRan);

        var rejected = Assert.Throws<InvalidOperationException>(() => lanes.Post("decide:a", () => Task.CompletedTask));
        Assert.Same(failure, rejected.InnerException);
    }

    /// <summary>ジョブが例外を投げると、終わりを待つ前でも、そのレーンへの投入は失敗する。ほかのレーンは動き続ける。</summary>
    [Fact]
    public async Task FailedLaneRejectsPostsWhileOtherLanesKeepRunning()
    {
        var lanes = new Lanes();
        var failure = new InvalidOperationException("boom");
        var otherRan = new TaskCompletionSource();
        InvalidOperationException? rejected = null;

        lanes.Post("decide:a", () => throw failure);
        var closed = SpinWait.SpinUntil(() =>
        {
            try
            {
                lanes.Post("decide:a", () => Task.CompletedTask);
                return false;
            }
            catch (InvalidOperationException ex)
            {
                rejected = ex;
                return true;
            }
        }, TimeSpan.FromSeconds(5));

        Assert.True(closed);
        Assert.Same(failure, rejected!.InnerException);
        Assert.Same(failure, await lanes.Failure.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        lanes.Post("plan:b", () =>
        {
            otherRan.SetResult();
            return Task.CompletedTask;
        });
        await otherRan.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(lanes.CompleteAsync);
    }

    /// <summary>取り消しの例外で閉じたレーンも、その例外をそのまま出す。複数のレーンが閉じたら、キーの序数の順で最初のレーンの例外を出す。</summary>
    [Fact]
    public async Task CompletionSurfacesFirstFaultInKeyOrder()
    {
        var lanes = new Lanes();
        var canceled = new OperationCanceledException("a");
        var failed = new InvalidOperationException("b");

        lanes.Post("decide:b", () => throw failed);
        lanes.Post("decide:a", () => throw canceled);

        var thrown = await Assert.ThrowsAsync<OperationCanceledException>(lanes.CompleteAsync);
        Assert.Same(canceled, thrown);
    }

    /// <summary>終わりを待つ処理は、実行中のジョブと、その後に積んであるジョブが終わるまで完了しない。</summary>
    [Fact]
    public async Task CompletionWaitsForRunningJob()
    {
        var lanes = new Lanes();
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var finished = false;
        var queuedRan = false;

        lanes.Post("plan:a", async () =>
        {
            started.SetResult();
            await release.Task;
            finished = true;
        });
        lanes.Post("plan:a", () =>
        {
            queuedRan = true;
            return Task.CompletedTask;
        });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var completion = lanes.CompleteAsync();
        Assert.False(completion.IsCompleted);

        release.SetResult();
        await completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(finished);
        Assert.True(queuedRan);
    }
}
