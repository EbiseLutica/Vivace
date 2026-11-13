using System.Collections.Immutable;
using Vivace.Core.Decision;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Planning;
using Vivace.Core.Randomness;
using Vivace.Core;
using Vivace.Tests.Support;

namespace Vivace.Tests.Rules;

/// <summary>
/// 行動の制限は、決めた時刻でなく、その場所に着く時刻で見る。
/// 睡眠の終わりで、起床の時刻（LastWake）を設定する。
/// </summary>
public class LimitAndWakeTests
{
    private static readonly PlaceId KaoriRoom = new("kaori_room");
    private static readonly PlaceId Park = new("park");

    /// <summary>
    /// 散歩は 6 時から 21 時まで。家から公園まで 14 分かかる。20 時 45 分に決めて、着くのは 20 時 59 分なので、制限の中。
    /// 20 時 55 分に決めると、着くのは 21 時 9 分で、制限の外。決めた時刻ではなく着く時刻で見る。
    /// </summary>
    [Fact]
    public void HoursLimitIsCheckedAtArrivalNotAtDecision()
    {
        var walk = Fixture.Context.Defs.Actions["walk_park"];
        var at2045 = Fixture.Now.AddMinutes(-135);
        var at2055 = Fixture.Now.AddMinutes(-125);
        var offset = Fixture.Context.Defs.Worlds[new WorldDefId("testville")].UtcOffset;

        Assert.True(ActionRules.WithinLimits(walk, null, at2045.AddMinutes(14), offset));
        Assert.False(ActionRules.WithinLimits(walk, null, at2055.AddMinutes(14), offset));
    }

    /// <summary>
    /// 20 時 55 分に決める決定では、散歩が候補に出ない（どの乱数の位置でも）。着く時刻が制限の外だから。
    /// </summary>
    [Fact]
    public void DecisionNeverChoosesActionWhoseArrivalIsOutsideHours()
    {
        var now = Fixture.Now.AddMinutes(-125);
        var plan = new DayPlan(
            1,
            now,
            Fixture.Now.AddHours(3),
            Fixture.Now.AddHours(3),
            PlanSource.Fallback,
            [new PlanBlock(null, BlockKind.Free, now, Fixture.Now.AddHours(3), null, null, PhonePolicy.Closed, null, [], PartnerAbsentPolicy.Continue)],
            null);
        var committed = Fixture.Apply(Fixture.WithKaori(), Fixture.Event(10, Fixture.Now, EventTypes.PlanCommitted, new PlanCommittedPayload(plan), Fixture.Kaori, new Guard([new PlanVersionCheck(0)], RejectAction.Drop))).State;
        var state = committed;

        for (ulong position = 0; position < 40; position++)
        {
            var random = new SplitMixRandomSource().Open(state.Worlds[Fixture.World].Seed, Fixture.Kaori.Value, "decide", position);
            var output = new UtilityDecider().Decide(new DecisionContext(state, Fixture.Kaori, now, DecisionReason.Free, null, Fixture.Context, random));

            Assert.NotEqual("walk_park", output.Payload.ActionId);
        }
    }

    /// <summary>睡眠の終わりで、起床の時刻が設定される。睡眠でない行動の終わりは、起床の時刻を変えない。</summary>
    [Fact]
    public void SleepEndSetsLastWakeAndOtherActionsDoNot()
    {
        var sleep = Fixture.Apply(Fixture.WithKaori(), Fixture.Event(10, Fixture.Now, EventTypes.ActionStarted, new ActionStartedPayload("core.sleep", KaoriRoom, TimeSpan.FromHours(5), ActivityReason.Chosen, 0, null), Fixture.Kaori)).State;
        var wake = Fixture.Apply(sleep, Fixture.Event(11, Fixture.Now.AddHours(5), EventTypes.ActionEnded, new ActionEndedPayload("core.sleep", 0), Fixture.Kaori));

        Assert.Equal(Fixture.Now.AddHours(5), wake.State.Personas[Fixture.Kaori].LastWake);

        var reading = Fixture.Apply(Fixture.WithKaori(), Fixture.Event(12, Fixture.Now, EventTypes.ActionStarted, new ActionStartedPayload("read_book", KaoriRoom, TimeSpan.FromMinutes(30), ActivityReason.Chosen, 0, null), Fixture.Kaori)).State;
        var readingEnd = Fixture.Apply(reading, Fixture.Event(13, Fixture.Now.AddMinutes(30), EventTypes.ActionEnded, new ActionEndedPayload("read_book", 0), Fixture.Kaori));

        Assert.Null(readingEnd.State.Personas[Fixture.Kaori].LastWake);
    }
}
