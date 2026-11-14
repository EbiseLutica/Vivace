using Microsoft.Extensions.Time.Testing;
using Vivace.Core.Time;

namespace Vivace.Tests.Time;

/// <summary>倍速の時計。実時間は FakeTimeProvider で進め、時刻の進みと待ちの完了を確かめる。</summary>
public class ScaledClockTests
{
    /// <summary>待ちの完了を確かめるときの上限の時間。</summary>
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(5);

    /// <summary>開始の時刻。</summary>
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);

    /// <summary>実時間の1分で、倍速の1時間が進む。</summary>
    [Fact]
    public void AdvancesAtSpeedTimesRealElapsed()
    {
        var time = new FakeTimeProvider(Start);
        var clock = new ScaledClock(Start, 60, time);

        Assert.Equal(Start, clock.UtcNow);

        time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(Start.AddHours(1), clock.UtcNow);
    }

    /// <summary>作った時点から経過を測る。作る前に進んだ実時間は、含まない。</summary>
    [Fact]
    public void ElapsedIsMeasuredFromConstruction()
    {
        var time = new FakeTimeProvider(Start);
        time.Advance(TimeSpan.FromHours(5));
        var clock = new ScaledClock(Start, 60, time);

        time.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(Start.AddMinutes(30), clock.UtcNow);
    }

    /// <summary>t までの待ちは、実時間で差を倍率で割った分（1時間なら60秒）だけ待ち、それまでは完了しない。</summary>
    [Fact]
    public async Task WaitCompletesAfterScaledDelayInRealTime()
    {
        var time = new FakeTimeProvider(Start);
        var clock = new ScaledClock(Start, 60, time);

        var wait = clock.WaitUntilAsync(Start.AddHours(1), TestContext.Current.CancellationToken).AsTask();
        time.Advance(TimeSpan.FromSeconds(59));
        Assert.False(wait.IsCompleted);

        time.Advance(TimeSpan.FromSeconds(1));
        await wait.WaitAsync(Bound, TestContext.Current.CancellationToken);
        Assert.Equal(Start.AddHours(1), clock.UtcNow);
    }

    /// <summary>残りが実時間の 1 ミリ秒に満たないときも、待ちは 1 ミリ秒以上になり、時計が進むまで完了しない（空回りしない）。</summary>
    [Fact]
    public async Task WaitWithSubMillisecondRemainderCompletesAfterOneMillisecond()
    {
        var time = new FakeTimeProvider(Start);
        var clock = new ScaledClock(Start, 60, time);
        var target = Start.AddTicks(30);

        var wait = clock.WaitUntilAsync(target, TestContext.Current.CancellationToken).AsTask();
        Assert.False(wait.IsCompleted);

        time.Advance(TimeSpan.FromMilliseconds(1));
        await wait.WaitAsync(Bound, TestContext.Current.CancellationToken);
        Assert.True(clock.UtcNow >= target);
    }

    /// <summary>t がいまと同じか、いまより前なら、待たずに完了する。</summary>
    [Fact]
    public async Task WaitAtOrBeforeNowCompletesImmediately()
    {
        var clock = new ScaledClock(Start, 60, new FakeTimeProvider(Start));

        await clock.WaitUntilAsync(Start, TestContext.Current.CancellationToken).AsTask().WaitAsync(Bound, TestContext.Current.CancellationToken);
        await clock.WaitUntilAsync(Start.AddHours(-1), TestContext.Current.CancellationToken).AsTask().WaitAsync(Bound, TestContext.Current.CancellationToken);
    }

    /// <summary>待っている最中にキャンセルされると、OperationCanceledException になる。</summary>
    [Fact]
    public async Task WaitHonoursCancellationWhileWaiting()
    {
        var clock = new ScaledClock(Start, 60, new FakeTimeProvider(Start));
        using var cancellation = new CancellationTokenSource();

        var wait = clock.WaitUntilAsync(Start.AddHours(1), cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait.WaitAsync(Bound, TestContext.Current.CancellationToken));
    }

    /// <summary>DateTimeOffset.MaxValue への待ちは、例外にならず、待ちに入る（キャンセルで抜ける）。</summary>
    [Fact]
    public async Task WaitUntilMaxValueWaitsWithoutThrowing()
    {
        var time = new FakeTimeProvider(Start);
        var clock = new ScaledClock(Start, 60, time);
        using var cancellation = new CancellationTokenSource();

        var wait = clock.WaitUntilAsync(DateTimeOffset.MaxValue, cancellation.Token).AsTask();
        time.Advance(TimeSpan.FromDays(2));
        Assert.False(wait.IsCompleted);

        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait.WaitAsync(Bound, TestContext.Current.CancellationToken));
    }

    /// <summary>時刻の計算が表せる範囲を超えても、例外にならず DateTimeOffset.MaxValue で頭打ちになる。</summary>
    [Fact]
    public void UtcNowClampsAtMaxValue()
    {
        var time = new FakeTimeProvider(Start);
        var clock = new ScaledClock(DateTimeOffset.MaxValue.AddHours(-1), 60, time);

        time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(DateTimeOffset.MaxValue, clock.UtcNow);

        time.Advance(TimeSpan.FromDays(10000));
        Assert.Equal(DateTimeOffset.MaxValue, clock.UtcNow);
    }

    /// <summary>倍率が 0 以下、NaN、無限大なら、ArgumentOutOfRangeException になる。</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidSpeedThrows(double speed)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScaledClock(Start, speed, new FakeTimeProvider(Start)));
    }
}
