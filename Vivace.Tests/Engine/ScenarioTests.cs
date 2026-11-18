using Vivace.Core.Events;
using Vivace.Core;
using Vivace.Tests.Support;
using Vivace.Testing;

namespace Vivace.Tests.Engine;

/// <summary>
/// シナリオ（テスト用の町で、2人のペルソナを仮想時計で7日回す）。不変条件はエンジンが全適用で検査するので、
/// 途中で破れれば、ここで例外になる。
/// </summary>
public class ScenarioTests
{
    /// <summary>7日分の事実のイベントの列が、保存された期待値と一致する。</summary>
    [Fact]
    public async Task SevenDaysMatchSnapshot()
    {
        await using var engine = await Scenario.RunAsync(7);

        await Snapshot.Text(EventText.Lines(engine.Log));
    }

    /// <summary>同じシードで2回回すと、事実のイベントの列が完全に一致する（決定性）。</summary>
    [Fact]
    public async Task SameSeedGivesIdenticalEventsTwice()
    {
        await using var first = await Scenario.RunAsync(7);
        await using var second = await Scenario.RunAsync(7);

        Assert.Equal(EventText.Lines(first.Log), EventText.Lines(second.Log));
    }

    /// <summary>適用されたイベントは、扱うタイプだけ。</summary>
    [Fact]
    public async Task OnlyPhaseTwoEventTypesAreApplied()
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            EventTypes.WorldCreated,
            EventTypes.PersonaCreated,
            EventTypes.DayStarted,
            EventTypes.PlanCommitted,
            EventTypes.PlanDue,
            EventTypes.BlockStarted,
            EventTypes.PhoneOpportunity,
            EventTypes.DecisionRequested,
            EventTypes.ActivityChosen,
            EventTypes.TravelStarted,
            EventTypes.TravelArrived,
            EventTypes.ActionStarted,
            EventTypes.ActionEnded,
        };
        await using var engine = await Scenario.RunAsync(2);

        var types = engine.Log.Where(entry => entry.Status == EventStatus.Applied).Select(entry => entry.Event.EventType).Distinct().ToList();

        Assert.All(types, type => Assert.Contains(type, allowed));
    }
}
