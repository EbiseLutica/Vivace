using System.Collections.Immutable;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Signals;
using Vivace.Core;
using Vivace.Tests.Support;

namespace Vivace.Tests.Rules;

/// <summary>行動の開始と終了、移動、決めどき、行動の決定の Apply を、手で計算した小さな例で確かめる。</summary>
public class ActivityRulesTests
{
    private static readonly PlaceId KaoriRoom = new("kaori_room");
    private static readonly PlaceId HomeLiving = new("home_living");

    /// <summary>行動の開始のイベントを作る。</summary>
    private static SimEvent ActionStarted(long seq, string action, PlaceId place, TimeSpan duration, long version) =>
        Fixture.Event(seq, Fixture.Now, EventTypes.ActionStarted, new ActionStartedPayload(action, place, duration, ActivityReason.Chosen, version, null), Fixture.Kaori);

    /// <summary>決めどきのイベントを作る。</summary>
    private static SimEvent Request(long seq) =>
        Fixture.Event(seq, Fixture.Now, EventTypes.DecisionRequested, new DecisionRequestedPayload(DecisionReason.Free, null), Fixture.Kaori);

    /// <summary>決めどきの決定の中身（行動、場所、所要時間、乱数の範囲）を作る。移動は 0 分。</summary>
    private static ActivityChosenPayload Choice(string action, PlaceId place, int minutes, ulong from, ulong to) =>
        new(action, place, ImmutableArray<RouteStep>.Empty, 0, TimeSpan.FromMinutes(minutes), false, new RngDraws(Fixture.Kaori.Value, "decide", from, to));

    /// <summary>行動の開始で、率が行動の率に替わり、終了の時刻が積まれる。読書は気力 −2、退屈度は既定の +3 のまま。</summary>
    [Fact]
    public void ActionStartedSwitchesRatesAndSchedulesEnd()
    {
        var result = Fixture.Apply(Fixture.WithKaori(), ActionStarted(10, "read_book", KaoriRoom, TimeSpan.FromMinutes(30), 0));

        Assert.Equal(EventOutcome.Applied, result.Outcome);
        var kaori = result.State.Personas[Fixture.Kaori];
        Assert.Equal("read_book", kaori.Activity!.ActionId);
        Assert.Equal(-2.0, kaori.Vitals.Energy.Rate);
        Assert.Equal(3.0, kaori.Vitals.Boredom.Rate);
        var ended = Assert.Single(result.FollowUps);
        Assert.Equal(new ActionEndedPayload("read_book", 0), ended.Payload);
        Assert.Equal(Fixture.Now.AddMinutes(30), ended.Time);
    }

    /// <summary>行動の終わりで、終了時の増減（退屈度 −20）と、率の戻しが入る。30 分のあいだに退屈度は 1.5 上がっていた。</summary>
    [Fact]
    public void ActionEndedAppliesOnEndEffectsAndRestoresRates()
    {
        var started = Fixture.Apply(Fixture.WithKaori(), ActionStarted(10, "read_book", KaoriRoom, TimeSpan.FromMinutes(30), 0)).State;
        var ended = Fixture.Event(11, Fixture.Now.AddMinutes(30), EventTypes.ActionEnded, new ActionEndedPayload("read_book", 0), Fixture.Kaori);

        var result = Fixture.Apply(started, ended);

        Assert.Equal(EventOutcome.Applied, result.Outcome);
        var kaori = result.State.Personas[Fixture.Kaori];
        Assert.Null(kaori.Activity);
        Assert.Equal(11.5, kaori.Vitals.Boredom.BaseValue, 6);
        Assert.Equal(79, kaori.Vitals.Energy.BaseValue, 6);
        Assert.Equal(-4.0, kaori.Vitals.Energy.Rate);
        Assert.Empty(result.FollowUps);
    }

    /// <summary>版が古い行動の終わりは捨てる。行動の終わりでは版は上がらない。</summary>
    [Fact]
    public void StaleActionEndedIsSkippedAndEndDoesNotBumpVersion()
    {
        var started = Fixture.Apply(Fixture.WithKaori(), ActionStarted(10, "read_book", KaoriRoom, TimeSpan.FromMinutes(30), 0)).State;
        var stale = Fixture.Event(11, Fixture.Now.AddMinutes(30), EventTypes.ActionEnded, new ActionEndedPayload("read_book", 7), Fixture.Kaori);

        Assert.Equal(EventOutcome.Skipped, Fixture.Apply(started, stale).Outcome);

        var valid = Fixture.Event(12, Fixture.Now.AddMinutes(30), EventTypes.ActionEnded, new ActionEndedPayload("read_book", 0), Fixture.Kaori);
        Assert.Equal(0, Fixture.Apply(started, valid).State.Personas[Fixture.Kaori].ActivityVersion);
    }

    /// <summary>睡眠の終わりで、気力は 100 に戻り、眠った時間が目標（7時間）に足りなければ健康度が下がる。5時間なら 2 時間足りないので −8。</summary>
    [Fact]
    public void SleepEndRestoresEnergyAndChargesSleepDebt()
    {
        var start = Fixture.Event(10, Fixture.Now, EventTypes.ActionStarted, new ActionStartedPayload("core.sleep", KaoriRoom, TimeSpan.FromHours(5), ActivityReason.Chosen, 0, null), Fixture.Kaori);
        var asleep = Fixture.Apply(Fixture.WithKaori(), start).State;
        var end = Fixture.Event(11, Fixture.Now.AddHours(5), EventTypes.ActionEnded, new ActionEndedPayload("core.sleep", 0), Fixture.Kaori);

        var kaori = Fixture.Apply(asleep, end).State.Personas[Fixture.Kaori];

        Assert.Equal(100, kaori.Vitals.Energy.BaseValue, 6);
        Assert.Equal(92, kaori.Vitals.Health.BaseValue, 6);
    }

    /// <summary>睡眠が起こす記憶の整理（ここでは Planner の起動）は、同じ期間で一度だけ。期間が変わったときに起きる。</summary>
    [Fact]
    public void ConsolidatingSleepStartsPlannerOncePerPeriod()
    {
        var later = Fixture.Event(10, Fixture.Now.AddHours(1), EventTypes.ActionStarted, new ActionStartedPayload("core.sleep", KaoriRoom, TimeSpan.FromHours(5), ActivityReason.Chosen, 0, null), Fixture.Kaori);

        var result = Fixture.Apply(Fixture.WithKaori(), later);

        Assert.Equal(new StartPlannerSignal(10, Fixture.Kaori, Fixture.Now.AddHours(1)), Assert.Single(result.Signals));
        var sameTime = Fixture.Event(11, Fixture.Now, EventTypes.ActionStarted, new ActionStartedPayload("core.sleep", KaoriRoom, TimeSpan.FromHours(5), ActivityReason.Chosen, 0, null), Fixture.Kaori);
        Assert.Empty(Fixture.Apply(Fixture.WithKaori(), sameTime).Signals);
    }

    /// <summary>決めどきの要求は、決めどきの印を立て、Decider の起動を依頼する。決め中にもう一度要求すると捨てる。</summary>
    [Fact]
    public void DecisionRequestIsRecordedOnceAndStartsDecider()
    {
        var first = Fixture.Apply(Fixture.WithKaori(), Request(10));
        Assert.True(first.State.Personas[Fixture.Kaori].Deciding);
        Assert.Equal(new StartDeciderSignal(10, Fixture.Kaori, Fixture.Now, DecisionReason.Free, null), Assert.Single(first.Signals));

        var second = Fixture.Apply(first.State, Fixture.Event(11, Fixture.Now, EventTypes.DecisionRequested, new DecisionRequestedPayload(DecisionReason.Ended, null), Fixture.Kaori));
        Assert.Equal(EventOutcome.Skipped, second.Outcome);
        Assert.Empty(second.Signals);
    }

    /// <summary>決め直しの出力が guard で却下されると、決め中の印を下ろし、決め直しを積む。</summary>
    [Fact]
    public void StaleDecisionIsRejectedAndRequestedAgain()
    {
        var deciding = Fixture.Apply(Fixture.WithKaori(), Request(10)).State;
        var guard = new Guard([new ActivityVersionCheck(5)], RejectAction.Redecide);

        var result = Fixture.Apply(deciding, Fixture.Event(11, Fixture.Now, EventTypes.ActivityChosen, Choice("read_book", KaoriRoom, 30, 0, 3), Fixture.Kaori, guard, 10));

        Assert.Equal(EventOutcome.Rejected, result.Outcome);
        Assert.Equal("行動の版が違う", result.Reason);
        Assert.False(result.State.Personas[Fixture.Kaori].Deciding);
        Assert.Equal(new DecisionRequestedPayload(DecisionReason.Retry, null), Assert.Single(result.FollowUps).Payload);
    }

    /// <summary>乱数の位置が決めたときと違えば却下する。</summary>
    [Fact]
    public void RngCursorMismatchIsRejected()
    {
        var deciding = Fixture.Apply(Fixture.WithKaori(), Request(10)).State;
        var guard = new Guard([new RngCursorCheck(Fixture.Kaori.Value, "decide", 4)], RejectAction.Redecide);

        var result = Fixture.Apply(deciding, Fixture.Event(11, Fixture.Now, EventTypes.ActivityChosen, Choice("read_book", KaoriRoom, 30, 0, 3), Fixture.Kaori, guard, 10));

        Assert.Equal(EventOutcome.Rejected, result.Outcome);
        Assert.Equal("乱数の位置が違う", result.Reason);
    }

    /// <summary>行動の決定の適用。同じ場所なら、行動の開始を同じ時刻に積み、版と乱数の位置を進める。</summary>
    [Fact]
    public void ChosenActivityInPlaceStartsActionAndAdvancesRng()
    {
        var deciding = Fixture.Apply(Fixture.WithKaori(), Request(10)).State;
        var guard = new Guard(
            [new ActivityVersionCheck(0), new FeasibleCheck("read_book", KaoriRoom), new RngCursorCheck(Fixture.Kaori.Value, "decide", 0)],
            RejectAction.Redecide);

        var result = Fixture.Apply(deciding, Fixture.Event(11, Fixture.Now, EventTypes.ActivityChosen, Choice("read_book", KaoriRoom, 30, 0, 3), Fixture.Kaori, guard, 10));

        Assert.Equal(EventOutcome.Applied, result.Outcome);
        var kaori = result.State.Personas[Fixture.Kaori];
        Assert.False(kaori.Deciding);
        Assert.Equal(1, kaori.ActivityVersion);
        Assert.Equal(3UL, kaori.Rng["decide"]);
        Assert.Equal(new ActionStartedPayload("read_book", KaoriRoom, TimeSpan.FromMinutes(30), ActivityReason.Chosen, 1, null), Assert.Single(result.FollowUps).Payload);
    }

    /// <summary>場所が違えば、移動を先に積み、到着のあとに行動を始める。移動中の人は、どの場所にもいない。</summary>
    [Fact]
    public void ChosenActivityElsewhereStartsTravelThenAction()
    {
        var route = ImmutableArray.Create(new RouteStep(KaoriRoom, HomeLiving, 1));
        var deciding = Fixture.Apply(Fixture.WithKaori(), Request(10)).State;
        var guard = new Guard([new ActivityVersionCheck(0), new FeasibleCheck("watch_tv", HomeLiving), new RngCursorCheck(Fixture.Kaori.Value, "decide", 0)], RejectAction.Redecide);
        var chosen = new ActivityChosenPayload("watch_tv", HomeLiving, route, 1, TimeSpan.FromMinutes(35), false, new RngDraws(Fixture.Kaori.Value, "decide", 0, 0));

        var moved = Fixture.Apply(deciding, Fixture.Event(11, Fixture.Now, EventTypes.ActivityChosen, chosen, Fixture.Kaori, guard, 10));

        var travel = Assert.Single(moved.FollowUps);
        Assert.Equal(EventTypes.TravelStarted, travel.EventType);
        var travelPayload = Assert.IsType<TravelStartedPayload>(travel.Payload);
        Assert.Equal(Fixture.Now.AddMinutes(1), travelPayload.ArrivesAt);
        Assert.Equal(new PendingAction("watch_tv", HomeLiving, TimeSpan.FromMinutes(35), ActivityReason.Chosen, null), travelPayload.Next);

        var onWay = Fixture.Apply(moved.State, Fixture.Event(12, Fixture.Now, EventTypes.TravelStarted, travelPayload, Fixture.Kaori));
        Assert.Equal(EventOutcome.Applied, onWay.Outcome);
        Assert.IsType<Traveling>(onWay.State.Personas[Fixture.Kaori].Location);

        var arrived = Fixture.Apply(onWay.State, Fixture.Event(13, Fixture.Now.AddMinutes(1), EventTypes.TravelArrived, new TravelArrivedPayload(HomeLiving, 1), Fixture.Kaori));
        Assert.Equal(new AtPlace(HomeLiving), arrived.State.Personas[Fixture.Kaori].Location);
        Assert.Equal(new ActionStartedPayload("watch_tv", HomeLiving, TimeSpan.FromMinutes(35), ActivityReason.Chosen, 1, null), Assert.Single(arrived.FollowUps).Payload);
    }

    /// <summary>移動の開始は、版が古ければ捨てる。</summary>
    [Fact]
    public void StaleTravelIsSkipped()
    {
        var stale = Fixture.Event(10, Fixture.Now, EventTypes.TravelStarted, new TravelStartedPayload(KaoriRoom, HomeLiving, ImmutableArray<RouteStep>.Empty, Fixture.Now.AddMinutes(1), 9), Fixture.Kaori);

        Assert.Equal(EventOutcome.Skipped, Fixture.Apply(Fixture.WithKaori(), stale).Outcome);
    }

    /// <summary>夜の決定は、行動の実行の判定（時刻の範囲の制限）で却下する。散歩は 6 時から 21 時まで。基準の時刻は 23 時。</summary>
    [Fact]
    public void ActivityOutsideHoursIsRejectedByFeasibility()
    {
        var park = new PlaceId("park");
        var deciding = Fixture.Apply(Fixture.WithKaori(), Request(10)).State;
        var guard = new Guard([new FeasibleCheck("walk_park", park)], RejectAction.Redecide);

        var result = Fixture.Apply(deciding, Fixture.Event(11, Fixture.Now, EventTypes.ActivityChosen, Choice("walk_park", park, 30, 0, 0), Fixture.Kaori, guard, 10));

        Assert.Equal(EventOutcome.Rejected, result.Outcome);
        Assert.Equal("行動が実行できない", result.Reason);
    }
}
