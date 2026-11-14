using Vivace.Core.Decision;
using Vivace.Core.Domain.State;
using Vivace.Core.Domain;
using Vivace.Core.Events.Guards;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;
using Vivace.Core.Planning;
using Vivace.Core.Randomness;
using Vivace.Core;
using Vivace.Tests.Support;

namespace Vivace.Tests.Domain;

/// <summary>
/// 定義は、インスタンスの ID の文字からでなく、インスタンス状態の DefId から引く。
/// ワールドのインスタンスの ID を定義 ID と違うものにして、定義の引きがそれを使っていないことを確かめる。
/// </summary>
public class DefinitionLookupTests
{
    private static readonly PlaceId KaoriRoom = new("kaori_room");

    /// <summary>
    /// 香織のワールドのインスタンスの ID を "testville#2" に差し替えた状態（定義 ID は testville のまま）。
    /// </summary>
    private static SimState WithRenamedWorldInstance(SimState state)
    {
        var renamed = new WorldId("testville#2");
        var world = state.Worlds[Fixture.World] with { Id = renamed };
        var kaori = state.Personas[Fixture.Kaori] with { World = renamed };
        return state with
        {
            Worlds = state.Worlds.Remove(Fixture.World).Add(renamed, world),
            Personas = state.Personas.SetItem(Fixture.Kaori, kaori),
        };
    }

    /// <summary>自由時間の決定タイミングを、香織の状態で行う。乱数はシード 42 のワールドの位置 0。</summary>
    private static DeciderOutput Decide(SimState state)
    {
        var random = new SplitMixRandomSource().Open(42, Fixture.Kaori.Value, "decide", 0);
        return new UtilityDecider().Decide(new DecisionContext(state, Fixture.Kaori, Fixture.Now, DecisionReason.Free, null, Fixture.Context, random));
    }

    /// <summary>
    /// 14 時から 17 時まで、自由時間の一つのプランを香織に積んだ状態。
    /// </summary>
    private static SimState WithFreePlan()
    {
        var plan = new DayPlan(
            1,
            Fixture.Now,
            Fixture.Now.AddHours(3),
            Fixture.Now.AddHours(3),
            PlanSource.Fallback,
            [new PlanBlock(null, BlockKind.Free, Fixture.Now, Fixture.Now.AddHours(3), null, null, PhonePolicy.Closed, null, [], PartnerAbsentPolicy.Continue)],
            null);
        return Fixture.Apply(Fixture.WithKaori(), Fixture.Event(10, Fixture.Now, EventTypes.PlanCommitted, new PlanCommittedPayload(plan), Fixture.Kaori, new Guard([new PlanVersionCheck(0)], RejectAction.Drop))).State;
    }

    /// <summary>移動の可否の判定は、定義の場所と経路を、インスタンスの DefId から引く。</summary>
    [Fact]
    public void MovementCheckUsesDefinitionOfInstanceState()
    {
        var state = WithRenamedWorldInstance(Fixture.WithKaori());
        var kaori = state.Personas[Fixture.Kaori];

        Assert.True(ActionRules.CanChoose(Fixture.Context, state, kaori, ActionRules.GoToNextId, KaoriRoom, Fixture.Now));
    }

    /// <summary>自由時間の決定は、定義の場所と経路を、インスタンスの DefId から引く。結果は、ワールドの ID を変えなかったときと同じ。</summary>
    [Fact]
    public void DecisionMatchesOriginalWhenWorldInstanceIdDiffers()
    {
        var committed = WithFreePlan();

        var expected = Decide(committed);
        var actual = Decide(WithRenamedWorldInstance(committed));

        Assert.Equal(expected.Payload.ActionId, actual.Payload.ActionId);
        Assert.Equal(expected.Payload.Duration, actual.Payload.Duration);
        Assert.Equal(expected.Payload.Place, actual.Payload.Place);
    }

    /// <summary>行動の開始は、定義の時差を、インスタンスの DefId から引く。読書は 14 時に始まり、状態は開始の行動を持つ。</summary>
    [Fact]
    public void ActionStartUsesDefinitionOfInstanceState()
    {
        var state = WithRenamedWorldInstance(Fixture.WithKaori());
        var payload = new ActionStartedPayload("read_book", KaoriRoom, TimeSpan.FromMinutes(30), ActivityReason.Chosen, 0, null);

        var result = Fixture.Apply(state, Fixture.Event(10, Fixture.Now, EventTypes.ActionStarted, payload, Fixture.Kaori));

        Assert.Equal(EventStatus.Applied, result.Status);
        Assert.Equal("read_book", result.State.Personas[Fixture.Kaori].Activity?.ActionId);
    }

    /// <summary>固定ブロックの開始は、場所の経路と行動の条件を、インスタンスの DefId から引く。同じ場所（経路 0 分）の読書は始まる。</summary>
    [Fact]
    public void FixedBlockStartUsesDefinitionOfInstanceState()
    {
        const string blockId = "reading@2026-11-01";
        var plan = new DayPlan(
            1,
            Fixture.Now,
            Fixture.Now.AddHours(24),
            Fixture.Now.AddHours(21),
            PlanSource.Fallback,
            [new PlanBlock(blockId, BlockKind.Fixed, Fixture.Now, Fixture.Now.AddHours(1), "read_book", KaoriRoom, PhonePolicy.Closed, null, [], PartnerAbsentPolicy.Continue)],
            null);
        var planned = Fixture.Apply(Fixture.WithKaori(), Fixture.Event(10, Fixture.Now, EventTypes.PlanCommitted, new PlanCommittedPayload(plan), Fixture.Kaori, new Guard([new PlanVersionCheck(0)], RejectAction.Drop))).State;

        var result = Fixture.Apply(WithRenamedWorldInstance(planned), Fixture.Event(12, Fixture.Now, EventTypes.BlockStarted, new BlockStartedPayload(blockId, 1), Fixture.Kaori));

        Assert.Equal(EventStatus.Applied, result.Status);
        Assert.Equal(blockId, result.State.Personas[Fixture.Kaori].LastBlockId);
        Assert.Contains(result.FollowUps, ev => ev.Payload is ActionStartedPayload { ActionId: "read_book" });
    }

    /// <summary>固定予定だけのプランの作り直しは、定義の時差と居住ワールドを、インスタンスの DefId から引く。作り直しの結果は、ワールドの ID を変えなかったときと同じ。</summary>
    [Fact]
    public void PlannerUsesDefinitionOfInstanceState()
    {
        var persona = WithRenamedWorldInstance(Fixture.WithKaori()).Personas[Fixture.Kaori];

        var plan = FallbackPlanner.Plan(Fixture.Context.Defs, persona, Fixture.Now, 1);

        Assert.Equal(2, plan.Blocks.Length);
        Assert.Equal("sleep_weekday@2026-11-01", plan.Blocks[1].BlockId);
        Assert.Equal(Fixture.Now.AddMinutes(45), plan.NextRebuildAt);
    }
}
