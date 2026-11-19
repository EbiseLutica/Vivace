using Vivace.Core.Domain.State;
using Vivace.Core.Events.Guards;
using Vivace.Core.Domain;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;
using Vivace.Core.Signals;
using Vivace.Core;
using Vivace.Tests.Support;

namespace Vivace.Tests.Rules;

/// <summary>
/// 副行動（スマホを開く、投稿する）の開始と終了と決定。主の行動は、ブロックの読書（1時間）とし、副行動は 5 分の喫茶店で確かめる。
/// </summary>
public class SubActionRulesTests
{
    private static readonly PlaceId TaroRoom = new("taro_room");
    private const string Block = "reading@2026-11-01";

    /// <summary>ブロックの読書（1時間）を始めた状態。</summary>
    private static SimState Reading() => Fixture.Apply(
        Fixture.WithTaro(),
        Fixture.Event(10, Fixture.Now, EventTypes.ActionStarted, new ActionStartedPayload("read_book", TaroRoom, TimeSpan.FromHours(1), ActivityReason.Block, 0, Block), Fixture.Taro)).State;

    /// <summary>副行動の開始のイベント（喫茶店で 5 分、理由は自由）。</summary>
    private static SimEvent SubStarted(long seq, DateTimeOffset at) =>
        Fixture.Event(seq, at, EventTypes.ActionStarted, new ActionStartedPayload("cafe", TaroRoom, TimeSpan.FromMinutes(5), ActivityReason.Chosen, 0, null, true), Fixture.Taro);

    /// <summary>読書の中で、副行動の喫茶店が始まった状態。</summary>
    private static SimState WithOverlay() => Fixture.Apply(Reading(), SubStarted(11, Fixture.Now)).State;

    /// <summary>スマホを開く機会の決定タイミングのイベント（ブロックの ID つき）。</summary>
    private static SimEvent PhoneRequest(long seq, string block) =>
        Fixture.Event(seq, Fixture.Now, EventTypes.DecisionRequested, new DecisionRequestedPayload(DecisionReason.Phone, block), Fixture.Taro);

    /// <summary>副行動の決定の中身。移動はなく、乱数の範囲は指定どおり。</summary>
    private static ActivityChosenPayload SubChoice(string action, TimeSpan duration, ulong from, ulong to) =>
        new(action, TaroRoom, [], 0, duration, true, new RngDraws(Fixture.Taro.Value, "decide", from, to));

    /// <summary>副行動の開始は、主の行動・率・バージョンを変えず、Overlay に入れ、終わりを 5 分後に積む。</summary>
    [Fact]
    public void SubStartSetsOverlayAndKeepsMainAction()
    {
        var result = Fixture.Apply(Reading(), SubStarted(11, Fixture.Now));

        Assert.Equal(EventStatus.Applied, result.Status);
        var taro = result.State.Personas[Fixture.Taro];
        Assert.Equal("cafe", taro.Overlay!.ActionId);
        Assert.Equal(Fixture.Now.AddMinutes(5), taro.Overlay.EndsAt);
        Assert.Equal("read_book", taro.Activity!.ActionId);
        Assert.Equal(0, taro.ActivityVersion);
        Assert.Equal(-2.0, taro.Vitals.Energy.Rate);
        var ended = Assert.Single(result.FollowUps);
        Assert.Equal(new ActionEndedPayload("cafe", 0, true), ended.Payload);
        Assert.Equal(Fixture.Now.AddMinutes(5), ended.Time);
    }

    /// <summary>主の行動がないときは、副行動は始まらない。</summary>
    [Fact]
    public void SubStartWithoutMainActionIsSkipped()
    {
        var result = Fixture.Apply(Fixture.WithTaro(), SubStarted(11, Fixture.Now));

        Assert.Equal(EventStatus.Skipped, result.Status);
        Assert.Null(result.State.Personas[Fixture.Taro].Overlay);
    }

    /// <summary>副行動の終わりは、Overlay を外す。効果は当てず、決定タイミングも積まない。主の行動は続く。</summary>
    [Fact]
    public void SubEndClearsOverlayWithoutDecision()
    {
        var ended = Fixture.Event(12, Fixture.Now.AddMinutes(5), EventTypes.ActionEnded, new ActionEndedPayload("cafe", 0, true), Fixture.Taro);

        var result = Fixture.Apply(WithOverlay(), ended);

        Assert.Equal(EventStatus.Applied, result.Status);
        var taro = result.State.Personas[Fixture.Taro];
        Assert.Null(taro.Overlay);
        Assert.Equal("read_book", taro.Activity!.ActionId);
        Assert.Empty(result.FollowUps);
    }

    /// <summary>終わりの時刻が違う副行動の終わりは捨てる（古い副行動）。Overlay は残る。</summary>
    [Fact]
    public void SubEndAtOtherTimeIsSkipped()
    {
        var stale = Fixture.Event(12, Fixture.Now.AddMinutes(4), EventTypes.ActionEnded, new ActionEndedPayload("cafe", 0, true), Fixture.Taro);

        var result = Fixture.Apply(WithOverlay(), stale);

        Assert.Equal(EventStatus.Skipped, result.Status);
        Assert.Equal("cafe", result.State.Personas[Fixture.Taro].Overlay!.ActionId);
    }

    /// <summary>主の行動が終わると、同じ時刻の副行動も外れる（Overlay は主の行動がないと成り立たない）。</summary>
    [Fact]
    public void MainEndClearsOverlay()
    {
        var mainEnded = Fixture.Event(12, Fixture.Now.AddMinutes(5), EventTypes.ActionEnded, new ActionEndedPayload("read_book", 0), Fixture.Taro);

        var result = Fixture.Apply(WithOverlay(), mainEnded);

        Assert.Equal(EventStatus.Applied, result.Status);
        var taro = result.State.Personas[Fixture.Taro];
        Assert.Null(taro.Activity);
        Assert.Null(taro.Overlay);
    }

    /// <summary>ブロックの行動の途中で、スマホを開く機会の決定タイミングを積むと、決定中のフラグを立て、Decider を起動する。</summary>
    [Fact]
    public void PhoneRequestDuringBlockActionStartsDecider()
    {
        var result = Fixture.Apply(Reading(), PhoneRequest(11, Block));

        Assert.Equal(EventStatus.Applied, result.Status);
        Assert.True(result.State.Personas[Fixture.Taro].Deciding);
        Assert.Equal(new StartDeciderSignal(11, Fixture.Taro, Fixture.Now, DecisionReason.Phone, Block), Assert.Single(result.Signals));
    }

    /// <summary>ブロックの行動が別のブロックのときと、副行動の途中のときは、スマホを開く機会を捨てる。</summary>
    [Fact]
    public void PhoneRequestIsDiscardedForOtherBlockOrOverlay()
    {
        var otherBlock = Fixture.Apply(Reading(), PhoneRequest(11, "other@2026-11-01"));
        var overlay = Fixture.Apply(WithOverlay(), PhoneRequest(12, Block));

        Assert.Equal(EventStatus.Skipped, otherBlock.Status);
        Assert.Empty(otherBlock.Signals);
        Assert.Equal(EventStatus.Skipped, overlay.Status);
        Assert.Empty(overlay.Signals);
    }

    /// <summary>副行動の決定は、主の行動のバージョンを上げず、副行動の開始を同じ時刻に積む。乱数の位置を進め、決定中を下ろす。</summary>
    [Fact]
    public void ChosenSubActionStartsWithoutBumpingVersion()
    {
        var deciding = Fixture.Apply(Reading(), PhoneRequest(11, Block)).State;

        var result = Fixture.Apply(deciding, Fixture.Event(12, Fixture.Now, EventTypes.ActivityChosen, SubChoice("cafe", TimeSpan.FromMinutes(5), 0, 3), Fixture.Taro));

        Assert.Equal(EventStatus.Applied, result.Status);
        var taro = result.State.Personas[Fixture.Taro];
        Assert.False(taro.Deciding);
        Assert.Equal(0, taro.ActivityVersion);
        Assert.Equal(3UL, taro.Rng["decide"]);
        var started = Assert.Single(result.FollowUps);
        Assert.Equal(EventTypes.ActionStarted, started.EventType);
        Assert.Equal(new ActionStartedPayload("cafe", TaroRoom, TimeSpan.FromMinutes(5), ActivityReason.Chosen, 0, null, true), started.Payload);
        Assert.Equal(Fixture.Now, started.Time);
    }

    /// <summary>何もしない（所要時間 0）は、乱数の位置だけを進め、決定中を下ろす。後続は積まない。</summary>
    [Fact]
    public void ChosenIdleOnlyAdvancesRng()
    {
        var deciding = Fixture.Apply(Reading(), PhoneRequest(11, Block)).State;

        var result = Fixture.Apply(deciding, Fixture.Event(12, Fixture.Now, EventTypes.ActivityChosen, SubChoice("core.idle", TimeSpan.Zero, 0, 2), Fixture.Taro));

        Assert.Equal(EventStatus.Applied, result.Status);
        var taro = result.State.Personas[Fixture.Taro];
        Assert.False(taro.Deciding);
        Assert.Equal(0, taro.ActivityVersion);
        Assert.Equal(2UL, taro.Rng["decide"]);
        Assert.Empty(result.FollowUps);
        Assert.Null(taro.Overlay);
    }

    /// <summary>次の固定ブロックの ID（読書と同じ時刻に始まる、別のブロック）。</summary>
    private const string Next = "other@2026-11-01";

    /// <summary>読書と、そのあとに始まる固定ブロック（同じ時刻）を持つプランを確定した太郎の状態。</summary>
    private static SimState WithTwoBlocks()
    {
        var plan = new DayPlan(
            1,
            Fixture.Now,
            Fixture.Now.AddHours(24),
            Fixture.Now.AddHours(21),
            PlanSource.Fallback,
            [
                new PlanBlock(Block, BlockKind.Fixed, Fixture.Now, Fixture.Now.AddHours(1), "read_book", TaroRoom, PhonePolicy.Open, null, [], PartnerAbsentPolicy.Continue),
                new PlanBlock(Next, BlockKind.Fixed, Fixture.Now, Fixture.Now.AddHours(1), "read_book", TaroRoom, PhonePolicy.Closed, null, [], PartnerAbsentPolicy.Continue),
            ],
            null);
        var committed = Fixture.Event(9, Fixture.Now, EventTypes.PlanCommitted, new PlanCommittedPayload(plan), Fixture.Taro, new Guard([new PlanVersionCheck(0)], RejectAction.Drop));
        return Fixture.Apply(Fixture.WithTaro(), committed).State;
    }

    /// <summary>
    /// 副行動の途中に固定ブロックが始まると、主の行動と一緒に副行動も外れる。あとから届いた副行動の終わりは、捨てられる。
    /// </summary>
    [Fact]
    public void BlockStartClearsOverlayAndLaterSubEndIsSkipped()
    {
        var reading = Fixture.Apply(WithTwoBlocks(), Fixture.Event(10, Fixture.Now, EventTypes.ActionStarted, new ActionStartedPayload("read_book", TaroRoom, TimeSpan.FromHours(1), ActivityReason.Block, 0, Block), Fixture.Taro)).State;
        var overlay = Fixture.Apply(reading, SubStarted(11, Fixture.Now)).State;
        Assert.NotNull(overlay.Personas[Fixture.Taro].Overlay);

        var started = Fixture.Apply(overlay, Fixture.Event(12, Fixture.Now, EventTypes.BlockStarted, new BlockStartedPayload(Next, 1), Fixture.Taro));

        Assert.Equal(EventStatus.Applied, started.Status);
        Assert.Null(started.State.Personas[Fixture.Taro].Overlay);
        var lateEnd = Fixture.Event(13, Fixture.Now.AddMinutes(5), EventTypes.ActionEnded, new ActionEndedPayload("cafe", 0, true), Fixture.Taro);
        Assert.Equal(EventStatus.Skipped, Fixture.Apply(started.State, lateEnd).Status);
    }
}
