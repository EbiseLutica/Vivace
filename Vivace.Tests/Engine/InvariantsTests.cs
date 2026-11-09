using System.Collections.Immutable;
using Vivace.Core.Domain;
using Vivace.Core.Engine;
using Vivace.Core.Events;
using Vivace.Tests.Support;

namespace Vivace.Tests.Engine;

/// <summary>
/// 「各ペルソナに次の自分のイベントがある」の検査を、わざと壊した状態で確かめる（D7 7.4）。
/// 行動中は同じ版の行動の終わり、移動中は到着、決め中は決める仕事、自由時間の手空きは決めどきが、それぞれ予定されていなければならない。
/// </summary>
public class InvariantsTests
{
    private static readonly PlaceId KaoriRoom = new("kaori_room");

    private static SimState Running(long version = 0) => Fixture.With(
        Fixture.WithKaori(),
        Fixture.Get(Fixture.WithKaori(), Fixture.Kaori) with
        {
            Activity = new Activity("read_book", KaoriRoom, Fixture.Now, Fixture.Now.AddMinutes(30), ActivityReason.Chosen, null),
            ActivityVersion = version,
        });

    private static SimState Traveling() => Fixture.With(
        Fixture.WithKaori(),
        Fixture.Get(Fixture.WithKaori(), Fixture.Kaori) with
        {
            Location = new Traveling(KaoriRoom, new PlaceId("home_living"), ImmutableArray<RouteStep>.Empty, Fixture.Now, Fixture.Now.AddMinutes(1)),
        });

    private static SimState Deciding() => Fixture.With(
        Fixture.WithKaori(),
        Fixture.Get(Fixture.WithKaori(), Fixture.Kaori) with { Deciding = true });

    private static SimEvent Pending(long seq, string type, EventPayload payload) =>
        Fixture.Event(seq, Fixture.Now.AddMinutes(30), type, payload, Fixture.Kaori);

    private static void Check(SimState state, IEnumerable<SimEvent> pending, bool busy = false)
    {
        var lookup = pending.ToLookup(ev => ev.PersonaId!.Value);
        var busySet = busy ? ImmutableHashSet.Create(Fixture.Kaori) : ImmutableHashSet<PersonaId>.Empty;
        Invariants.Check(Fixture.Context, state, state.Position.Time, busySet, lookup, null);
    }

    /// <summary>行動中なのに同じ版の行動の終わりを積まなかった状態は検出される。別の版の終わりでは足りない。</summary>
    [Fact]
    public void MissingActionEndOfSameVersionIsDetected()
    {
        var running = Running(version: 2);
        Assert.Throws<InvariantViolationException>(() => Check(running, []));
        Assert.Throws<InvariantViolationException>(() => Check(running, [Pending(10, EventTypes.ActionEnded, new ActionEndedPayload("read_book", 1))]));
        Check(running, [Pending(11, EventTypes.ActionEnded, new ActionEndedPayload("read_book", 2))]);
    }

    /// <summary>移動中なのに到着を積まなかった状態は検出される。</summary>
    [Fact]
    public void MissingArrivalIsDetected()
    {
        Assert.Throws<InvariantViolationException>(() => Check(Traveling(), []));
        Check(Traveling(), [Pending(10, EventTypes.TravelArrived, new TravelArrivedPayload(new PlaceId("home_living"), 0))]);
    }

    /// <summary>決め中なのに、決める仕事を起動しなかった状態は検出される。仕事が動いていれば通る。</summary>
    [Fact]
    public void MissingDecisionIsDetected()
    {
        Assert.Throws<InvariantViolationException>(() => Check(Deciding(), []));
        Check(Deciding(), [], busy: true);
    }

    /// <summary>何も予定されていない、場所にいるだけのペルソナは、止まっているので検出される。</summary>
    [Fact]
    public void IdlePersonaWithoutEventIsDetected()
    {
        Assert.Throws<InvariantViolationException>(() => Check(Fixture.WithKaori(), []));
    }

    /// <summary>
    /// 自由時間に手空きのままで、決めどきも予定されていない状態は検出される（B5 の止まり方）。
    /// 次の予定のブロックがあっても、今の決めどきがなければ検出する。
    /// </summary>
    [Fact]
    public void IdleInFreeTimeWithoutDecisionIsDetected()
    {
        var plan = new Vivace.Core.Domain.DayPlan(
            1,
            Fixture.Now,
            Fixture.Now.AddHours(3),
            Fixture.Now.AddHours(3),
            Vivace.Core.Domain.PlanSource.Fallback,
            [new Vivace.Core.Domain.PlanBlock(null, Vivace.Core.Domain.BlockKind.Free, Fixture.Now, Fixture.Now.AddHours(3), null, null, Vivace.Core.Domain.PhonePolicy.Closed, null, [], Vivace.Core.Domain.PartnerAbsentPolicy.Continue)],
            null);
        var state = Fixture.With(Fixture.WithKaori(), Fixture.Get(Fixture.WithKaori(), Fixture.Kaori) with { Plan = plan });

        Assert.Throws<InvariantViolationException>(() => Check(state, [Pending(10, EventTypes.BlockStarted, new BlockStartedPayload("later@2026-11-01", 1))]));
        Check(state, [Pending(11, EventTypes.DecisionRequested, new DecisionRequestedPayload(DecisionReason.Ended, null))]);
    }

    /// <summary>範囲の外の状態変数は検出される。</summary>
    [Fact]
    public void VitalOutOfRangeIsDetected()
    {
        var broken = Fixture.With(
            Fixture.WithKaori(),
            Fixture.Get(Fixture.WithKaori(), Fixture.Kaori) with
            {
                Vitals = Fixture.Get(Fixture.WithKaori(), Fixture.Kaori).Vitals with { Energy = new VitalTrack(Fixture.Now, 150, 0) },
            });

        Assert.Throws<InvariantViolationException>(() => Check(broken, [Pending(10, EventTypes.BlockStarted, new BlockStartedPayload(null, 1))]));
    }

    /// <summary>時刻が進まないまま、上限を超えて適用が続くと止まる。上限以内なら通る（B5）。</summary>
    [Fact]
    public void ProgressGuardStopsEndlessApplicationAtOneInstant()
    {
        Invariants.CheckProgress(Invariants.MaxAppliedAtOneInstant);
        Assert.Throws<InvariantViolationException>(() => Invariants.CheckProgress(Invariants.MaxAppliedAtOneInstant + 1));
    }
}
