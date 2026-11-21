using Vivace.Core.Time;

namespace Vivace.Tests.Time;

/// <summary>差し替えのできる時計は、使っている時計の時刻と待ちに従い、替えたあとは新しい方だけを動かす。</summary>
public class SwitchableClockTests
{
    /// <summary>最初の時計の時刻。</summary>
    private static readonly DateTimeOffset First = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    /// <summary>替えたあとの時計の時刻。</summary>
    private static readonly DateTimeOffset Second = new(2026, 11, 3, 12, 30, 0, TimeSpan.Zero);

    /// <summary>時刻は、使っている時計のものを返す。替えると、以後は新しい時計の時刻になる。</summary>
    [Fact]
    public void UtcNowFollowsTheClockInUse()
    {
        var clock = new SwitchableClock(new VirtualClock(First));
        Assert.Equal(First, clock.UtcNow);

        clock.Replace(new VirtualClock(Second));

        Assert.Equal(Second, clock.UtcNow);
    }

    /// <summary>待ちは、使っている時計だけを進める。替えたあとの待ちは、前の時計に影響しない。</summary>
    [Fact]
    public async Task WaitUntilAdvancesOnlyTheClockInUse()
    {
        var ct = TestContext.Current.CancellationToken;
        var before = new VirtualClock(First);
        var after = new VirtualClock(First);
        var clock = new SwitchableClock(before);
        clock.Replace(after);

        await clock.WaitUntilAsync(Second, ct);

        Assert.Equal(Second, after.UtcNow);
        Assert.Equal(First, before.UtcNow);
    }

    /// <summary>Current は、いま使っている時計をそのまま返す。</summary>
    [Fact]
    public void CurrentReturnsTheReplacedClock()
    {
        var replacement = new VirtualClock(Second);
        var clock = new SwitchableClock(new VirtualClock(First));

        clock.Replace(replacement);

        Assert.Same(replacement, clock.Current);
    }
}
