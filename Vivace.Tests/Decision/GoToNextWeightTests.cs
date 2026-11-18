using Vivace.Core.Decision;
using Vivace.Core.Definitions.Model;
using Vivace.Core.Definitions;
using Vivace.Core.Domain.State;
using Vivace.Core.Domain;
using Vivace.Core.Events.Guards;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;
using Vivace.Core.Randomness;
using Vivace.Core.Routes;
using Vivace.Core;
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

    /// <summary>
    /// 次の予定の場所にいて、ほかに実行できる候補があるなら、好みを 1000 倍にしても、次の予定の場所へ向かう行動は候補に残らず選ばれない。
    /// </summary>
    [Fact]
    public void StronglyLikedGoToNextIsNotChosenWhileOtherActionsFit()
    {
        var context = ContextFor(WithPreference("1000.0"));
        var state = FreeDay(context);

        var chosen = Choices(context, state);

        Assert.DoesNotContain(UtilityDecider.GoToNext, chosen);
    }

    /// <summary>
    /// ほかの行動の重みがすべて 0 なら（候補には入るが、選ばれない）、次の予定の場所へ向かう行動（その場で待つ）が選ばれ、次の予定まで待つ。
    /// </summary>
    [Fact]
    public void WaitsWhenEveryOtherActionWeighsZero()
    {
        var context = ContextFor(WithZeroWeights());
        var state = FreeDay(context);

        var chosen = Choices(context, state);

        Assert.All(chosen, id => Assert.Equal(UtilityDecider.GoToNext, id));
        var output = new UtilityDecider().Decide(new DecisionContext(state, Fixture.Kaori, Fixture.Now, DecisionReason.Free, null, context, new SplitMixRandomSource().Open(state.Worlds[Fixture.World].Seed, Fixture.Kaori.Value, "decide", 0)));
        Assert.Equal(TimeSpan.FromHours(3), output.Payload.Duration);
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

    /// <summary>
    /// 次の予定が別の場所（家）にあり、移動ありの次の予定の場所へ向かう行動が候補に入る。好みを 1000 倍にすると、ほぼ必ず選ばれる。
    /// 香織は学校にいて、3 時間後に家の場所の読書の予定がある。学校から家までは 19 分（school_2a → street 15、street → home_living 3、home_living → kaori_room 1）。
    /// </summary>
    [Fact]
    public void StronglyLikedGoToNextIsChosenWhileMoving()
    {
        var context = ContextFor(WithPreference("1000.0"));
        var state = MovingDay(context);

        var chosen = Choices(context, state);

        Assert.True(chosen.Count(id => id == UtilityDecider.GoToNext) >= Positions - 2, $"選ばれた回数: {chosen.Count(id => id == UtilityDecider.GoToNext)}");
    }

    /// <summary>学校にいる香織の状態。3 時間後に家の場所の読書の予定があり、その前の時間は自由。</summary>
    private static SimState MovingDay(RuleContext context)
    {
        var start = Fixture.Now.AddHours(3);
        var plan = new DayPlan(
            1,
            Fixture.Now,
            start.AddHours(1),
            start.AddHours(1),
            PlanSource.Fallback,
            [
                new PlanBlock(null, BlockKind.Free, Fixture.Now, start, null, null, PhonePolicy.Closed, null, [], PartnerAbsentPolicy.Continue),
                new PlanBlock("reading@2026-11-01", BlockKind.Fixed, start, start.AddHours(1), "read_book", new PlaceId("kaori_room"), PhonePolicy.Closed, null, [], PartnerAbsentPolicy.Continue),
            ],
            null);
        var state = FreeDayWorld(context);
        var committed = Fixture.Event(10, Fixture.Now, EventTypes.PlanCommitted, new PlanCommittedPayload(plan), Fixture.Kaori, new Guard([new PlanVersionCheck(0)], RejectAction.Drop));
        state = new EventApplier(context).Apply(state, committed).State;
        return Fixture.With(state, state.Personas[Fixture.Kaori] with { Location = new AtPlace(new PlaceId("school_2a")) });
    }

    /// <summary>テスト用の町の香織の定義の好みを、次の予定の場所へ向かう行動について書き換えた定義。</summary>
    private static DefinitionPack WithPreference(string value)
    {
        var files = DefinitionFixtures.Replaced("personas/kaori.toml", "prefs = { read_book", $"prefs = {{ \"core.go_to_next\" = {value}, read_book");
        return DefinitionLoader.LoadFiles(files).Definitions ?? throw new InvalidOperationException("定義が読めません。");
    }

    /// <summary>テスト用の町の行動を、すべて重み 0 にした定義（候補には入るが、選ばれない）。</summary>
    private static DefinitionPack WithZeroWeights()
    {
        var files = TestWorld.Files().ToList();
        var index = files.FindIndex(file => file.Path == "actions/actions.toml");
        var text = files[index].Text
            .Replace("weight = 1.0", string.Empty, StringComparison.Ordinal)
            .Replace("[[action]]", "[[action]]\nweight = 0.0", StringComparison.Ordinal);
        files[index] = files[index] with { Text = text };
        return DefinitionLoader.LoadFiles(files).Definitions ?? throw new InvalidOperationException("定義が読めません。");
    }

    /// <summary>定義から規則のコンテキストを作る。</summary>
    private static RuleContext ContextFor(DefinitionPack definitions) => new(definitions, RouteCache.Create(definitions));

    /// <summary>香織が家にいて、3 時間の自由時間のプランを持つ状態（このコンテキストの定義で作る）。</summary>
    private static SimState FreeDay(RuleContext context)
    {
        var state = FreeDayWorld(context);
        var plan = new DayPlan(
            1,
            Fixture.Now,
            Fixture.Now.AddHours(3),
            Fixture.Now.AddHours(3),
            PlanSource.Fallback,
            [new PlanBlock(null, BlockKind.Free, Fixture.Now, Fixture.Now.AddHours(3), null, null, PhonePolicy.Closed, null, [], PartnerAbsentPolicy.Continue)],
            null);
        var committed = Fixture.Event(10, Fixture.Now, EventTypes.PlanCommitted, new PlanCommittedPayload(plan), Fixture.Kaori, new Guard([new PlanVersionCheck(0)], RejectAction.Drop));
        return new EventApplier(context).Apply(state, committed).State;
    }

    /// <summary>ワールドと香織（家は kaori_room）を作っただけの状態。</summary>
    private static SimState FreeDayWorld(RuleContext context)
    {
        var state = new EventApplier(context).Apply(Fixture.Empty(), Fixture.Event(1, Fixture.Now, EventTypes.WorldCreated, new WorldCreatedPayload(TestWorld.DefId, 42), null)).State;
        return new EventApplier(context).Apply(state, Fixture.Event(2, Fixture.Now, EventTypes.PersonaCreated, new PersonaCreatedPayload(new PersonaDefId("kaori"), new PlaceId("kaori_room")), null)).State;
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
