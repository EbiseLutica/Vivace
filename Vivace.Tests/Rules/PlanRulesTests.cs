using System.Collections.Immutable;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Rules;
using Vivace.Tests.Support;

namespace Vivace.Tests.Rules;

/// <summary>プランの確定、ブロックの開始、Planner の起動の Apply を、手で作った小さなプランで確かめる。</summary>
public class PlanRulesTests
{
    private static readonly PlaceId KaoriRoom = new("kaori_room");
    private static readonly PlaceId HomeLiving = new("home_living");

    /// <summary>自由時間 1 時間、読書の固定ブロック 1 時間、自由時間 1 時間の小さなプラン。次の作り直しは 21 時間後。</summary>
    private static DayPlan SmallPlan(long version = 1) => new(
        version,
        Fixture.Now,
        Fixture.Now.AddHours(24),
        Fixture.Now.AddHours(21),
        PlanSource.Fallback,
        [
            new PlanBlock(null, BlockKind.Free, Fixture.Now, Fixture.Now.AddHours(1), null, null, PhonePolicy.Closed, null, [], PartnerAbsentPolicy.Continue),
            new PlanBlock("reading@2026-11-01", BlockKind.Fixed, Fixture.Now.AddHours(1), Fixture.Now.AddHours(2), "read_book", KaoriRoom, PhonePolicy.Closed, null, [], PartnerAbsentPolicy.Continue),
            new PlanBlock(null, BlockKind.Free, Fixture.Now.AddHours(2), Fixture.Now.AddHours(3), null, null, PhonePolicy.Closed, null, [], PartnerAbsentPolicy.Continue),
        ],
        null);

    private static SimState WithPlan()
    {
        var committed = Fixture.Event(10, Fixture.Now, EventTypes.PlanCommitted, new PlanCommittedPayload(SmallPlan()), Fixture.Kaori, new Guard([new PlanVersionCheck(0)], RejectAction.Drop));
        return Fixture.Apply(Fixture.WithKaori(), committed).State;
    }

    private static SimEvent BlockStarted(long seq, DateTimeOffset time, string? block, long version = 1) =>
        Fixture.Event(seq, time, EventTypes.BlockStarted, new BlockStartedPayload(block, version), Fixture.Kaori);

    /// <summary>プランの確定は、版を上げ、これから始まるブロックと、次の作り直しの時刻を積む。</summary>
    [Fact]
    public void PlanCommittedSchedulesBlockStartsAndNextRebuild()
    {
        var committed = Fixture.Event(10, Fixture.Now, EventTypes.PlanCommitted, new PlanCommittedPayload(SmallPlan()), Fixture.Kaori, new Guard([new PlanVersionCheck(0)], RejectAction.Drop));

        var result = Fixture.Apply(Fixture.WithKaori(), committed);

        Assert.Equal(EventOutcome.Applied, result.Outcome);
        Assert.Equal(1, result.State.Personas[Fixture.Kaori].PlanVersion);
        Assert.Equal(3, result.FollowUps.Count(e => e.EventType == EventTypes.BlockStarted));
        Assert.Equal(
            new BlockStartedPayload(null, 1),
            result.FollowUps.First(e => e.Time == Fixture.Now && e.EventType == EventTypes.BlockStarted).Payload);
        var due = Assert.Single(result.FollowUps, e => e.EventType == EventTypes.PlanDue);
        Assert.Equal(Fixture.Now.AddHours(21), due.Time);
    }

    /// <summary>古いプランの確定は、guard で却下する。状態は変わらない。</summary>
    [Fact]
    public void StalePlanIsRejected()
    {
        var committed = Fixture.Event(10, Fixture.Now, EventTypes.PlanCommitted, new PlanCommittedPayload(SmallPlan()), Fixture.Kaori, new Guard([new PlanVersionCheck(3)], RejectAction.Drop));

        var result = Fixture.Apply(Fixture.WithKaori(), committed);

        Assert.Equal(EventOutcome.Rejected, result.Outcome);
        Assert.Equal(0, result.State.Personas[Fixture.Kaori].PlanVersion);
        Assert.Empty(result.FollowUps);
    }

    /// <summary>自由時間のブロックの開始は、いま行動がなければ決めどき（自由時間）を積む。</summary>
    [Fact]
    public void FreeBlockStartRequestsDecisionWhenIdle()
    {
        var result = Fixture.Apply(WithPlan(), BlockStarted(11, Fixture.Now, null));

        Assert.Equal(new DecisionRequestedPayload(DecisionReason.Free, null), Assert.Single(result.FollowUps).Payload);
    }

    /// <summary>プランにないブロックの開始（ID がない、または時刻がずれている）は捨てる。版ではなく、いまのプランに入っているかで見る（J-135）。</summary>
    [Fact]
    public void UnknownOrMovedBlockStartIsSkipped()
    {
        var plan = WithPlan();

        Assert.Equal(EventOutcome.Skipped, Fixture.Apply(plan, BlockStarted(11, Fixture.Now.AddHours(1), "nothing@2026-11-01")).Outcome);
        Assert.Equal(EventOutcome.Skipped, Fixture.Apply(plan, BlockStarted(12, Fixture.Now.AddHours(5), "reading@2026-11-01")).Outcome);
    }

    /// <summary>新しいプランで続くブロックは、積み直さない。プランの確定が積むのは、新しいブロックと、いま続いているブロックだけ（J-135）。</summary>
    [Fact]
    public void PlanCommittedDoesNotReEmitContinuingBlocks()
    {
        var first = Fixture.Apply(Fixture.WithKaori(), Fixture.Event(10, Fixture.Now, EventTypes.PlanCommitted, new PlanCommittedPayload(SmallPlan()), Fixture.Kaori, new Guard([new PlanVersionCheck(0)], RejectAction.Drop)));
        var result = Fixture.Apply(first.State, Fixture.Event(11, Fixture.Now, EventTypes.PlanCommitted, new PlanCommittedPayload(SmallPlan()), Fixture.Kaori, new Guard([new PlanVersionCheck(1)], RejectAction.Drop)));

        Assert.Equal(EventOutcome.Applied, result.Outcome);
        Assert.Equal(2, result.State.Personas[Fixture.Kaori].PlanVersion);
        Assert.Empty(result.FollowUps.Where(e => e.EventType == EventTypes.BlockStarted));
    }

    /// <summary>固定ブロックは、その場所で行動を始める（同じ場所なので、移動はない）。同じブロックは二度始めない。</summary>
    [Fact]
    public void FixedBlockStartsActionInPlaceOnce()
    {
        var plan = WithPlan();
        var start = BlockStarted(11, Fixture.Now.AddHours(1), "reading@2026-11-01");

        var result = Fixture.Apply(plan, start);

        Assert.Equal(new ActionStartedPayload("read_book", KaoriRoom, TimeSpan.FromHours(1), ActivityReason.Block, 0, "reading@2026-11-01"), Assert.Single(result.FollowUps).Payload);
        Assert.Equal("reading@2026-11-01", result.State.Personas[Fixture.Kaori].LastBlockId);
        Assert.Equal(EventOutcome.Skipped, Fixture.Apply(result.State, start).Outcome);
    }

    /// <summary>ブロックの場所が違えば、移動を先に積み、到着のあとに行動を始める。残りの時間は、移動のあとのブロックの終わりまで。</summary>
    [Fact]
    public void FixedBlockElsewhereTravelsFirst()
    {
        var plan = new DayPlan(
            1,
            Fixture.Now,
            Fixture.Now.AddHours(24),
            Fixture.Now.AddHours(21),
            PlanSource.Fallback,
            [new PlanBlock("tv@2026-11-01", BlockKind.Fixed, Fixture.Now, Fixture.Now.AddHours(1), "watch_tv", HomeLiving, PhonePolicy.Open, null, [], PartnerAbsentPolicy.Continue)],
            null);
        var committed = Fixture.Event(10, Fixture.Now, EventTypes.PlanCommitted, new PlanCommittedPayload(plan), Fixture.Kaori, new Guard([new PlanVersionCheck(0)], RejectAction.Drop));
        var state = Fixture.Apply(Fixture.WithKaori(), committed).State;

        var result = Fixture.Apply(state, BlockStarted(11, Fixture.Now, "tv@2026-11-01"));

        var travel = Assert.IsType<TravelStartedPayload>(Assert.Single(result.FollowUps).Payload);
        Assert.Equal(Fixture.Now.AddMinutes(1), travel.ArrivesAt);
        Assert.Equal(new PendingAction("watch_tv", HomeLiving, TimeSpan.FromHours(1).Subtract(TimeSpan.FromMinutes(1)), ActivityReason.Block, "tv@2026-11-01"), travel.Next);
    }

    /// <summary>固定ブロックの開始は、いまの行動を打ち切る。行動の版が上がり、ハンドラの取り消しを依頼する（D1 1.3、P-03）。</summary>
    [Fact]
    public void BlockStartCutsRunningActivity()
    {
        var withActivity = Fixture.Apply(Fixture.WithKaori(), Fixture.Event(12, Fixture.Now, EventTypes.ActionStarted, new ActionStartedPayload("read_book", KaoriRoom, TimeSpan.FromHours(2), ActivityReason.Chosen, 0, null), Fixture.Kaori)).State;
        var planned = Fixture.Apply(withActivity, Fixture.Event(13, Fixture.Now, EventTypes.PlanCommitted, new PlanCommittedPayload(SmallPlan()), Fixture.Kaori, new Guard([new PlanVersionCheck(0)], RejectAction.Drop))).State;

        var result = Fixture.Apply(planned, BlockStarted(14, Fixture.Now.AddHours(1), "reading@2026-11-01"));

        Assert.Equal(EventOutcome.Applied, result.Outcome);
        Assert.Equal(new CancelHandler(Fixture.Kaori), Assert.Single(result.Signals));
        Assert.Equal(1, result.State.Personas[Fixture.Kaori].ActivityVersion);
        Assert.Null(result.State.Personas[Fixture.Kaori].Activity);
        Assert.Equal(1, Assert.IsType<ActionStartedPayload>(Assert.Single(result.FollowUps).Payload).ActivityVersion);
    }

    /// <summary>行動の計画の期間ごとに、Planner は一度だけ動く。プランの作り直しの時刻が来ても、同じ時刻で動いていれば何もしない。</summary>
    [Fact]
    public void PlanDueStartsPlannerOnceAndDropsStalePlan()
    {
        var plan = WithPlan();
        var due = Fixture.Event(11, Fixture.Now.AddHours(21), EventTypes.PlanDue, new PlanDuePayload(1), Fixture.Kaori);

        var first = Fixture.Apply(plan, due);
        Assert.Equal(new StartPlanner(11, Fixture.Kaori, Fixture.Now.AddHours(21)), Assert.Single(first.Signals));

        Assert.Equal(EventOutcome.Skipped, Fixture.Apply(first.State, due).Outcome);
        Assert.Equal(EventOutcome.Skipped, Fixture.Apply(plan, Fixture.Event(12, Fixture.Now.AddHours(21), EventTypes.PlanDue, new PlanDuePayload(0), Fixture.Kaori)).Outcome);
    }

    /// <summary>規則にない型は、黙って捨てず、例外で止める（段階2 の範囲外）。</summary>
    [Fact]
    public void UnsupportedEventTypeThrows()
    {
        var ev = Fixture.Event(10, Fixture.Now, EventTypes.PhoneOpportunity, new PhoneOpportunityPayload("reading@2026-11-01"), Fixture.Kaori);

        Assert.Throws<NotSupportedException>(() => Fixture.Apply(Fixture.WithKaori(), ev));
    }
}
