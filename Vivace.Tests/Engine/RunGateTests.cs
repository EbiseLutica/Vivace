using Vivace.Testing;
using Vivace.Tests.Support;

namespace Vivace.Tests.Engine;

/// <summary>RunUntilAsync の実行の排他（同時に2つは回さない）。</summary>
public class RunGateTests
{
    /// <summary>待ちの完了を確かめるときの上限の時間。</summary>
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    /// <summary>実行中にもう1つ RunUntilAsync を呼ぶと、実行中の例外になり、最初の実行は続く。</summary>
    [Fact]
    public async Task SecondRunUntilWhileRunningIsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new GatedClock(Scenario.Start, Scenario.Start.AddMinutes(30));
        await using var engine = await Scenario.CreateAsync(clock: clock);
        var first = engine.RunUntilAsync(Scenario.Start.AddDays(2), ct);
        await clock.Entered.Task.WaitAsync(Bound, ct);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.RunUntilAsync(Scenario.Start.AddDays(2), ct));

        Assert.Contains("すでに実行中", error.Message, StringComparison.Ordinal);
        Assert.False(first.IsCompleted);
        clock.Release.TrySetResult();
        await first.WaitAsync(Bound, ct);
    }
}
