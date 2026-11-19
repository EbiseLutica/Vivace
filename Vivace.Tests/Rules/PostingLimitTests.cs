using Vivace.Core;
using Vivace.Core.Definitions.Model;
using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;
using Vivace.Tests.Support;

namespace Vivace.Tests.Rules;

/// <summary>投稿の行動の制限。スキップのあとの30分、サーバーの次の時刻、最後の投稿からの cooldown を、主でも副でも同じに見る。</summary>
public class PostingLimitTests
{
    private static readonly ActionDef Post = Fixture.Context.Defs.Actions["post_sns"];
    private static readonly ActionDef Read = Fixture.Context.Defs.Actions["read_book"];
    private static readonly PlaceId KaoriRoom = new("kaori_room");

    /// <summary>サーバーが返した次に投稿できる時刻より前は、投稿を候補にしない。その時刻ちょうどで候補に戻る。</summary>
    [Fact]
    public void RemoteNotBeforeBlocksPostUntilThatTime()
    {
        var remoteNotBefore = Fixture.Now.AddMinutes(10);
        var posting = new PostingState(null, null, remoteNotBefore, []);

        Assert.False(ActionRules.WithinPostingLimits(Post, posting, remoteNotBefore.AddMinutes(-1)));
        Assert.True(ActionRules.WithinPostingLimits(Post, posting, remoteNotBefore));
    }

    /// <summary>スキップの直後は、投稿を候補にしない。30分ちょうどで候補に戻る（30分未満は含まない）。</summary>
    [Fact]
    public void SkipBlocksPostUntilThirtyMinutesPass()
    {
        var posting = new PostingState(null, Fixture.Now, null, []);

        Assert.False(ActionRules.WithinPostingLimits(Post, posting, Fixture.Now.AddMinutes(29).AddSeconds(59)));
        Assert.True(ActionRules.WithinPostingLimits(Post, posting, Fixture.Now.AddMinutes(30)));
    }

    /// <summary>最後の投稿から、行動の cooldown（60分）の間は候補にしない。ちょうど60分で候補に戻る。</summary>
    [Fact]
    public void LastPostCooldownIsMeasuredFromLastPostAt()
    {
        var posting = new PostingState(Fixture.Now, null, null, []);

        Assert.Equal(TimeSpan.FromMinutes(60), Post.Limits.Cooldown);
        Assert.False(ActionRules.WithinPostingLimits(Post, posting, Fixture.Now.AddMinutes(59)));
        Assert.True(ActionRules.WithinPostingLimits(Post, posting, Fixture.Now.AddMinutes(60)));
    }

    /// <summary>投稿以外の行動は、投稿の状態によらず満たす。</summary>
    [Fact]
    public void OtherActionsIgnorePostingState()
    {
        var posting = new PostingState(Fixture.Now, Fixture.Now, null, []);

        Assert.True(ActionRules.WithinPostingLimits(Read, posting, Fixture.Now.AddMinutes(1)));
    }

    /// <summary>スキップのあとは、行動の選べる判定（候補の実行できること）でも投稿を外す。</summary>
    [Fact]
    public void SkipAlsoMakesPostNotChoosable()
    {
        var skipped = Fixture.Apply(Fixture.WithKaori(), Fixture.Event(10, Fixture.Now, EventTypes.PostSkipped, new PostSkippedPayload(PostSkippedPayload.PostSkipReason.AllRejected, null), Fixture.Kaori)).State;
        var persona = skipped.Personas[Fixture.Kaori];

        Assert.False(ActionRules.CanChoose(Fixture.Context, skipped, persona, "post_sns", KaoriRoom, Fixture.Now.AddMinutes(10)));
        Assert.True(ActionRules.CanChoose(Fixture.Context, skipped, persona, "post_sns", KaoriRoom, Fixture.Now.AddMinutes(30)));
    }
}
