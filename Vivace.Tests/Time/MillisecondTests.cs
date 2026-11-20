using Microsoft.Extensions.Time.Testing;
using Vivace.Core.Time;

namespace Vivace.Tests.Time;

/// <summary>時計の出す時刻は、ミリ秒の精度に切り捨てる（SQLite の列と JSON の時刻がそろうように）。</summary>
public class MillisecondTests
{
    /// <summary>開始の時刻（ミリ秒未満の 4321 ティック、つまり 432.1 マイクロ秒の端数を持つ）。</summary>
    private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero).AddTicks(12_345);

    /// <summary>実時間の時計は、TimeProvider の時刻のミリ秒未満を切り捨てて返す。</summary>
    [Fact]
    public void SystemClockTruncatesToMilliseconds()
    {
        var clock = new SystemClock(new FakeTimeProvider(Start));

        Assert.Equal(new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero).AddTicks(10_000), clock.UtcNow);
    }

    /// <summary>倍速の時計は、進んだ先の時刻のミリ秒未満を切り捨てて返す（開始の端数も含め、ミリ秒の境界に乗る）。</summary>
    [Fact]
    public void ScaledClockTruncatesToMilliseconds()
    {
        var time = new FakeTimeProvider(Start);
        var clock = new ScaledClock(Start, 1, time);

        time.Advance(TimeSpan.FromTicks(25_678));

        Assert.Equal(new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero).AddTicks(30_000), clock.UtcNow);
    }
}
