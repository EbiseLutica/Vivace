using Microsoft.Extensions.Time.Testing;
using Vivace.Core;
using Vivace.Core.Events;
using Vivace.Core.Randomness;
using Vivace.Core.Time;
using Vivace.Testing;

namespace Vivace.Tests.Time;

/// <summary>時計の出す時刻は、ミリ秒の精度に切り捨てる（SQLite の列と JSON の時刻がそろうように）。</summary>
public class MillisecondTests
{
    /// <summary>開始の時刻（ミリ秒未満の 2345 ティック、つまり 234.5 マイクロ秒の端数を持つ）。</summary>
    private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero).AddTicks(12_345);

    /// <summary>実時間の時計は、TimeProvider の時刻のミリ秒未満を切り捨てて返す。</summary>
    [Fact]
    public void SystemClockTruncatesToMilliseconds()
    {
        var clock = new SystemClock(new FakeTimeProvider(Start));

        Assert.Equal(new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero).AddTicks(10_000), clock.UtcNow);
    }

    /// <summary>投入の時刻も、ミリ秒未満を切り捨ててから記録される（端数のある時刻を投入しても、記録の時刻はミリ秒の境界）。</summary>
    [Fact]
    public async Task InjectedTimeIsTruncatedToMilliseconds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var engine = SimEngine.Create(TestWorld.LoadOrThrow(), new InMemoryStorage(), new SplitMixRandomSource(), new VirtualClock(Scenario.Start), null, TestHolidays.Instance, null, [new NullChannel(Scenario.ChannelKind)]);
        var result = await engine.InjectAsync(InjectRequest.WorldCreated(TestWorld.DefId, Scenario.WorldSeed, Origin.Test, Scenario.Start.AddTicks(12_345)), ct);
        Assert.True(result.Accepted);

        await engine.RunUntilAsync(Scenario.Start.AddMinutes(1), ct);

        var logged = Assert.Single(engine.Log, entry => entry.Event.EventType == EventTypes.WorldCreated);
        Assert.Equal(Scenario.Start.AddTicks(10_000), logged.Event.Time);
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
