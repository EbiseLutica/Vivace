using Microsoft.Extensions.Time.Testing;
using Vivace.Core.Time;

namespace Vivace.Tests.Time;

/// <summary>実時間の時計。時刻と待ちを FakeTimeProvider で進め、いつ完了するかを確かめる。</summary>
public class SystemClockTests
{
    /// <summary>待ちの完了を確かめるときの上限の時間。</summary>
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(5);

    /// <summary>いまの UTC の時刻は、TimeProvider の時刻をそのまま返す。</summary>
    [Fact]
    public void UtcNowReadsTimeProvider()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero));
        var clock = new SystemClock(time);

        Assert.Equal(time.GetUtcNow(), clock.UtcNow);

        time.Advance(TimeSpan.FromMinutes(7));
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 0, 7, 0, TimeSpan.Zero), clock.UtcNow);
    }

    /// <summary>t がいまと同じか、いまより前なら、待たずに完了する。</summary>
    [Fact]
    public async Task WaitAtOrBeforeNowCompletesImmediately()
    {
        var now = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);
        var clock = new SystemClock(new FakeTimeProvider(now));

        await clock.WaitUntilAsync(now, TestContext.Current.CancellationToken).AsTask().WaitAsync(Bound, TestContext.Current.CancellationToken);
        await clock.WaitUntilAsync(now.AddHours(-1), TestContext.Current.CancellationToken).AsTask().WaitAsync(Bound, TestContext.Current.CancellationToken);
    }

    /// <summary>t まで時刻が進むまでは完了せず、t に達すると完了する。</summary>
    [Fact]
    public async Task WaitCompletesOnlyWhenTimeIsReached()
    {
        var now = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(now);
        var clock = new SystemClock(time);

        var wait = clock.WaitUntilAsync(now.AddSeconds(10), TestContext.Current.CancellationToken).AsTask();
        time.Advance(TimeSpan.FromSeconds(9));
        Assert.False(wait.IsCompleted);

        time.Advance(TimeSpan.FromSeconds(1));
        await wait.WaitAsync(Bound, TestContext.Current.CancellationToken);
        Assert.Equal(now.AddSeconds(10), clock.UtcNow);
    }

    /// <summary>待っている最中にキャンセルされると、OperationCanceledException になる。</summary>
    [Fact]
    public async Task WaitHonoursCancellationWhileWaiting()
    {
        var now = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);
        var clock = new SystemClock(new FakeTimeProvider(now));
        using var cancellation = new CancellationTokenSource();

        var wait = clock.WaitUntilAsync(now.AddHours(1), cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait.WaitAsync(Bound, TestContext.Current.CancellationToken));
    }

    /// <summary>すでにキャンセルされていれば、待たずに OperationCanceledException になる。</summary>
    [Fact]
    public async Task WaitThrowsWhenAlreadyCancelled()
    {
        var now = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);
        var clock = new SystemClock(new FakeTimeProvider(now));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => clock.WaitUntilAsync(now.AddHours(1), cancellation.Token).AsTask());
    }

    /// <summary>DateTimeOffset.MaxValue への待ちは、例外にならず、待ちに入る（キャンセルで抜ける）。</summary>
    [Fact]
    public async Task WaitUntilMaxValueWaitsWithoutThrowing()
    {
        var now = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(now);
        var clock = new SystemClock(time);
        using var cancellation = new CancellationTokenSource();

        var wait = clock.WaitUntilAsync(DateTimeOffset.MaxValue, cancellation.Token).AsTask();
        Assert.False(wait.IsCompleted);

        time.Advance(TimeSpan.FromDays(2));
        Assert.False(wait.IsCompleted);

        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait.WaitAsync(Bound, TestContext.Current.CancellationToken));
    }

    /// <summary>TimeProvider を省略（null）すると、TimeProvider.System の時刻を読む。</summary>
    [Fact]
    public void OmittedTimeProviderUsesSystemTime()
    {
        var before = DateTimeOffset.UtcNow;
        var clock = new SystemClock(null);

        Assert.InRange(clock.UtcNow, before, DateTimeOffset.UtcNow);
    }
}
