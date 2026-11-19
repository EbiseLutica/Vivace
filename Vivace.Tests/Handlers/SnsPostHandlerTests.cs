using Vivace.Core;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Events.Guards;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Handlers;
using Vivace.Core.Llm;
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

    /// <summary>副行動の投稿は、スマホを開ける主の行動（テレビを見る）の表示名を「いまの行動」に入れる。投稿の行動の名前は入らない。</summary>
    [Fact]
    public async Task OverlayPostCarriesMainActivityName()
    {
        var state = Start(Start(Fixture.WithKaori(), 3, Fixture.Now, "watch_tv"), 4, Fixture.Now, "post_sns", sub: true);

        var user = await UserMessageOf(state);

        Assert.Contains("いまの行動：テレビを見る\nいる場所：香織の部屋", user, StringComparison.Ordinal);
        Assert.DoesNotContain("直前の行動", user, StringComparison.Ordinal);
    }

    /// <summary>主の行動がスマホを開けない（授業を受ける）ときの副行動の投稿は、「いまの行動」に「授業を受けるの合間」が入る。</summary>
    [Fact]
    public async Task OverlayPostDuringClosedActivityCarriesInterlude()
    {
        var state = Start(Start(Fixture.WithKaori(), 3, Fixture.Now, "attend_class"), 4, Fixture.Now, "post_sns", sub: true);

        var user = await UserMessageOf(state);

        Assert.Contains("いまの行動：授業を受けるの合間\nいる場所：香織の部屋", user, StringComparison.Ordinal);
    }

    /// <summary>主の行動の投稿は、「いまの行動」の行を出さず、直前の行動（最後の開始が最も新しいもの）を出す。投稿の行動の名前は入らない。</summary>
    [Fact]
    public async Task MainPostCarriesPreviousActivityInsteadOfItself()
    {
        var at = Fixture.Now;
        var state = Start(Fixture.WithKaori(), 3, at, "read_book");
        state = End(state, 4, at.AddMinutes(30), "read_book");
        state = Start(state, 5, at.AddMinutes(30), "watch_tv");
        state = End(state, 6, at.AddMinutes(60), "watch_tv");
        state = Start(state, 7, at.AddMinutes(60), "post_sns");
        Assert.Equal("post_sns", state.Personas[Fixture.Kaori].Activity?.ActionId);

        var user = await UserMessageOf(state);

        Assert.Contains("直前の行動：テレビを見る\nいる場所：香織の部屋", user, StringComparison.Ordinal);
        Assert.DoesNotContain("いまの行動", user, StringComparison.Ordinal);
        Assert.DoesNotContain("SNS に投稿する", user, StringComparison.Ordinal);
    }

    /// <summary>システムの指示（System）に、ここに書いていない人の様子や居場所は書かないという一行が入る。メッセージ側には入らない。</summary>
    [Fact]
    public async Task SystemForbidsInventingOthersState()
    {
        var llm = new RecordingLlm();

        await SnsPostHandler.RunAsync(Fixture.Context, llm, Fixture.WithKaori(), Effect(), CancellationToken.None);

        var request = Assert.Single(llm.Requests);
        Assert.Contains("ここに書いていない人の、いまの様子や居場所は書きません。", request.System, StringComparison.Ordinal);
    }

    /// <summary>本文は前後の空白を取り除いて、発話にする。出力の時刻は行動の終わり、guard は行動のバージョン。</summary>
    [Fact]
    public async Task BodyIsTrimmedAndGuardedByActivityVersion()
    {
        var llm = new ScriptedLlm().Reply("post.compose", "  今日の授業眠い \n");

        var draft = await SnsPostHandler.RunAsync(Fixture.Context, llm, Fixture.WithKaori(), Effect(), CancellationToken.None);

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
        SnsPostHandler.RunAsync(Fixture.Context, llm, state ?? Fixture.WithKaori(), Effect(), CancellationToken.None);

    /// <summary>要求を記録して、固定の本文を返す LLM。</summary>
    private sealed class RecordingLlm : ILlmClient
    {
        /// <summary>受け取った要求の列。</summary>
        public List<LlmRequest> Requests { get; } = [];

        /// <summary>要求を記録し、固定の本文を返す。</summary>
        /// <param name="request">呼び出しの内容。</param>
        /// <param name="ct">キャンセル（使わない）。</param>
        public Task<LlmResult<string>> GenerateTextAsync(LlmRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(new LlmResult<string>("今日の授業眠い", new LlmUsage("recording", 0, 0)));
        }
    }

    /// <summary>テスト用の起動のシグナル。</summary>
    private static StartEffectSignal Effect() =>
        new(10, Fixture.Kaori, Fixture.Now.AddMinutes(8), "post_sns", KaoriRoom, 0);

    /// <summary>文面の呼び出しを1回して、メッセージの本文を返す。</summary>
    private static async Task<string> UserMessageOf(SimState state)
    {
        var llm = new RecordingLlm();
        await SnsPostHandler.RunAsync(Fixture.Context, llm, state, Effect(), CancellationToken.None);
        return Assert.Single(Assert.Single(llm.Requests).Messages).Content;
    }

    /// <summary>香織の部屋で行動を1件始める。sub なら副行動として Overlay に入る（行動のバージョン 0、所要時間 30 分）。</summary>
    private static SimState Start(SimState state, long seq, DateTimeOffset at, string actionId, bool sub = false) =>
        Fixture.Apply(state, Fixture.Event(seq, at, EventTypes.ActionStarted,
            new ActionStartedPayload(actionId, KaoriRoom, TimeSpan.FromMinutes(30), ActivityReason.Chosen, 0, null, sub), Fixture.Kaori)).State;

    /// <summary>行動を1件終える。</summary>
    private static SimState End(SimState state, long seq, DateTimeOffset at, string actionId) =>
        Fixture.Apply(state, Fixture.Event(seq, at, EventTypes.ActionEnded, new ActionEndedPayload(actionId, 0), Fixture.Kaori)).State;

    /// <summary>香織の発話を1件適用した状態。</summary>
    private static SimState Speech(SimState state, string body) =>
        Fixture.Apply(state, Fixture.Event(9, Fixture.Now, EventTypes.OutboundSpeech,
            new OutboundSpeechPayload("console", OutboundSpeechPayload.OutboundPostKind.Post, body, null, Audience.Public, [], default, null),
            Fixture.Kaori)).State;
}
