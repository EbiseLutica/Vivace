using Vivace.Core.Decision;
using Vivace.Core.Domain.State;
using Vivace.Core.Domain;
using Vivace.Core.Events.Guards;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;
using Vivace.Core.Randomness;
using Vivace.Core;
using Vivace.Tests.Support;

namespace Vivace.Tests.Decision;

/// <summary>
/// スマホを開く機会の決定。候補は「投稿する」（sns.post）と「何もしない」だけ。投稿はブロックの残りで切る。
/// </summary>
public class PhoneDeciderTests
{
    private static readonly PlaceId KaoriRoom = new("kaori_room");
    private const string Block = "reading@2026-11-01";
    private const string Post = "post_sns";
    private const int Positions = 40;

    /// <summary>読書の固定ブロック（残りが length）の中で、読書を始めた香織の状態。</summary>
    private static SimState InBlock(TimeSpan length)
    {
        var plan = new DayPlan(
            1,
            Fixture.Now,
            Fixture.Now.AddHours(24),
            Fixture.Now.AddHours(21),
            PlanSource.Fallback,
            [new PlanBlock(Block, BlockKind.Fixed, Fixture.Now, Fixture.Now + length, "read_book", KaoriRoom, PhonePolicy.Open, null, [], PartnerAbsentPolicy.Continue)],
            null);
        var committed = Fixture.Event(10, Fixture.Now, EventTypes.PlanCommitted, new PlanCommittedPayload(plan), Fixture.Kaori, new Guard([new PlanVersionCheck(0)], RejectAction.Drop));
        var state = Fixture.Apply(Fixture.WithKaori(), committed).State;
        var started = Fixture.Event(11, Fixture.Now, EventTypes.ActionStarted, new ActionStartedPayload("read_book", KaoriRoom, length, ActivityReason.Block, 0, Block), Fixture.Kaori);
        return Fixture.Apply(state, started).State;
    }

    /// <summary>位置を指定して、スマホを開く機会の決定を1回する。</summary>
    private static DeciderOutput Decide(SimState state, ulong position)
    {
        var random = new SplitMixRandomSource().Open(state.Worlds[Fixture.World].Seed, Fixture.Kaori.Value, "decide", position);
        return new UtilityDecider().Decide(new DecisionContext(state, Fixture.Kaori, Fixture.Now, DecisionReason.Phone, Block, Fixture.Context, random));
    }

    /// <summary>位置を変えて、決定を集める。</summary>
    private static List<DeciderOutput> Outputs(SimState state) =>
        Enumerable.Range(0, Positions).Select(position => Decide(state, (ulong)position)).ToList();

    /// <summary>候補は投稿と何もしないだけ。どちらも副行動として、いまの場所のまま（移動なし）。何もしないは所要時間 0。</summary>
    [Fact]
    public void CandidatesAreOnlyPostAndIdle()
    {
        var outputs = Outputs(InBlock(TimeSpan.FromHours(1)));

        Assert.All(outputs, output => Assert.Contains(output.Payload.ActionId, new[] { Post, UtilityDecider.Idle }));
        Assert.Contains(outputs, output => output.Payload.ActionId == Post);
        Assert.All(outputs, output =>
        {
            Assert.True(output.Payload.IsSubAction);
            Assert.Equal(KaoriRoom, output.Payload.Place);
            Assert.Equal(0, output.Payload.TravelMinutes);
            Assert.True(output.Payload.Route.IsEmpty);
        });
        Assert.All(
            outputs.Where(output => output.Payload.ActionId == UtilityDecider.Idle),
            output => Assert.Equal(TimeSpan.Zero, output.Payload.Duration));
    }

    /// <summary>残りが投稿の最短（3 分）より短くても、投稿は残りで切って候補に残る。残り 2 分なら、どの投稿も 2 分になる。</summary>
    [Fact]
    public void ShortRemainderCutsPostToRemainder()
    {
        var outputs = Outputs(InBlock(TimeSpan.FromMinutes(2)));

        Assert.Contains(outputs, output => output.Payload.ActionId == Post);
        Assert.All(outputs.Where(output => output.Payload.ActionId == Post), output => Assert.Equal(TimeSpan.FromMinutes(2), output.Payload.Duration));
    }

    /// <summary>行動の制限（投稿のクールダウン 60 分）は、副行動の投稿にも効く。10 分前に投稿を始めていれば外れ、61 分前なら候補に戻る。</summary>
    [Fact]
    public void ActionCooldownAppliesToPhonePost()
    {
        var recent = WithLastStarted(InBlock(TimeSpan.FromHours(1)), Fixture.Now.AddMinutes(-10));
        var old = WithLastStarted(InBlock(TimeSpan.FromHours(1)), Fixture.Now.AddMinutes(-61));

        Assert.All(Outputs(recent), output => Assert.Equal(UtilityDecider.Idle, output.Payload.ActionId));
        Assert.Contains(Outputs(old), output => output.Payload.ActionId == Post);
    }

    /// <summary>投稿の行動の、最後に始めた時刻を書き換えた状態。</summary>
    private static SimState WithLastStarted(SimState state, DateTimeOffset at)
    {
        var persona = state.Personas[Fixture.Kaori];
        var usage = new ActionUsage(at, new DateOnly(2026, 11, 2), 1, 0);
        return Fixture.With(state, persona with { Usage = persona.Usage.SetItem(Post, usage) });
    }

    /// <summary>
    /// 出力が guard で却下されると（ここでは行動のバージョンが違う）、決定中を下ろし、ブロックの行動は残し、決め直しを積む。
    /// 却下の動きが Redecide である必要がある（Drop だと決定中のまま残る）。
    /// </summary>
    [Fact]
    public void RejectedPhoneDecisionClearsDecidingAndKeepsBlockAction()
    {
        var state = InBlock(TimeSpan.FromHours(1));
        var request = Fixture.Event(12, Fixture.Now, EventTypes.DecisionRequested, new DecisionRequestedPayload(DecisionReason.Phone, Block), Fixture.Kaori);
        var deciding = Fixture.Apply(state, request).State;
        var output = Decide(deciding, 0);
        var stale = Fixture.With(deciding, deciding.Personas[Fixture.Kaori] with { ActivityVersion = 1 });

        var result = Fixture.Apply(stale, Fixture.Event(13, Fixture.Now, EventTypes.ActivityChosen, output.Payload, Fixture.Kaori, output.Guard, 12));

        Assert.Equal(EventStatus.Rejected, result.Status);
        var kaori = result.State.Personas[Fixture.Kaori];
        Assert.False(kaori.Deciding);
        Assert.Equal("read_book", kaori.Activity!.ActionId);
        Assert.Equal(new DecisionRequestedPayload(DecisionReason.Retry, null), Assert.Single(result.FollowUps).Payload);
    }
}
