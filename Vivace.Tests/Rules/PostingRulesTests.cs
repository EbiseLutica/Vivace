using Vivace.Core;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;
using Vivace.Tests.Support;

namespace Vivace.Tests.Rules;

/// <summary>投稿の記録。発話は最後の投稿と直近の投稿を、投稿のスキップは最後のスキップの時刻を更新する。</summary>
public class PostingRulesTests
{
    /// <summary>投稿の発話を当てると、最後の投稿の時刻と直近の投稿が更新される。</summary>
    [Fact]
    public void SpeechUpdatesLastPostAndRecent()
    {
        var at = Fixture.Now.AddMinutes(5);
        var result = Fixture.Apply(Fixture.WithKaori(), Speech(10, at, "今日の授業眠い"));

        Assert.Equal(EventStatus.Applied, result.Status);
        var posting = result.State.Personas[Fixture.Kaori].Posting;
        Assert.Equal(at, posting.LastPostAt);
        Assert.Null(posting.LastSkippedAt);
        var recent = Assert.Single(posting.Recent);
        Assert.Equal(new RecentPost(at, "今日の授業眠い"), recent);
    }

    /// <summary>直近の投稿は、新しいものから RecentLimit 件だけ残す。古いものが外れる。</summary>
    [Fact]
    public void RecentPostsKeepOnlyTheLatestFive()
    {
        var state = Fixture.WithKaori();
        for (var i = 0; i < PostingState.RecentLimit + 1; i++)
        {
            state = Fixture.Apply(state, Speech(10 + i, Fixture.Now.AddMinutes(i), $"独り言{i}")).State;
        }

        var recent = state.Personas[Fixture.Kaori].Posting.Recent;
        Assert.Equal(PostingState.RecentLimit, recent.Count);
        Assert.Equal("独り言1", recent[0].Body);
        Assert.Equal($"独り言{PostingState.RecentLimit}", recent[^1].Body);
    }

    /// <summary>投稿のスキップは、最後のスキップの時刻だけを更新する。投稿の記録は変えない。</summary>
    [Fact]
    public void SkipUpdatesOnlyLastSkippedAt()
    {
        var at = Fixture.Now.AddMinutes(7);
        var skipped = Fixture.Event(10, at, EventTypes.PostSkipped, new PostSkippedPayload(PostSkippedPayload.PostSkipReason.GenerationFailed, null), Fixture.Kaori);

        var posting = Fixture.Apply(Fixture.WithKaori(), skipped).State.Personas[Fixture.Kaori].Posting;

        Assert.Equal(at, posting.LastSkippedAt);
        Assert.Null(posting.LastPostAt);
        Assert.Empty(posting.Recent);
    }

    /// <summary>ペルソナがいなければ、発話も投稿のスキップも捨てる。</summary>
    [Fact]
    public void PostingForMissingPersonaIsSkipped()
    {
        var speech = Fixture.Apply(Fixture.WithKaori(), Speech(10, Fixture.Now, "独り言", persona: Fixture.Shiori));
        var skipped = Fixture.Apply(Fixture.WithKaori(), Fixture.Event(11, Fixture.Now, EventTypes.PostSkipped, new PostSkippedPayload(PostSkippedPayload.PostSkipReason.AllRejected, null), Fixture.Shiori));

        Assert.Equal(EventStatus.Skipped, speech.Status);
        Assert.Equal("ペルソナがない", speech.Reason);
        Assert.Equal(EventStatus.Skipped, skipped.Status);
        Assert.Equal("ペルソナがない", skipped.Reason);
    }

    /// <summary>投稿の発話を作る。送信の中身は空でよい（Apply は本文と時刻だけを見る）。</summary>
    private static SimEvent Speech(long seq, DateTimeOffset at, string body, PersonaId? persona = null) =>
        Fixture.Event(seq, at, EventTypes.OutboundSpeech, new OutboundSpeechPayload(
            "console",
            OutboundSpeechPayload.OutboundPostKind.Post,
            body,
            null,
            Audience.Public,
            [seq],
            default,
            null), persona ?? Fixture.Kaori);
}
