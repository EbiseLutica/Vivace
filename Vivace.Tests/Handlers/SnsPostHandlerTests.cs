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

/// <summary>投稿の文面の呼び出し。入力に場所と行動が入ること、空の本文はスキップになること、出力に行動のバージョンの検査が付くこと。</summary>
public class SnsPostHandlerTests
{
    private static readonly PlaceId KaoriRoom = new("kaori_room");

    /// <summary>入力に、いまの行動と場所（表示名）が入る。入っていなければ台本に当たらず、スキップになる。</summary>
    [Fact]
    public async Task RequestCarriesActionAndPlaceNames()
    {
        var llm = new ScriptedLlm().Reply("post.compose", "今日の授業眠い", contains: "いまの行動：SNS に投稿する\nいる場所：香織の部屋");

        var draft = await SnsPostHandler.RunAsync(Fixture.Context, llm, Fixture.WithKaori(), Effect());

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
        var llm = new ScriptedLlm().Reply("post.compose", " \n ");

        var draft = await SnsPostHandler.RunAsync(Fixture.Context, llm, Fixture.WithKaori(), Effect());

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

    /// <summary>テスト用の起動のシグナル（香織が、所要時間 8 分の投稿を、行動のバージョン 0 で始めた）。</summary>
    private static StartEffectSignal Effect() =>
        new(10, Fixture.Kaori, Fixture.Now.AddMinutes(8), SnsPostHandler.Name, "post_sns", KaoriRoom, 0);
}
