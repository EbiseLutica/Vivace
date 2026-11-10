using Vivace.Core.Events;
using Vivace.Core.Rules;
using Vivace.Tests.Support;

namespace Vivace.Tests.Rules;

/// <summary>イベントタイプから引くルールの表。段階2 で扱うタイプをすべて持ち、表にないタイプは例外になる。</summary>
public class RuleTableTests
{
    /// <summary>段階2 で扱うイベントタイプ。</summary>
    private static readonly string[] Stage2EventTypes =
    [
        "world.created",
        "day.started",
        "persona.created",
        "plan.committed",
        "plan.due",
        "block.started",
        "decision.requested",
        "activity.chosen",
        "travel.started",
        "travel.arrived",
        "action.started",
        "action.ended",
    ];

    /// <summary>表のキーは段階2 の12タイプちょうどで、余りも不足もない。</summary>
    [Fact]
    public void TableHasExactlyTheStage2EventTypes()
    {
        Assert.Equal(
            Stage2EventTypes.Order(StringComparer.Ordinal),
            EventApplier.Table.Keys.Order(StringComparer.Ordinal));
    }

    /// <summary>各ルールの EventType は、表でそのルールを引くキーと一致する。</summary>
    [Fact]
    public void EachRuleReportsTheKeyItIsListedUnder()
    {
        foreach (var (key, rule) in EventApplier.Table)
        {
            Assert.Equal(key, rule.EventType);
        }
    }

    /// <summary>表にないイベントタイプは、NotSupportedException になる。</summary>
    [Fact]
    public void UnlistedEventTypeThrowsNotSupported()
    {
        var ev = Fixture.Event(1, Fixture.Now, "incident.occurred", new DayStartedPayload(new DateOnly(2026, 11, 1)), null);

        var ex = Assert.Throws<NotSupportedException>(() => Fixture.Apply(Fixture.Empty(), ev));

        Assert.Equal("非対応のイベントタイプです: incident.occurred", ex.Message);
    }
}
