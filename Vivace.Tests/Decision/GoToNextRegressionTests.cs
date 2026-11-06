using Vivace.Core.Decision;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Randomness;
using Vivace.Core.Rules;
using Vivace.Tests.Support;

namespace Vivace.Tests.Decision;

/// <summary>
/// 次の予定の場所へ向かう行動が、帰還ぎりぎりで候補から消えて、決める側が空になる誤りの再発防止（J-112、J-138）。
/// </summary>
public class GoToNextRegressionTests
{
    private static readonly PlaceId SchoolRoom = new("school_2a");
    private static readonly PlaceId KaoriRoom = new("kaori_room");

    /// <summary>
    /// 学校から、18 分後に家の場所の予定が始まる。家までは 19 分かかる（待ちは −1 分）。
    /// 次の予定の場所へ向かう行動は残り、移動だけで終わる（所要時間 0）。例外にならない。
    /// </summary>
    [Fact]
    public void GoToNextIsKeptAsMoveOnlyWhenTheWayTakesLongerThanTheWait()
    {
        var start = Fixture.Now.AddMinutes(18);
        var plan = new DayPlan(
            1,
            Fixture.Now,
            start.AddHours(1),
            start.AddHours(1),
            PlanSource.Fallback,
            [
                new PlanBlock(null, BlockKind.Free, Fixture.Now, start, null, null, PhonePolicy.Closed, null, [], PartnerAbsentPolicy.Continue),
                new PlanBlock("home@2026-11-01", BlockKind.Fixed, start, start.AddHours(1), "read_book", KaoriRoom, PhonePolicy.Closed, null, [], PartnerAbsentPolicy.Continue),
            ],
            null);
        var committed = Fixture.Apply(Fixture.WithKaori(), Fixture.Event(10, Fixture.Now, EventTypes.PlanCommitted, new PlanCommittedPayload(plan), Fixture.Kaori, new Guard([new PlanVersionCheck(0)], RejectAction.Drop))).State;
        var state = Fixture.With(committed, committed.Personas[Fixture.Kaori] with { Location = new AtPlace(SchoolRoom) });
        var persona = state.Personas[Fixture.Kaori];
        var random = new SplitMixRandomSource().Open(state.Worlds[Fixture.World].Seed, Fixture.Kaori.Value, "decide", 0);

        var output = new UtilityDecider().Decide(new DecisionContext(state, Fixture.Kaori, Fixture.Now, DecisionReason.Free, null, Fixture.Context, random));

        Assert.Equal(UtilityDecider.GoToNext, output.Payload.ActionId);
        Assert.Equal(KaoriRoom, output.Payload.Place);
        Assert.Equal(19, output.Payload.TravelMinutes);
        Assert.Equal(TimeSpan.Zero, output.Payload.Duration);
        Assert.Equal(new ActivityVersionCheck(persona.ActivityVersion), output.Guard.Checks[0]);
    }
}
