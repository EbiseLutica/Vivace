using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;

namespace Vivace.Tests.Events;

/// <summary>イベントタイプのテーブルが、全タイプを漏れなく持つことを確かめる。</summary>
public class EventTypeTableTests
{
    /// <summary>ステージのテーブルとイベントタイプの全件（名前とステージ）。</summary>
    private static readonly (string Name, EventStage Stage)[] Expected =
    [
        ("day.started", EventStage.DayTurn),
        ("action.ended", EventStage.Ending),
        ("travel.arrived", EventStage.Ending),
        ("incident.resolved", EventStage.Ending),
        ("incident.occurred", EventStage.World),
        ("place.added", EventStage.World),
        ("place.adopted", EventStage.World),
        ("place.retired", EventStage.World),
        ("term.recorded", EventStage.World),
        ("vital.adjusted", EventStage.World),
        ("condition.attached", EventStage.World),
        ("condition.detached", EventStage.World),
        ("persona.created", EventStage.World),
        ("persona.destroyed", EventStage.World),
        ("persona.gap", EventStage.World),
        ("world.created", EventStage.World),
        ("world.destroyed", EventStage.World),
        ("definitions.reloaded", EventStage.World),
        ("vital.threshold", EventStage.Reaction),
        ("notification.arrived", EventStage.External),
        ("outbound.sent", EventStage.External),
        ("outbound.failed", EventStage.External),
        ("outbound.unknown", EventStage.External),
        ("plan.committed", EventStage.Schedule),
        ("plan.due", EventStage.Schedule),
        ("block.started", EventStage.Schedule),
        ("phone.opportunity", EventStage.Schedule),
        ("decision.requested", EventStage.Decision),
        ("activity.chosen", EventStage.Decision),
        ("block.responded", EventStage.Decision),
        ("travel.started", EventStage.Decision),
        ("action.started", EventStage.Decision),
        ("outbound.speech", EventStage.Decision),
        ("outbound.operation", EventStage.Decision),
        ("inbox.handled", EventStage.Decision),
        ("post.skipped", EventStage.Decision),
    ];

    /// <summary>テーブルは、全タイプを、正しいステージと一緒に過不足なく持つ。</summary>
    [Fact]
    public void TableHoldsEveryEventTypeWithItsStage()
    {
        var table = EventTypes.All.ToDictionary(info => info.Name, info => info.Stage);

        Assert.Equal(Expected.Length, EventTypes.All.Length);
        Assert.Equal(Expected.Length, table.Count);
        foreach (var (name, stage) in Expected)
        {
            Assert.True(table.ContainsKey(name), $"テーブルにイベントタイプがありません: {name}");
            Assert.Equal(stage, table[name]);
            Assert.Equal(stage, EventTypes.StageOf(name));
        }
    }

    /// <summary>すべてのイベントタイプに、ペイロードの型（EventPayload の派生）が付き、バージョンは 1 から始まる。</summary>
    [Fact]
    public void EveryEventTypeHasItsPayloadType()
    {
        foreach (var info in EventTypes.All)
        {
            Assert.True(typeof(EventPayload).IsAssignableFrom(info.PayloadType), $"ペイロードの型が EventPayload ではありません: {info.Name}");
            Assert.False(info.PayloadType.IsAbstract, $"ペイロードの型が抽象です: {info.Name}");
        }

        Assert.Equal(1, new DayStartedPayload(new DateOnly(2026, 11, 2)).V);
    }

    /// <summary>知らないイベントタイプの名前は、ステージを引けない。</summary>
    [Fact]
    public void UnknownEventTypeHasNoStage()
    {
        Assert.False(EventTypes.TryGet("nope.unknown", out _));
        Assert.Throws<ArgumentException>(() => EventTypes.StageOf("nope.unknown"));
    }
}
