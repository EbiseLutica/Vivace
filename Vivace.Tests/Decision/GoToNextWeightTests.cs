using Vivace.Core.Decision;
using Vivace.Core.Definitions;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Randomness;
using Vivace.Core.Routes;
using Vivace.Core.Rules;
using Vivace.Tests.Definitions;
using Vivace.Tests.Support;
using Vivace.Testing;

namespace Vivace.Tests.Decision;

/// <summary>
/// 次の予定の場所へ向かう行動の基本の重みと好み。Decider を通して、選ばれる頻度で確かめる。
/// </summary>
public class GoToNextWeightTests
{
    private const int Positions = 40;

    /// <summary>好みを定義で 1000 倍にすると、乱数の位置を変えても、ほぼ必ず次の予定の場所へ向かう行動が選ばれる。</summary>
    [Fact]
    public void StronglyLikedGoToNextIsChosenAlmostAlways()
    {
        var context = ContextFor(WithPreference("1000.0"));
        var state = FreeDay(context);

        var chosen = Choices(context, state);

        Assert.True(chosen.Count(id => id == UtilityDecider.GoToNext) >= Positions - 2, $"選ばれた回数: {chosen.Count(id => id == UtilityDecider.GoToNext)}");
    }

    /// <summary>好みを書かなければ、同じ位置の範囲で、次の予定の場所へ向かう行動以外も選ばれる（重みが 1.0 のまま、ほかと同じ扱い）。</summary>
    [Fact]
    public void WithoutPreferenceOtherActionsAreChosenToo()
    {
        var context = ContextFor(TestWorld.LoadOrThrow());
        var state = FreeDay(context);

        var chosen = Choices(context, state);

        Assert.Contains(chosen, id => id != UtilityDecider.GoToNext);
    }

    /// <summary>テスト用の町の香織の定義の好みを、次の予定の場所へ向かう行動について書き換えた定義。</summary>
    private static Vivace.Core.Definitions.Definitions WithPreference(string value)
    {
        var files = DefinitionFixtures.Replaced("personas/kaori.toml", "prefs = { read_book", $"prefs = {{ \"core.go_to_next\" = {value}, read_book");
        return DefinitionLoader.LoadFiles(files).Definitions ?? throw new InvalidOperationException("定義が読めない");
    }

    private static RuleContext ContextFor(Vivace.Core.Definitions.Definitions definitions) => new(definitions, RouteCache.Create(definitions));

    /// <summary>香織が家にいて、3 時間の自由時間のプランを持つ状態（この文脈の定義で作る）。</summary>
    private static SimState FreeDay(RuleContext context)
    {
        var state = Vivace.Core.Rules.Rules.Apply(context, Fixture.Empty(), Fixture.Event(1, Fixture.Now, EventTypes.WorldCreated, new WorldCreatedPayload(TestWorld.WorldId, 42), null)).State;
        state = Vivace.Core.Rules.Rules.Apply(context, state, Fixture.Event(2, Fixture.Now, EventTypes.PersonaCreated, new PersonaCreatedPayload("kaori", new PlaceId("kaori_room")), null)).State;
        var plan = new DayPlan(
            1,
            Fixture.Now,
            Fixture.Now.AddHours(3),
            Fixture.Now.AddHours(3),
            PlanSource.Fallback,
            [new PlanBlock(null, BlockKind.Free, Fixture.Now, Fixture.Now.AddHours(3), null, null, PhonePolicy.Closed, null, [], PartnerAbsentPolicy.Continue)],
            null);
        var committed = Fixture.Event(10, Fixture.Now, EventTypes.PlanCommitted, new PlanCommittedPayload(plan), Fixture.Kaori, new Guard([new PlanVersionCheck(0)], RejectAction.Drop));
        return Vivace.Core.Rules.Rules.Apply(context, state, committed).State;
    }

    /// <summary>乱数の位置を変えて、Decider が選んだ行動の ID を集める。</summary>
    private static List<string> Choices(RuleContext context, SimState state)
    {
        var choices = new List<string>();
        for (ulong position = 0; position < Positions; position++)
        {
            var random = new SplitMixRandomSource().Open(state.Worlds[Fixture.World].Seed, Fixture.Kaori.Value, "decide", position);
            var output = new UtilityDecider().Decide(new DecisionContext(state, Fixture.Kaori, Fixture.Now, DecisionReason.Free, null, context, random));
            choices.Add(output.Payload.ActionId);
        }

        return choices;
    }
}
