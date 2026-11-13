using Vivace.Core.Definitions;
using Vivace.Core.Events;
using Vivace.Core;
using Vivace.Testing;
using Vivace.Tests.Definitions;

namespace Vivace.Tests.Engine;

/// <summary>
/// 睡眠のブロックが始まれないと、Planner が起きず、同じ時刻の決めどきを積み続けて時刻が進まなくなる誤りの再発防止。
/// 定義は、睡眠（記憶の整理を起こす行動）の場所が、行動の条件（場所のタグ）を満たさない組み合わせ。読み込みは通る。
/// </summary>
public class StuckSleepRegressionTests
{
    /// <summary>睡眠が始まれなくても、2 日で時刻が進み、計画は作り直され、適用は上限の中に収まる。</summary>
    [Fact]
    public async Task UnstartableSleepDoesNotStopTimeOrLoop()
    {
        var files = DefinitionFixtures.Replaced("personas/kaori.toml", "action = \"core.sleep\"", "action = \"nap_club\"");
        files.Add(new DefinitionFile("actions/nap.toml", "[[action]]\nid = \"nap_club\"\ndisplay = \"仮眠\"\ndescription = \"クラブ室でだけ眠る。\"\nduration = \"2h\"\nphone = \"closed\"\nschedule_only = true\nrequires.place_tags = [\"club\"]\neffects.restore.energy = 50\neffects.sleep = { consolidate = true, debt = false }\n"));
        var definitions = DefinitionLoader.LoadFiles(files).Definitions ?? throw new InvalidOperationException("定義が読めない");
        var until = Scenario.Start.AddDays(2);

        await using var engine = await Scenario.CreateAsync(Scenario.WorldSeed, null, definitions);
        await engine.RunUntilAsync(until, CancellationToken.None);

        var applied = engine.Log.Where(entry => entry.Status == EventStatus.Applied).ToList();
        Assert.True(applied.Count < 20_000, $"適用が多すぎる（時刻が進んでいない可能性）: {applied.Count}");
        Assert.True(applied.Max(entry => entry.Event.Time) <= until);
        foreach (var persona in new[] { "kaori", "shiori" })
        {
            var plans = applied.Count(entry => entry.Event.EventType == EventTypes.PlanCommitted && entry.Event.PersonaId?.Value == persona);
            Assert.True(plans >= 3, $"{persona} の計画が作り直されていない: {plans}");
        }
    }
}
