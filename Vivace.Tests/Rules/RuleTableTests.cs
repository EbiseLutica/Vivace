using Vivace.Core.Events.Payloads;
using Vivace.Core;
using Vivace.Tests.Support;

namespace Vivace.Tests.Rules;

/// <summary>イベントタイプから引くルールのテーブル。段階2 で扱うタイプをすべて持ち、テーブルにないタイプは例外になる。</summary>
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

    /// <summary>テーブルのキーは段階2 の12タイプちょうどで、余りも不足もない。</summary>
    [Fact]
    public void TableHasExactlyTheStage2EventTypes()
    {
        Assert.Equal(
            Stage2EventTypes.Order(StringComparer.Ordinal),
            EventApplier.Table.Keys.Order(StringComparer.Ordinal));
    }

    /// <summary>各ルールの EventType は、テーブルでそのルールを引くキーと一致する。</summary>
    [Fact]
    public void EachRuleReportsTheKeyItIsListedUnder()
    {
        foreach (var (key, rule) in EventApplier.Table)
        {
            Assert.Equal(key, rule.EventType);
        }
    }

    /// <summary>テーブルにないイベントタイプは、NotSupportedException になる。</summary>
    [Fact]
    public void UnlistedEventTypeThrowsNotSupported()
    {
        var ev = Fixture.Event(1, Fixture.Now, "incident.occurred", new DayStartedPayload(new DateOnly(2026, 11, 1)), null);

        var ex = Assert.Throws<NotSupportedException>(() => Fixture.Apply(Fixture.Empty(), ev));

        Assert.Equal("非対応のイベントタイプです: incident.occurred", ex.Message);
    }
}
