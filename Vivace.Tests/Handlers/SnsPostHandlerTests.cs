using Vivace.Core;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Events.Guards;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Handlers;
using Vivace.Core.Signals;
using Vivace.Testing;
using Vivace.Tests.Support;

namespace Vivace.Tests.Handlers;

/// <summary>投稿の文面の呼び出し。入力に人物と世界の lore、時刻、行動と場所、直近の投稿が入ること、空の本文はスキップになること、出力に行動のバージョンの検査が付くこと。</summary>
public class SnsPostHandlerTests
{
    private static readonly PlaceId KaoriRoom = new("kaori_room");

    /// <summary>入力に、人物の lore の一節が入る。入っていなければ台本に当たらず、スキップになる。</summary>
    [Fact]
    public async Task RequestCarriesPersonaLore()
    {
        var draft = await Run(new ScriptedLlm().Reply("post.compose", "今日の授業眠い", contains: "図書委員"));

        Assert.Equal(EventTypes.OutboundSpeech, draft.EventType);
    }

    /// <summary>入力に、世界の lore の一節が入る。</summary>
    [Fact]
    public async Task RequestCarriesWorldLore()
    {
        var draft = await Run(new ScriptedLlm().Reply("post.compose", "今日の授業眠い", contains: "テスト用の小さな町"));

        Assert.Equal(EventTypes.OutboundSpeech, draft.EventType);
    }

    /// <summary>入力に、現地の時刻が "yyyy-MM-dd HH:mm" で入る。出力の時刻（行動の終わり、現地 23:08）。</summary>
    [Fact]
    public async Task RequestCarriesLocalTime()
    {
        var draft = await Run(new ScriptedLlm().Reply("post.compose", "今日の授業眠い", contains: "いまの時刻：2026-11-01 23:08"));

        Assert.Equal(EventTypes.OutboundSpeech, draft.EventType);
    }

    /// <summary>入力に、直近の投稿の本文が入る（直近の投稿は「- 本文」の行）。</summary>
    [Fact]
    public async Task RequestCarriesRecentPosts()
    {
        var state = Speech(Fixture.WithKaori(), "前の独り言");

        var draft = await Run(new ScriptedLlm().Reply("post.compose", "今日の授業眠い", contains: "- 前の独り言"), state);

        Assert.Equal(EventTypes.OutboundSpeech, draft.EventType);
    }

    /// <summary>直近の投稿がないときは「- まだない」と書く。</summary>
    [Fact]
    public async Task RequestSaysNoRecentPostsYet()
    {
        var draft = await Run(new ScriptedLlm().Reply("post.compose", "今日の授業眠い", contains: "- まだない"));

        Assert.Equal(EventTypes.OutboundSpeech, draft.EventType);
    }

    /// <summary>入力に、いまの行動と場所（表示名）が入る。</summary>
    [Fact]
    public async Task RequestCarriesActionAndPlaceNames()
    {
        var draft = await Run(new ScriptedLlm().Reply("post.compose", "今日の授業眠い", contains: "いまの行動：SNS に投稿する\nいる場所：香織の部屋"));

        Assert.Equal(EventTypes.OutboundSpeech, draft.EventType);
    }

    /// <summary>本文は前後の空白を取り除いて、発話にする。出力の時刻は行動の終わり、guard は行動のバージョン。</summary>
    [Fact]
    public async Task BodyIsTrimmedAndGuardedByActivityVersion()
    {
        var llm = new ScriptedLlm().Reply("post.compose", "  今日の授業眠い \n");

        var draft = await SnsPostHandler.RunAsync(Fixture.Context, llm, Fixture.WithKaori(), Effect());

        var speech = Assert.IsType<OutboundSpeechPayload>(draft.Payload);
        Assert.Equal("今日の授業眠い", speech.Body);
        Assert.Equal(Fixture.Now.AddMinutes(8), draft.Time);
        Assert.Equal(10, draft.BatchSeq);
        Assert.NotNull(draft.Guard);
        var guard = draft.Guard!;
        Assert.Equal(RejectAction.Drop, guard.OnReject);
        Assert.Equal(new ActivityVersionCheck(0), Assert.Single(guard.Checks));
    }

    /// <summary>空の本文（空白だけ）は、発話にせず生成の失敗のスキップにする。</summary>
    [Fact]
    public async Task BlankBodyBecomesGenerationFailedSkip()
    {
        var draft = await Run(new ScriptedLlm().Reply("post.compose", " \n "));

        Assert.Equal(EventTypes.PostSkipped, draft.EventType);
        Assert.Equal(PostSkippedPayload.PostSkipReason.GenerationFailed, ((PostSkippedPayload)draft.Payload).Reason);
    }

    /// <summary>行動のバージョンが変わった後に届いた発話は、guard で却下される（Apply の検査）。</summary>
    [Fact]
    public void SpeechFromAnOlderActivityIsRejected()
    {
        var state = Fixture.WithKaori();
        var stale = Fixture.Event(10, Fixture.Now, EventTypes.OutboundSpeech,
            new OutboundSpeechPayload("console", OutboundSpeechPayload.OutboundPostKind.Post, "古い独り言", null, Audience.Public, [], default, null),
            Fixture.Kaori, new Guard([new ActivityVersionCheck(7)], RejectAction.Drop));

        var result = Fixture.Apply(state, stale);

        Assert.Equal(EventStatus.Rejected, result.Status);
        Assert.Equal("行動のバージョンが違う", result.Reason);
        Assert.Null(result.State.Personas[Fixture.Kaori].Posting.LastPostAt);
    }

    /// <summary>既定の状態（香織が、所要時間 8 分の投稿を、行動のバージョン 0 で始めた）で、文面の呼び出しを1回する。</summary>
    private static Task<EventDraft> Run(ScriptedLlm llm, SimState? state = null) =>
        SnsPostHandler.RunAsync(Fixture.Context, llm, state ?? Fixture.WithKaori(), Effect());

    /// <summary>テスト用の起動のシグナル。</summary>
    private static StartEffectSignal Effect() =>
        new(10, Fixture.Kaori, Fixture.Now.AddMinutes(8), SnsPostHandler.Name, "post_sns", KaoriRoom, 0);

    /// <summary>香織の発話を1件適用した状態。</summary>
    private static SimState Speech(SimState state, string body) =>
        Fixture.Apply(state, Fixture.Event(9, Fixture.Now, EventTypes.OutboundSpeech,
            new OutboundSpeechPayload("console", OutboundSpeechPayload.OutboundPostKind.Post, body, null, Audience.Public, [], default, null),
            Fixture.Kaori)).State;
}
