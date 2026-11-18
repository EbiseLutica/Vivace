using System.Collections.Immutable;
using Vivace.Core.Domain.State;
using Vivace.Core.Domain;
using Vivace.Core.Events.Guards;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;
using Vivace.Core;
using Vivace.Tests.Support;

namespace Vivace.Tests.Rules;

/// <summary>
/// スマホを開く機会（phone.opportunity）と、ブロックの開始からの機会の積み方。小さなプランで、手で数えた時刻を確かめる。
/// </summary>
public class PhoneOpportunityRulesTests
{
    private static readonly PlaceId KaoriRoom = new("kaori_room");
    private const string Block = "reading@2026-11-01";

    /// <summary>1時間の読書の固定ブロック（14:00 から 15:00、香織の部屋）。スマホの設定と間隔を指定する。</summary>
    private static DayPlan Plan(PhonePolicy phone, TimeSpan? every) => new(
        1,
        Fixture.Now,
        Fixture.Now.AddHours(24),
        Fixture.Now.AddHours(21),
        PlanSource.Fallback,
        [new PlanBlock(Block, BlockKind.Fixed, Fixture.Now, Fixture.Now.AddHours(1), "read_book", KaoriRoom, phone, every, [], PartnerAbsentPolicy.Continue)],
        null);

    /// <summary>プランを確定した香織の状態。</summary>
    private static SimState WithPlan(PhonePolicy phone, TimeSpan? every)
    {
        var committed = Fixture.Event(10, Fixture.Now, EventTypes.PlanCommitted, new PlanCommittedPayload(Plan(phone, every)), Fixture.Kaori, new Guard([new PlanVersionCheck(0)], RejectAction.Drop));
        return Fixture.Apply(Fixture.WithKaori(), committed).State;
    }

    /// <summary>スマホを開く機会のイベント（ブロックの ID つき）。</summary>
    private static SimEvent Opportunity(long seq, DateTimeOffset at) =>
        Fixture.Event(seq, at, EventTypes.PhoneOpportunity, new PhoneOpportunityPayload(Block), Fixture.Kaori);

    /// <summary>スマホを開けるブロックの機会は、同じ時刻に「スマホを開く機会」の決定タイミングを積む。間隔がなければ、次の機会は積まない。</summary>
    [Fact]
    public void OpenBlockRequestsPhoneDecisionAtSameTime()
    {
        var result = Fixture.Apply(WithPlan(PhonePolicy.Open, null), Opportunity(11, Fixture.Now));

        Assert.Equal(EventStatus.Applied, result.Status);
        var request = Assert.Single(result.FollowUps);
        Assert.Equal(EventTypes.DecisionRequested, request.EventType);
        Assert.Equal(new DecisionRequestedPayload(DecisionReason.Phone, Block), request.Payload);
        Assert.Equal(Fixture.Now, request.Time);
    }

    /// <summary>スマホを開けないブロックでは、機会を積まず、捨てる。</summary>
    [Fact]
    public void ClosedBlockGivesNoOpportunity()
    {
        var result = Fixture.Apply(WithPlan(PhonePolicy.Closed, null), Opportunity(11, Fixture.Now));

        Assert.Equal(EventStatus.Skipped, result.Status);
        Assert.Empty(result.FollowUps);
    }

    /// <summary>phone_every が 20 分なら、14:00 の機会のあと、14:20 に次の機会を積む（ブロックは 15:00 まで）。</summary>
    [Fact]
    public void PhoneEverySchedulesNextOpportunity()
    {
        var result = Fixture.Apply(WithPlan(PhonePolicy.Open, TimeSpan.FromMinutes(20)), Opportunity(11, Fixture.Now));

        Assert.Equal(2, result.FollowUps.Length);
        var next = result.FollowUps.Single(e => e.EventType == EventTypes.PhoneOpportunity);
        Assert.Equal(Fixture.Now.AddMinutes(20), next.Time);
        Assert.Equal(new PhoneOpportunityPayload(Block), next.Payload);
    }

    /// <summary>次の機会の時刻がブロックの終わりと同じか後なら積まない（14:40 のあと、次は 15:00 で、ブロックの終わりと同じ）。</summary>
    [Fact]
    public void PhoneEveryStopsAtBlockEnd()
    {
        var result = Fixture.Apply(WithPlan(PhonePolicy.Open, TimeSpan.FromMinutes(20)), Opportunity(11, Fixture.Now.AddMinutes(40)));

        var request = Assert.Single(result.FollowUps);
        Assert.Equal(EventTypes.DecisionRequested, request.EventType);
    }

    /// <summary>移動中の機会は捨てる。次の機会も積まない。</summary>
    [Fact]
    public void MovingPersonaSkipsOpportunity()
    {
        var travel = Fixture.Event(
            11,
            Fixture.Now,
            EventTypes.TravelStarted,
            new TravelStartedPayload(KaoriRoom, new PlaceId("home_living"), ImmutableArray<RouteStep>.Empty, Fixture.Now.AddMinutes(10), 0, null),
            Fixture.Kaori);
        var moving = Fixture.Apply(WithPlan(PhonePolicy.Open, TimeSpan.FromMinutes(20)), travel).State;

        var result = Fixture.Apply(moving, Opportunity(12, Fixture.Now));

        Assert.Equal(EventStatus.Skipped, result.Status);
        Assert.Empty(result.FollowUps);
    }

    /// <summary>ブロックが始まって行動を始めるとき、ブロックが開けるなら、同じ時刻に機会も積む。行動は段階 7、機会は段階 6 で先に適用される。</summary>
    [Fact]
    public void BlockStartWithOpenPhoneAlsoSchedulesOpportunity()
    {
        var start = Fixture.Event(11, Fixture.Now, EventTypes.BlockStarted, new BlockStartedPayload(Block, 1), Fixture.Kaori);

        var result = Fixture.Apply(WithPlan(PhonePolicy.Open, null), start);

        Assert.Equal(2, result.FollowUps.Length);
        var started = Assert.Single(result.FollowUps, e => e.EventType == EventTypes.ActionStarted);
        Assert.Equal(new ActionStartedPayload("read_book", KaoriRoom, TimeSpan.FromHours(1), ActivityReason.Block, 0, Block), started.Payload);
        var opportunity = Assert.Single(result.FollowUps, e => e.EventType == EventTypes.PhoneOpportunity);
        Assert.Equal(new PhoneOpportunityPayload(Block), opportunity.Payload);
        Assert.Equal(Fixture.Now, opportunity.Time);
    }

    /// <summary>スマホを開けないブロックの開始は、行動の開始だけを積む。</summary>
    [Fact]
    public void BlockStartWithClosedPhoneOnlyStartsAction()
    {
        var start = Fixture.Event(11, Fixture.Now, EventTypes.BlockStarted, new BlockStartedPayload(Block, 1), Fixture.Kaori);

        var result = Fixture.Apply(WithPlan(PhonePolicy.Closed, null), start);

        var only = Assert.Single(result.FollowUps);
        Assert.Equal(EventTypes.ActionStarted, only.EventType);
    }
}
