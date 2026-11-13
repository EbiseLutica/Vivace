using Vivace.Core;
using Vivace.Testing;

namespace Vivace.Tests.Engine;

/// <summary>
/// 複数のシードで 28 日回しても、エンジンが止まらない（候補が空になって例外になる、などを防ぐ）。
/// 数百シードの掃引は、テストとは別に手で回す（時間がかかる）。
/// </summary>
public class SeedSmokeTests
{
    /// <summary>12 のシードで 28 日回しても、例外にならず、どの日にも適用が続く。</summary>
    [Fact]
    public async Task TwelveSeedsRunTwentyEightDaysWithoutStopping()
    {
        foreach (var seed in Enumerable.Range(1, 12))
        {
            await using var engine = await Scenario.RunAsync(28, (ulong)seed);

            var applied = engine.Log.Count(entry => entry.Status == EventStatus.Applied);
            Assert.True(applied > 28 * 10, $"シード {seed} で適用が少ない: {applied}");
        }
    }
}
