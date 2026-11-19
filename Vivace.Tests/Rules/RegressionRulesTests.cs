using Vivace.Core.Definitions;
using Vivace.Core.Domain.State;
using Vivace.Core.Domain;
using Vivace.Core.Events.Guards;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;
using Vivace.Core.Routes;
using Vivace.Core.Signals;
using Vivace.Core;
using Vivace.Tests.Definitions;
using Vivace.Tests.Support;
using Vivace.Testing;

namespace Vivace.Tests.Rules;

/// <summary>睡眠の率、次の行動の候補、繰り返しの減衰、状態変数のバージョンの再発防止。</summary>
public class RegressionRulesTests
{
    private static readonly PlaceId TaroRoom = new("taro_room");

    /// <summary>睡眠の開始が Planner を起こすとき、睡眠の率（気力 0）が残る。期間のキーは、睡眠のブロック ID で持つ。</summary>
    [Fact]
    public void ConsolidatingSleepKeepsSleepRatesAndPeriodKey()
    {
        var start = Fixture.Event(
            10,
            Fixture.Now.AddHours(1),
            EventTypes.ActionStarted,
            new ActionStartedPayload("core.sleep", TaroRoom, TimeSpan.FromHours(5), ActivityReason.Block, 0, "sleep_weekday@2026-11-01"),
            Fixture.Taro);

        var result = Fixture.Apply(Fixture.WithTaro(), start);

        var taro = result.State.Personas[Fixture.Taro];
        Assert.Equal(0.0, taro.Vitals.Energy.Rate);
        Assert.Equal(0, taro.ActivityVersion);
        Assert.Equal("sleep_weekday@2026-11-01", taro.PlannedFor);
        Assert.Equal(new StartPlannerSignal(10, Fixture.Taro, Fixture.Now.AddHours(1)), Assert.Single(result.Signals));
    }

    /// <summary>
    /// 繰り返しの減衰（repeat_decay）は、退屈度の減少に掛かる。2 回目は 0.6 倍。
    /// 退屈度 90 から、1 回目（30 分、減少 −25）で 66.5、2 回目（30 分、減少 −25 × 0.6 = −15）で 53。
    /// </summary>
    [Fact]
    public void RepeatDecayShrinksBoredomReliefOfRepeatedAction()
    {
        var state = Fixture.WithTaro();
        var taro = state.Personas[Fixture.Taro] with
        {
            Vitals = new Vitals(new VitalTrack(Fixture.Now, 80, 0), new VitalTrack(Fixture.Now, 100, 0), new VitalTrack(Fixture.Now, 90, 0), new VitalTrack(Fixture.Now, 0, 0), 1),
        };
        state = Fixture.With(state, taro);
        var half = Fixture.Now.AddMinutes(30);
        var end = Fixture.Now.AddMinutes(60);

        var first = Fixture.Apply(state, Fixture.Event(10, Fixture.Now, EventTypes.ActionStarted, new ActionStartedPayload("walk_park", TaroRoom, TimeSpan.FromMinutes(30), ActivityReason.Chosen, 0, null), Fixture.Taro)).State;
        var firstEnd = Fixture.Apply(first, Fixture.Event(11, half, EventTypes.ActionEnded, new ActionEndedPayload("walk_park", 0), Fixture.Taro)).State;
        Assert.Equal(66.5, firstEnd.Personas[Fixture.Taro].Vitals.Boredom.BaseValue, 6);

        var second = Fixture.Apply(firstEnd, Fixture.Event(12, half, EventTypes.ActionStarted, new ActionStartedPayload("walk_park", TaroRoom, TimeSpan.FromMinutes(30), ActivityReason.Chosen, 0, null), Fixture.Taro)).State;
        var secondEnd = Fixture.Apply(second, Fixture.Event(13, end, EventTypes.ActionEnded, new ActionEndedPayload("walk_park", 0), Fixture.Taro));

        Assert.Equal(EventStatus.Applied, secondEnd.Status);
        Assert.Equal(53.0, secondEnd.State.Personas[Fixture.Taro].Vitals.Boredom.BaseValue, 6);
    }

    /// <summary>
    /// 次の予定の場所へ向かう行動は、使える行動の一覧に書かなくても、guard で却下されない（書いていないと却下され、同じ時刻で止まっていた）。
    /// </summary>
    [Fact]
    public void GoToNextIsFeasibleWhenUseListOmitsCore()
    {
        var files = DefinitionFixtures.Replaced("personas/taro.toml", "\"core.*\", ", string.Empty);
        var definitions = DefinitionLoader.LoadFiles(files).Definitions!;
        var context = new RuleContext(definitions, RouteCache.Create(definitions));
        var state = Fixture.WithWorld();
        state = new EventApplier(context).Apply(state, Fixture.Event(2, Fixture.Now, EventTypes.PersonaCreated, new PersonaCreatedPayload(new PersonaDefId("taro"), TaroRoom), null)).State;

        var taro = state.Personas[Fixture.Taro];

        Assert.True(ActionRules.CanChoose(context, state, taro, ActionRules.GoToNextId, TaroRoom, Fixture.Now));
        Assert.False(ActionRules.CanChoose(context, state, taro, "attend_class", TaroRoom, Fixture.Now));
    }

    /// <summary>
    /// 作り直しの時刻と睡眠の開始が同じなら、プランの作り直しの予定は動かず、睡眠の開始が Planner を一度だけ起こす。
    /// </summary>
    [Fact]
    public void PlanDueYieldsToSleepStartingAtTheSameTime()
    {
        var sleepAt = Fixture.Now.AddHours(1);
        var plan = new DayPlan(
            1,
            Fixture.Now,
            Fixture.Now.AddHours(4),
            sleepAt,
            PlanSource.Fallback,
            [new PlanBlock("sleep@2026-11-01", BlockKind.Fixed, sleepAt, sleepAt.AddHours(6), "core.sleep", TaroRoom, PhonePolicy.Closed, null, [], PartnerAbsentPolicy.Continue)],
            null);
        var state = Fixture.Apply(Fixture.WithTaro(), Fixture.Event(10, Fixture.Now, EventTypes.PlanCommitted, new PlanCommittedPayload(plan), Fixture.Taro, new Guard([new PlanVersionCheck(0)], RejectAction.Drop))).State;

        var due = Fixture.Apply(state, Fixture.Event(11, sleepAt, EventTypes.PlanDue, new PlanDuePayload(1), Fixture.Taro));
        Assert.Equal(EventStatus.Skipped, due.Status);

        var sleep = Fixture.Apply(state, Fixture.Event(12, sleepAt, EventTypes.ActionStarted, new ActionStartedPayload("core.sleep", TaroRoom, TimeSpan.FromHours(6), ActivityReason.Block, 0, "sleep@2026-11-01"), Fixture.Taro));
        Assert.Equal(new StartPlannerSignal(12, Fixture.Taro, sleepAt), Assert.Single(sleep.Signals));
    }

    /// <summary>状態変数を急に変えても、バージョンが上がる。古い予測を捨てるため。</summary>
    [Fact]
    public void AbruptVitalChangeBumpsVersion()
    {
        var definitions = TestWorld.LoadOrThrow();
        var taro = Fixture.WithTaro().Personas[Fixture.Taro];

        var changed = VitalRules.Change(taro, VitalName.Boredom, Fixture.Now, definitions, -5, null);

        Assert.Equal(taro.Vitals.Version + 1, changed.Vitals.Version);
    }
}
