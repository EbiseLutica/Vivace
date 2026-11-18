using Vivace.Core.Domain.State;
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
    private static readonly PlaceId KaoriRoom = new("kaori_room");
    private const string Block = "reading@2026-11-01";

    /// <summary>ブロックの読書（1時間）を始めた状態。</summary>
    private static SimState Reading() => Fixture.Apply(
        Fixture.WithKaori(),
        Fixture.Event(10, Fixture.Now, EventTypes.ActionStarted, new ActionStartedPayload("read_book", KaoriRoom, TimeSpan.FromHours(1), ActivityReason.Block, 0, Block), Fixture.Kaori)).State;

    /// <summary>副行動の開始のイベント（喫茶店で 5 分、理由は自由）。</summary>
    private static SimEvent SubStarted(long seq, DateTimeOffset at) =>
        Fixture.Event(seq, at, EventTypes.ActionStarted, new ActionStartedPayload("cafe", KaoriRoom, TimeSpan.FromMinutes(5), ActivityReason.Chosen, 0, null, true), Fixture.Kaori);

    /// <summary>読書の中で、副行動の喫茶店が始まった状態。</summary>
    private static SimState WithOverlay() => Fixture.Apply(Reading(), SubStarted(11, Fixture.Now)).State;

    /// <summary>スマホを開く機会の決定タイミングのイベント（ブロックの ID つき）。</summary>
    private static SimEvent PhoneRequest(long seq, string block) =>
        Fixture.Event(seq, Fixture.Now, EventTypes.DecisionRequested, new DecisionRequestedPayload(DecisionReason.Phone, block), Fixture.Kaori);

    /// <summary>副行動の決定の中身。移動はなく、乱数の範囲は指定どおり。</summary>
    private static ActivityChosenPayload SubChoice(string action, TimeSpan duration, ulong from, ulong to) =>
        new(action, KaoriRoom, [], 0, duration, true, new RngDraws(Fixture.Kaori.Value, "decide", from, to));

    /// <summary>副行動の開始は、主の行動・率・バージョンを変えず、Overlay に入れ、終わりを 5 分後に積む。</summary>
    [Fact]
    public void SubStartSetsOverlayAndKeepsMainAction()
    {
        var result = Fixture.Apply(Reading(), SubStarted(11, Fixture.Now));

        Assert.Equal(EventStatus.Applied, result.Status);
        var kaori = result.State.Personas[Fixture.Kaori];
        Assert.Equal("cafe", kaori.Overlay!.ActionId);
        Assert.Equal(Fixture.Now.AddMinutes(5), kaori.Overlay.EndsAt);
        Assert.Equal("read_book", kaori.Activity!.ActionId);
        Assert.Equal(0, kaori.ActivityVersion);
        Assert.Equal(-2.0, kaori.Vitals.Energy.Rate);
        var ended = Assert.Single(result.FollowUps);
        Assert.Equal(new ActionEndedPayload("cafe", 0, true), ended.Payload);
        Assert.Equal(Fixture.Now.AddMinutes(5), ended.Time);
    }

    /// <summary>主の行動がないときは、副行動は始まらない。</summary>
    [Fact]
    public void SubStartWithoutMainActionIsSkipped()
    {
        var result = Fixture.Apply(Fixture.WithKaori(), SubStarted(11, Fixture.Now));

        Assert.Equal(EventStatus.Skipped, result.Status);
        Assert.Null(result.State.Personas[Fixture.Kaori].Overlay);
    }

    /// <summary>副行動の終わりは、Overlay を外す。効果は当てず、決定タイミングも積まない。主の行動は続く。</summary>
    [Fact]
    public void SubEndClearsOverlayWithoutDecision()
    {
        var ended = Fixture.Event(12, Fixture.Now.AddMinutes(5), EventTypes.ActionEnded, new ActionEndedPayload("cafe", 0, true), Fixture.Kaori);

        var result = Fixture.Apply(WithOverlay(), ended);

        Assert.Equal(EventStatus.Applied, result.Status);
        var kaori = result.State.Personas[Fixture.Kaori];
        Assert.Null(kaori.Overlay);
        Assert.Equal("read_book", kaori.Activity!.ActionId);
        Assert.Empty(result.FollowUps);
    }

    /// <summary>終わりの時刻が違う副行動の終わりは捨てる（古い副行動）。Overlay は残る。</summary>
    [Fact]
    public void SubEndAtOtherTimeIsSkipped()
    {
        var stale = Fixture.Event(12, Fixture.Now.AddMinutes(4), EventTypes.ActionEnded, new ActionEndedPayload("cafe", 0, true), Fixture.Kaori);

        var result = Fixture.Apply(WithOverlay(), stale);

        Assert.Equal(EventStatus.Skipped, result.Status);
        Assert.Equal("cafe", result.State.Personas[Fixture.Kaori].Overlay!.ActionId);
    }

    /// <summary>主の行動が終わると、同じ時刻の副行動も外れる（Overlay は主の行動がないと成り立たない）。</summary>
    [Fact]
    public void MainEndClearsOverlay()
    {
        var mainEnded = Fixture.Event(12, Fixture.Now.AddMinutes(5), EventTypes.ActionEnded, new ActionEndedPayload("read_book", 0), Fixture.Kaori);

        var result = Fixture.Apply(WithOverlay(), mainEnded);

        Assert.Equal(EventStatus.Applied, result.Status);
        var kaori = result.State.Personas[Fixture.Kaori];
        Assert.Null(kaori.Activity);
        Assert.Null(kaori.Overlay);
    }

    /// <summary>ブロックの行動の途中で、スマホを開く機会の決定タイミングを積むと、決定中のフラグを立て、Decider を起動する。</summary>
    [Fact]
    public void PhoneRequestDuringBlockActionStartsDecider()
    {
        var result = Fixture.Apply(Reading(), PhoneRequest(11, Block));

        Assert.Equal(EventStatus.Applied, result.Status);
        Assert.True(result.State.Personas[Fixture.Kaori].Deciding);
        Assert.Equal(new StartDeciderSignal(11, Fixture.Kaori, Fixture.Now, DecisionReason.Phone, Block), Assert.Single(result.Signals));
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

        var result = Fixture.Apply(deciding, Fixture.Event(12, Fixture.Now, EventTypes.ActivityChosen, SubChoice("cafe", TimeSpan.FromMinutes(5), 0, 3), Fixture.Kaori));

        Assert.Equal(EventStatus.Applied, result.Status);
        var kaori = result.State.Personas[Fixture.Kaori];
        Assert.False(kaori.Deciding);
        Assert.Equal(0, kaori.ActivityVersion);
        Assert.Equal(3UL, kaori.Rng["decide"]);
        var started = Assert.Single(result.FollowUps);
        Assert.Equal(EventTypes.ActionStarted, started.EventType);
        Assert.Equal(new ActionStartedPayload("cafe", KaoriRoom, TimeSpan.FromMinutes(5), ActivityReason.Chosen, 0, null, true), started.Payload);
        Assert.Equal(Fixture.Now, started.Time);
    }

    /// <summary>何もしない（所要時間 0）は、乱数の位置だけを進め、決定中を下ろす。後続は積まない。</summary>
    [Fact]
    public void ChosenIdleOnlyAdvancesRng()
    {
        var deciding = Fixture.Apply(Reading(), PhoneRequest(11, Block)).State;

        var result = Fixture.Apply(deciding, Fixture.Event(12, Fixture.Now, EventTypes.ActivityChosen, SubChoice("core.idle", TimeSpan.Zero, 0, 2), Fixture.Kaori));

        Assert.Equal(EventStatus.Applied, result.Status);
        var kaori = result.State.Personas[Fixture.Kaori];
        Assert.False(kaori.Deciding);
        Assert.Equal(0, kaori.ActivityVersion);
        Assert.Equal(2UL, kaori.Rng["decide"]);
        Assert.Empty(result.FollowUps);
        Assert.Null(kaori.Overlay);
    }
}
