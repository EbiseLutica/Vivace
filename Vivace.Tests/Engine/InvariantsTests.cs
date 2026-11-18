using System.Collections.Immutable;
using Vivace.Core.Domain.State;
using Vivace.Core.Domain;
using Vivace.Core.Events.Payloads;
using Vivace.Core;
using Vivace.Core.Events;
using Vivace.Tests.Support;

namespace Vivace.Tests.Engine;

/// <summary>
/// 「各ペルソナに次の自分のイベントがある」の検査を、わざと壊した状態で確かめる。
/// 行動中は同じバージョンの行動の終わり、移動中は到着、決定中は決めるジョブ、自由時間のアイドルは決定タイミングが、それぞれ予定されていなければならない。
/// </summary>
public class InvariantsTests
{
    private static readonly PlaceId KaoriRoom = new("kaori_room");

    /// <summary>行動中の状態を作る。</summary>
    private static SimState Running(long version = 0) => Fixture.With(
        Fixture.WithKaori(),
        Fixture.Get(Fixture.WithKaori(), Fixture.Kaori) with
        {
            Activity = new Activity("read_book", KaoriRoom, Fixture.Now, Fixture.Now.AddMinutes(30), ActivityReason.Chosen, null),
            ActivityVersion = version,
        });

    /// <summary>移動中の状態を作る。</summary>
    private static SimState Traveling() => Fixture.With(
        Fixture.WithKaori(),
        Fixture.Get(Fixture.WithKaori(), Fixture.Kaori) with
        {
            Location = new Traveling(KaoriRoom, new PlaceId("home_living"), ImmutableArray<RouteStep>.Empty, Fixture.Now, Fixture.Now.AddMinutes(1)),
        });

    /// <summary>決定中の状態を作る。</summary>
    private static SimState Deciding() => Fixture.With(
        Fixture.WithKaori(),
        Fixture.Get(Fixture.WithKaori(), Fixture.Kaori) with { Deciding = true });

    /// <summary>未適用のイベントを作る。</summary>
    private static SimEvent Pending(long seq, string type, EventPayload payload) =>
        Fixture.Event(seq, Fixture.Now.AddMinutes(30), type, payload, Fixture.Kaori);

    /// <summary>不変条件を検査する。</summary>
    private static void Check(SimState state, IEnumerable<SimEvent> pending, bool busy = false)
    {
        var lookup = pending.ToLookup(ev => ev.PersonaId!.Value);
        var busySet = busy ? ImmutableHashSet.Create(Fixture.Kaori) : ImmutableHashSet<PersonaId>.Empty;
        Invariants.Check(Fixture.Context, state, state.Cursor.Time, busySet, lookup, null);
    }

    /// <summary>行動中なのに同じバージョンの行動の終わりを積まなかった状態は検出される。別のバージョンの終わりでは足りない。</summary>
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

    /// <summary>決定中なのに、決めるジョブを起動しなかった状態は検出される。ジョブが動いていれば通る。</summary>
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
    /// 自由時間にアイドルのままで、決定タイミングも予定されていない状態は検出される（B5 の止まり方）。
    /// 次の予定のブロックがあっても、今の決定タイミングがなければ検出する。
    /// </summary>
    [Fact]
    public void IdleInFreeTimeWithoutDecisionIsDetected()
    {
        var plan = new Vivace.Core.Domain.State.DayPlan(
            1,
            Fixture.Now,
            Fixture.Now.AddHours(3),
            Fixture.Now.AddHours(3),
            Vivace.Core.Domain.State.PlanSource.Fallback,
            [new Vivace.Core.Domain.State.PlanBlock(null, Vivace.Core.Domain.State.BlockKind.Free, Fixture.Now, Fixture.Now.AddHours(3), null, null, Vivace.Core.Domain.PhonePolicy.Closed, null, [], Vivace.Core.Domain.PartnerAbsentPolicy.Continue)],
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

    /// <summary>時刻が進まないまま、上限を超えて適用が続くと止まる。上限以内なら通る。</summary>
    [Fact]
    public void ProgressGuardStopsEndlessApplicationAtOneInstant()
    {
        Invariants.CheckProgress(Invariants.MaxAppliedAtOneInstant);
        Assert.Throws<InvariantViolationException>(() => Invariants.CheckProgress(Invariants.MaxAppliedAtOneInstant + 1));
    }

    /// <summary>副行動の途中なのに、主の行動がない状態は検出される（副行動は主の行動があって成り立つ）。</summary>
    [Fact]
    public void OverlayWithoutMainActionIsDetected()
    {
        var overlay = new Activity("cafe", KaoriRoom, Fixture.Now, Fixture.Now.AddMinutes(5), ActivityReason.Chosen, null);
        var state = Fixture.With(Fixture.WithKaori(), Fixture.Get(Fixture.WithKaori(), Fixture.Kaori) with { Overlay = overlay });
        var subEnd = Fixture.Event(10, overlay.EndsAt, EventTypes.ActionEnded, new ActionEndedPayload("cafe", 0, true), Fixture.Kaori);

        Assert.Throws<InvariantViolationException>(() => Check(state, []));
        Assert.Throws<InvariantViolationException>(() => Check(state, [subEnd]));
    }

    /// <summary>
    /// 副行動の途中なのに、副行動の終わりを積んでいない状態は検出される。同じ行動と同じ時刻の終わりがあれば通る。
    /// </summary>
    [Fact]
    public void OverlayWithoutSubActionEndIsDetected()
    {
        var overlay = new Activity("cafe", KaoriRoom, Fixture.Now, Fixture.Now.AddMinutes(5), ActivityReason.Chosen, null);
        var running = Running();
        var state = Fixture.With(running, Fixture.Get(running, Fixture.Kaori) with { Overlay = overlay });
        var mainEnd = Pending(11, EventTypes.ActionEnded, new ActionEndedPayload("read_book", 0));
        var subEnd = Fixture.Event(12, Fixture.Now.AddMinutes(5), EventTypes.ActionEnded, new ActionEndedPayload("cafe", 0, true), Fixture.Kaori);

        Assert.Throws<InvariantViolationException>(() => Check(state, [mainEnd]));
        Check(state, [mainEnd, subEnd]);
    }
}
