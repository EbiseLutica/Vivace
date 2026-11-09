using System.Collections.Immutable;
using Vivace.Core.Decision;
using Vivace.Core.Definitions;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Randomness;
using Vivace.Core.Rules;
using Vivace.Tests.Support;
using Vivace.Testing;

namespace Vivace.Tests.Decision;

/// <summary>
/// 自由時間の決定。欲求の倍率と重みは、設計書（D5 5.3）の計算例の数字で確かめる。候補と締め切りは、小さなプランで確かめる。
/// </summary>
public class UtilityDeciderTests
{
    private static readonly PlaceId KaoriRoom = new("kaori_room");
    private static readonly PlaceId Park = new("park");

    private static readonly ActionEffects NoEffects = new(
        ImmutableDictionary<VitalName, double>.Empty,
        ImmutableDictionary<VitalName, double>.Empty,
        ImmutableDictionary<VitalName, double>.Empty,
        ImmutableDictionary<VitalName, double>.Empty,
        null,
        null,
        null);

    private static ImmutableDictionary<VitalName, double> Map(params (VitalName Vital, double Value)[] entries) =>
        entries.ToImmutableDictionary(entry => entry.Vital, entry => entry.Value);

    private static Vitals VitalsWith(double energy, double boredom) => new(
        new VitalTrack(Fixture.Now, energy, 0),
        new VitalTrack(Fixture.Now, 100, 0),
        new VitalTrack(Fixture.Now, boredom, 0),
        new VitalTrack(Fixture.Now, 0, 0),
        1);

    /// <summary>設計書の計算例（D5 5.3）：14:00、気力 30、退屈度 70。家で昼寝（60 分、気力 +20/時）、公園を散歩（移動 15 分 + 45 分、気力 −6/時、終了時に退屈度 −25）、家でゲーム（60 分、終了時に退屈度 −30、好き）。</summary>
    [Fact]
    public void NeedMatchesDesignWorkedExample()
    {
        var definitions = TestWorld.LoadOrThrow();
        var kaoriDef = definitions.Personas["kaori"];
        var basis = definitions.Actions["read_book"];
        var state = Fixture.WithKaori();
        var kaori = state.Personas[Fixture.Kaori] with { Vitals = VitalsWith(30, 70) };
        state = Fixture.With(state, kaori);

        var nap = basis with
        {
            Duration = new ActionDuration(TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(60), false),
            Effects = NoEffects with { Rates = Map((VitalName.Energy, 20)) },
        };
        var walk = basis with
        {
            Duration = new ActionDuration(TimeSpan.FromMinutes(45), TimeSpan.FromMinutes(45), false),
            Effects = NoEffects with { Rates = Map((VitalName.Energy, -6)), OnEnd = Map((VitalName.Boredom, -25)) },
        };
        var game = basis with
        {
            Duration = new ActionDuration(TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(60), false),
            Effects = NoEffects with { OnEnd = Map((VitalName.Boredom, -30)) },
        };

        var napNeed = UtilityDecider.Need(state, kaori, kaoriDef, Fixture.Now, nap, KaoriRoom, 0, TimeSpan.FromMinutes(60), 0);
        var walkNeed = UtilityDecider.Need(state, kaori, kaoriDef, Fixture.Now, walk, Park, 15, TimeSpan.FromMinutes(45), 0);
        var gameNeed = UtilityDecider.Need(state, kaori, kaoriDef, Fixture.Now, game, KaoriRoom, 0, TimeSpan.FromMinutes(60), 0);

        Assert.Equal(1.28, napNeed, 2);
        Assert.Equal(1.27, walkNeed, 2);
        Assert.Equal(1.40, gameNeed, 2);
        var napWeight = UtilityDecider.Weight(1, 1, napNeed, 0, 0);
        var walkWeight = UtilityDecider.Weight(1, 1, walkNeed, 0, 15);
        var gameWeight = UtilityDecider.Weight(1, 2.0, gameNeed, 0, 0);
        Assert.Equal(0.58, gameWeight / (napWeight + walkWeight + gameWeight), 2);
    }

    /// <summary>重みは要素の積。同じ行動が続くと 0.7 を掛け、移動は 30 分ごとに e^−1 を掛ける（D5 5.3）。</summary>
    [Fact]
    public void WeightMultipliesFactors()
    {
        Assert.Equal(0.49, UtilityDecider.Weight(1.0, 1.0, 1.0, 2, 0), 9);
        Assert.Equal(Math.Exp(-1), UtilityDecider.Weight(1.0, 1.0, 1.0, 0, 30), 9);
        Assert.Equal(2.1, UtilityDecider.Weight(2.0, 1.0, 1.5, 1, 0), 9);
    }

    /// <summary>自由時間の決定。候補は行動 ID と場所 ID の順で並び、同じ写しと同じ乱数の位置なら、結果は毎回同じ。</summary>
    [Fact]
    public void DecisionIsDeterministicForSameStateAndSeed()
    {
        var state = FreeDay(Fixture.Now.AddHours(3));

        var first = Decide(state, 0);
        var second = Decide(state, 0);

        Assert.Equal(first.Payload.ActionId, second.Payload.ActionId);
        Assert.Equal(first.Payload.Place, second.Payload.Place);
        Assert.Equal(first.Payload.Duration, second.Payload.Duration);
        Assert.Equal(first.Payload.Draws, second.Payload.Draws);
    }

    /// <summary>候補の外すもの。スケジュールだけの行動（授業）と、今日の回数を使い切った行動（カフェ、1日2回）は、どの乱数の位置でも選ばれない。</summary>
    [Fact]
    public void ScheduleOnlyAndExhaustedActionsAreNeverChosen()
    {
        var state = FreeDay(Fixture.Now.AddHours(3));
        var kaori = state.Personas[Fixture.Kaori];
        var exhausted = kaori with
        {
            Usage = ImmutableDictionary<string, ActionUsage>.Empty.Add("cafe", new ActionUsage(null, new DateOnly(2026, 11, 1), 2, 0)),
        };
        state = Fixture.With(state, exhausted);

        for (ulong position = 0; position < 40; position++)
        {
            var chosen = Decide(state, position).Payload.ActionId;

            Assert.NotEqual("cafe", chosen);
            Assert.NotEqual("attend_class", chosen);
            Assert.NotEqual("eat_lunch", chosen);
        }
    }

    /// <summary>次の予定までに戻れない候補は残さない。次の予定が 10 分後なら、読書（30 分）は選べず、次の予定の場所へ向かうだけになる。</summary>
    [Fact]
    public void CandidatesMustFitBeforeNextBlock()
    {
        var state = FreeDayWithFixedBlockAt(Fixture.Now.AddMinutes(10));

        var result = Decide(state, 0);

        Assert.Equal(UtilityDecider.GoToNext, result.Payload.ActionId);
        Assert.Equal(TimeSpan.FromMinutes(10), result.Payload.Duration);
        Assert.Equal(new PlaceId("kaori_room"), result.Payload.Place);
    }

    /// <summary>出力の guard は、決めたときの行動の版、行動の実行の可否、乱数の位置を確かめる。却下されたら決め直す。</summary>
    [Fact]
    public void OutputGuardChecksVersionFeasibilityAndRng()
    {
        var state = FreeDay(Fixture.Now.AddHours(3));

        var output = Decide(state, 5);

        Assert.Equal(RejectAction.Redecide, output.Guard.OnReject);
        Assert.Equal(3, output.Guard.Checks.Length);
        Assert.Equal(new ActivityVersionCheck(0), output.Guard.Checks[0]);
        Assert.Equal(new FeasibleCheck(output.Payload.ActionId, output.Payload.Place), output.Guard.Checks[1]);
        Assert.Equal(new RngCursorCheck(Fixture.Kaori.Value, "decide", 5), output.Guard.Checks[2]);
        Assert.Equal(5UL, output.Payload.Draws!.From);
        Assert.True(output.Payload.Draws.To > 5);
    }

    /// <summary>自由時間の決定に使う状態。香織が家にいて、いまから until まで自由時間。</summary>
    private static SimState FreeDay(DateTimeOffset until)
    {
        var plan = new DayPlan(
            1,
            Fixture.Now,
            until,
            until,
            PlanSource.Fallback,
            [new PlanBlock(null, BlockKind.Free, Fixture.Now, until, null, null, PhonePolicy.Closed, null, [], PartnerAbsentPolicy.Continue)],
            null);
        return Commit(plan);
    }

    /// <summary>自由時間のあと、指定の時刻に読書の固定ブロックが始まるプラン。</summary>
    private static SimState FreeDayWithFixedBlockAt(DateTimeOffset start)
    {
        var plan = new DayPlan(
            1,
            Fixture.Now,
            start.AddHours(1),
            start.AddHours(1),
            PlanSource.Fallback,
            [
                new PlanBlock(null, BlockKind.Free, Fixture.Now, start, null, null, PhonePolicy.Closed, null, [], PartnerAbsentPolicy.Continue),
                new PlanBlock("reading@2026-11-01", BlockKind.Fixed, start, start.AddHours(1), "read_book", KaoriRoom, PhonePolicy.Closed, null, [], PartnerAbsentPolicy.Continue),
            ],
            null);
        return Commit(plan);
    }

    private static SimState Commit(DayPlan plan)
    {
        var committed = Fixture.Event(10, Fixture.Now, EventTypes.PlanCommitted, new PlanCommittedPayload(plan), Fixture.Kaori, new Guard([new PlanVersionCheck(0)], RejectAction.Drop));
        return Fixture.Apply(Fixture.WithKaori(), committed).State;
    }

    private static DeciderOutput Decide(SimState state, ulong position)
    {
        var persona = state.Personas[Fixture.Kaori];
        var random = new SplitMixRandomSource().Open(state.Worlds[Fixture.World].Seed, Fixture.Kaori.Value, "decide", position);
        var context = new DecisionContext(state, Fixture.Kaori, Fixture.Now, DecisionReason.Free, null, Fixture.Context, random);
        return new UtilityDecider().Decide(context);
    }
}
