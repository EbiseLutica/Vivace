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
/// requires に場所のタグがない行動は、自由時間の候補で、いまの場所だけになる。移動してまでする理由がないため。
/// </summary>
public class PlacelessActionTests
{
    private static readonly PlaceId TaroRoom = new("taro_room");
    private const string Post = "post_sns";
    private const int Positions = 40;

    /// <summary>太郎が太郎の部屋にいて、3 時間の自由時間の中にいる状態。</summary>
    private static SimState FreeThreeHours()
    {
        var plan = new DayPlan(
            1,
            Fixture.Now,
            Fixture.Now.AddHours(24),
            Fixture.Now.AddHours(21),
            PlanSource.Fallback,
            [new PlanBlock(null, BlockKind.Free, Fixture.Now, Fixture.Now.AddHours(3), null, null, PhonePolicy.Closed, null, [], PartnerAbsentPolicy.Continue)],
            null);
        var committed = Fixture.Event(10, Fixture.Now, EventTypes.PlanCommitted, new PlanCommittedPayload(plan), Fixture.Taro, new Guard([new PlanVersionCheck(0)], RejectAction.Drop));
        return Fixture.Apply(Fixture.WithTaro(), committed).State;
    }

    /// <summary>位置を指定して、自由時間の決定を1回する。</summary>
    private static DeciderOutput Decide(SimState state, ulong position)
    {
        var random = new SplitMixRandomSource().Open(state.Worlds[Fixture.World].Seed, Fixture.Taro.Value, "decide", position);
        return new UtilityDecider().Decide(new DecisionContext(state, Fixture.Taro, Fixture.Now, DecisionReason.Free, null, Fixture.Context, random));
    }

    /// <summary>投稿（場所のタグなし）は、どの位置の決定でも太郎の部屋で、ほかの場所には出ない。投稿は選ばれる候補として残る。</summary>
    [Fact]
    public void PlacelessActionIsOnlyCandidateAtCurrentPlace()
    {
        var state = FreeThreeHours();

        var posts = Enumerable.Range(0, Positions)
            .Select(position => Decide(state, (ulong)position))
            .Where(output => output.Payload.ActionId == Post)
            .ToList();

        Assert.NotEmpty(posts);
        Assert.All(posts, output => Assert.Equal(TaroRoom, output.Payload.Place));
    }
}
