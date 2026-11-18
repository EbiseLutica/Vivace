using System.Collections.Immutable;
using Vivace.Core.Definitions.Model;
using Vivace.Core.Domain.State;
using Vivace.Core.Domain;
using Vivace.Core.Events.Guards;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;
using Vivace.Core.Handlers;
using Vivace.Core.Randomness;
using Vivace.Core.Routes;

namespace Vivace.Core.Decision;

/// <summary>
/// 自由時間の行動を、重みつきの抽選で決める。重みは、行動の基本の重み、好み、欲求の倍率、移動の手間などの積。
/// 同じスナップショットと同じ乱数の位置なら、同じ結果になる。
/// </summary>
public sealed class UtilityDecider : IDecider
{
    /// <summary>行動の切実さの効き目（既定 3.0）。</summary>
    public const double Beta = 3.0;

    /// <summary>同じ行動が続くたびに掛ける重み。</summary>
    public const double RepeatFactor = 0.7;

    /// <summary>移動の手間の尺度（分）。exp(−移動時間 / 30)。</summary>
    public const double TravelScaleMinutes = 30.0;

    /// <summary>行動の既定の所要時間の刻み。</summary>
    public static readonly TimeSpan DurationStep = TimeSpan.FromMinutes(5);

    /// <summary>次の予定の場所へ向かう行動の ID。移動があるときは移動だけ（所要時間 0）、その場所にいるときは次の予定まで待つ。</summary>
    public const string GoToNext = "core.go_to_next";

    /// <summary>スマホを開く機会で何もしない行動の ID。所要時間 0。</summary>
    public const string Idle = "core.idle";

    /// <inheritdoc />
    public DeciderOutput Decide(DecisionContext context)
    {
        var defs = context.Rules.Defs;
        var state = context.State;
        var now = context.Now;
        var persona = state.Personas[context.Persona];
        if (persona.Location is not AtPlace at)
        {
            throw new InvalidOperationException("決定タイミングは、ペルソナが場所にいるときにしか起きません。");
        }

        var personaDef = defs.Personas[persona.DefId];
        var worldDefId = state.Worlds[persona.World].DefId;
        var worldDef = defs.Worlds[worldDefId];
        if (context.Reason == DecisionReason.Phone)
        {
            return DecidePhone(context, persona, personaDef, worldDef, at.Place);
        }

        var routes = context.Rules.Routes.For(worldDefId);
        var plan = persona.Plan;
        var next = plan?.NextFixedAfter(now);
        var deadline = next?.Start ?? plan?.HorizonEnd ?? now;
        var from = context.Random.Position;
        var random = context.Random;

        var options = Candidates(defs, persona, personaDef, worldDef, at.Place, now, routes, next, deadline);
        var drawn = options.Select(option => (Option: option, Duration: option.FixedDuration ?? DurationOf(option, random))).ToList();
        var feasible = drawn.Where(entry => IsFeasible(entry.Option, entry.Duration, now, deadline, next, routes)).ToList();
        var weights = WeightsOf(feasible, state, persona, personaDef, now);
        if (feasible.Where((entry, i) => !entry.Option.GoToNext && weights[i] > 0).Any())
        {
            feasible = feasible.Where(entry => !entry.Option.Waits).ToList();
            weights = WeightsOf(feasible, state, persona, personaDef, now);
        }

        var chosenIndex = weights.Sum() > 0 ? Pick(weights, random) : feasible.FindIndex(entry => entry.Option.GoToNext);
        var chosen = feasible[chosenIndex];
        var place = chosen.Option.Place;
        var route = chosen.Option.Minutes == 0
            ? ImmutableArray<RouteStep>.Empty
            : routes.Steps(at.Place, place) ?? ImmutableArray<RouteStep>.Empty;
        var draws = new RngDraws(persona.Id.Value, "decide", from, random.Position);
        var payload = new ActivityChosenPayload(chosen.Option.ActionId, place, route, chosen.Option.Minutes, Positive(chosen.Duration), false, draws);
        var guard = new Guard(
            [
                new ActivityVersionCheck(persona.ActivityVersion),
                new FeasibleCheck(chosen.Option.ActionId, place),
                new RngCursorCheck(persona.Id.Value, "decide", from),
            ],
            RejectAction.Redecide);
        return new DeciderOutput(payload, guard);
    }

    /// <summary>スマホを開く機会の決定。候補は、いまの場所での投稿（ブロックの残りで切る）と、何もしない（重み 1.0、所要時間 0）だけ。</summary>
    private static DeciderOutput DecidePhone(DecisionContext context, PersonaState persona, PersonaDef personaDef, WorldDef worldDef, PlaceId here)
    {
        var now = context.Now;
        var random = context.Random;
        var from = random.Position;
        var remaining = context.BlockId is { } blockId && persona.Plan?.CurrentBlock(blockId, now) is { } block
            ? block.End - now
            : TimeSpan.Zero;

        var entries = new List<(Option Option, TimeSpan Duration)>();
        foreach (var option in PhonePosts(context.Rules.Defs, persona, personaDef, worldDef, here, now, remaining))
        {
            var duration = DurationOf(option, random);
            entries.Add((option, duration > remaining ? remaining : duration));
        }

        var weights = WeightsOf(entries, context.State, persona, personaDef, now).Append(1.0).ToArray();
        entries.Add((new Option(Idle, here, 0, 1.0, null, TimeSpan.Zero), TimeSpan.Zero));
        var chosen = entries[Pick(weights, random)];
        var draws = new RngDraws(persona.Id.Value, "decide", from, random.Position);
        var payload = new ActivityChosenPayload(chosen.Option.ActionId, here, ImmutableArray<RouteStep>.Empty, 0, Positive(chosen.Duration), true, draws);
        var checks = new List<GuardCheck> { new ActivityVersionCheck(persona.ActivityVersion), new RngCursorCheck(persona.Id.Value, "decide", from) };
        if (chosen.Option.Action is not null)
        {
            checks.Insert(1, new FeasibleCheck(chosen.Option.ActionId, here));
        }

        return new DeciderOutput(payload, new Guard(checks.ToImmutableArray(), RejectAction.Redecide));
    }

    /// <summary>スマホを開く機会で候補になる、いまの場所での投稿の行動。ブロックの残りがなければ空。</summary>
    private static List<Option> PhonePosts(DefinitionPack defs, PersonaState persona, PersonaDef personaDef, WorldDef worldDef, PlaceId here, DateTimeOffset now, TimeSpan remaining)
    {
        var options = new List<Option>();
        if (remaining <= TimeSpan.Zero || worldDef.Places.FirstOrDefault(p => p.Id == here.Value) is not { } placeDef)
        {
            return options;
        }

        foreach (var action in defs.Actions.Values.OrderBy(a => a.Id, StringComparer.Ordinal))
        {
            if (action.Effects.Handler != SnsPostHandler.Name || action.ScheduleOnly || action.Duration is null || !ActionRules.IsUsable(personaDef, action.Id))
            {
                continue;
            }

            if (ActionRules.PreferenceFactor(personaDef, action) is not { } preference
                || !ActionRules.PlaceAllows(action, personaDef, placeDef)
                || !ActionRules.WithinLimits(action, persona.Usage.GetValueOrDefault(action.Id), now, worldDef.UtcOffset)
                || !ActionRules.WithinPostingLimits(action, persona.Posting, now))
            {
                continue;
            }

            options.Add(new Option(action.Id, here, 0, preference, action, null));
        }

        return options;
    }

    /// <summary>候補（行動と場所の組）。</summary>
    private sealed record Option(string ActionId, PlaceId Place, int Minutes, double Preference, ActionDef? Action, TimeSpan? FixedDuration)
    {
        /// <summary>次の予定の場所へ向かう行動か。</summary>
        public bool GoToNext => ActionId == UtilityDecider.GoToNext;

        /// <summary>移動のない次の予定の場所へ向かう行動か。次の予定まで、その場で待つ。</summary>
        public bool Waits => GoToNext && Minutes == 0;
    }

    /// <summary>候補を、行動 ID と場所 ID の順に並べて返す。</summary>
    private static List<Option> Candidates(DefinitionPack defs, PersonaState persona, PersonaDef personaDef, WorldDef worldDef, PlaceId here, DateTimeOffset now, RouteTable routes, PlanBlock? next, DateTimeOffset deadline)
    {
        var options = new List<Option>();
        var goPlace = next?.Place ?? here;
        var goMinutes = routes.Minutes(here, goPlace);
        if (goMinutes is null)
        {
            goPlace = here;
            goMinutes = 0;
        }

        var goAction = defs.Actions[GoToNext];
        var goPreference = ActionRules.PreferenceFactor(personaDef, goAction) ?? 1.0;
        var goDuration = goMinutes.Value == 0 ? deadline - now : TimeSpan.Zero;
        options.Add(new Option(GoToNext, goPlace, goMinutes.Value, goPreference, goAction, goDuration));
        foreach (var action in defs.Actions.Values.OrderBy(a => a.Id, StringComparer.Ordinal))
        {
            if (action.ScheduleOnly || action.Id == GoToNext || action.Duration is null || !ActionRules.IsUsable(personaDef, action.Id))
            {
                continue;
            }

            if (ActionRules.PreferenceFactor(personaDef, action) is not { } preference)
            {
                continue;
            }

            foreach (var place in worldDef.Places.OrderBy(p => p.Id, StringComparer.Ordinal))
            {
                var placeId = new PlaceId(place.Id);
                if (action.Requires.PlaceTags.IsEmpty && placeId != here)
                {
                    continue;
                }

                if (!ActionRules.PlaceAllows(action, personaDef, place) || routes.Minutes(here, placeId) is not { } minutes)
                {
                    continue;
                }

                if (!ActionRules.WithinLimits(action, persona.Usage.GetValueOrDefault(action.Id), now.AddMinutes(minutes), worldDef.UtcOffset)
                    || !ActionRules.WithinPostingLimits(action, persona.Posting, now.AddMinutes(minutes)))
                {
                    continue;
                }

                options.Add(new Option(action.Id, placeId, minutes, preference, action, null));
            }
        }

        return options.OrderBy(option => option.ActionId, StringComparer.Ordinal)
            .ThenBy(option => option.Place.Value, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>所要時間を決める。範囲なら5分刻みで一様に引き、固定なら引かない。</summary>
    private static TimeSpan DurationOf(Option option, IRandom random)
    {
        var duration = option.Action!.Duration!;
        if (!duration.IsRange)
        {
            return duration.Min;
        }

        var steps = (int)((duration.Max - duration.Min).TotalMinutes / DurationStep.TotalMinutes);
        return duration.Min + DurationStep * random.NextInt(0, steps + 1);
    }

    /// <summary>行けて、終えて、次の予定の場所へ戻れる候補か。次の予定の場所へ向かう候補は、いつも残す。</summary>
    private static bool IsFeasible(Option option, TimeSpan duration, DateTimeOffset now, DateTimeOffset deadline, PlanBlock? next, RouteTable routes)
    {
        if (option.GoToNext)
        {
            return true;
        }

        var back = 0;
        if (next?.Place is { } nextPlace)
        {
            if (routes.Minutes(option.Place, nextPlace) is not { } minutes)
            {
                return false;
            }

            back = minutes;
        }

        return duration > TimeSpan.Zero
            && now.AddMinutes(option.Minutes) + duration + TimeSpan.FromMinutes(back) <= deadline;
    }

    /// <summary>候補ごとの重みを、候補の並びのまま返す。</summary>
    /// <param name="entries">重みを出す候補と所要時間。</param>
    /// <param name="state">全体の状態。</param>
    /// <param name="persona">ペルソナの状態。</param>
    /// <param name="personaDef">ペルソナの定義。</param>
    /// <param name="now">いまの時刻。</param>
    private static double[] WeightsOf(List<(Option Option, TimeSpan Duration)> entries, SimState state, PersonaState persona, PersonaDef personaDef, DateTimeOffset now) =>
        entries.Select(entry => Weight(
            entry.Option.Action?.Weight ?? 1.0,
            entry.Option.Preference,
            Need(state, persona, personaDef, now, entry.Option.Action, entry.Option.Place, entry.Option.Minutes, Positive(entry.Duration), ConsecutiveOf(persona, entry.Option.ActionId)),
            ConsecutiveOf(persona, entry.Option.ActionId),
            entry.Option.Minutes)).ToArray();

    /// <summary>重みに比例して1つを引き、その位置を返す。重みの合計は正であること。</summary>
    private static int Pick(double[] weights, IRandom random)
    {
        var draw = random.NextDouble() * weights.Sum();
        var cumulative = 0.0;
        for (var i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (draw < cumulative)
            {
                return i;
            }
        }

        return weights.Length - 1;
    }

    /// <summary>負の長さを 0 に切り上げる。</summary>
    private static TimeSpan Positive(TimeSpan duration) => duration < TimeSpan.Zero ? TimeSpan.Zero : duration;

    /// <summary>直前まで同じ行動を続けた回数を返す。</summary>
    private static int ConsecutiveOf(PersonaState persona, string actionId) =>
        persona.Usage.TryGetValue(actionId, out var usage) ? usage.Consecutive : 0;

    /// <summary>候補の重み。どれか1つの要素が 0 なら選ばれない。</summary>
    /// <param name="baseWeight">行動の基本の重み。</param>
    /// <param name="preference">好みの倍率。</param>
    /// <param name="need">欲求の倍率（<see cref="Need"/>）。</param>
    /// <param name="consecutive">直前から連続して同じ行動を続けた回数。</param>
    /// <param name="travelMinutes">移動時間（分）。</param>
    internal static double Weight(double baseWeight, double preference, double need, int consecutive, int travelMinutes)
    {
        var repeat = 1.0;
        for (var i = 0; i < consecutive; i++)
        {
            repeat *= RepeatFactor;
        }

        var travel = Math.Exp(-travelMinutes / TravelScaleMinutes);
        return baseWeight * preference * need * repeat * travel;
    }

    /// <summary>欲求の倍率 exp(β × Σ u × g)。u は切実さ、g は行動を終えたときに良くなる量。</summary>
    /// <param name="state">全体の状態。</param>
    /// <param name="persona">ペルソナの状態。</param>
    /// <param name="personaDef">ペルソナの定義。</param>
    /// <param name="now">いまの時刻。</param>
    /// <param name="action">行動の定義（次の予定の場所へ向かう行動は null）。</param>
    /// <param name="place">行動する場所。</param>
    /// <param name="travelMinutes">移動時間（分）。</param>
    /// <param name="duration">所要時間。</param>
    /// <param name="consecutive">行動を始める前に、同じ行動が連続した回数。</param>
    internal static double Need(SimState state, PersonaState persona, PersonaDef personaDef, DateTimeOffset now, ActionDef? action, PlaceId place, int travelMinutes, TimeSpan duration, int consecutive)
    {
        var together = state.AnyoneElseAt(place, persona.Id);
        var sum = 0.0;
        foreach (var vital in VitalRules.AllVitals)
        {
            var range = VitalRules.Range(vital, personaDef.Vitals);
            var (target, width) = vital is VitalName.Energy or VitalName.Health ? (100.0, 100.0) : (0.0, 100.0);
            var current = persona.Vitals.Get(vital).ValueAt(now, range);
            var defaultRate = vital switch
            {
                VitalName.Energy => VitalRules.AwakeEnergyRate,
                VitalName.Health => VitalRules.DefaultHealthRate,
                VitalName.Boredom => VitalRules.DefaultBoredomRate,
                _ => together ? personaDef.Vitals.SocialRateTogether : personaDef.Vitals.SocialRateAlone,
            };
            var middle = VitalTrack.Clamp(current + defaultRate * travelMinutes / 60.0, range);
            var rate = action is not null && action.Effects.Rates.TryGetValue(vital, out var actionRate) ? actionRate : defaultRate;
            var end = VitalTrack.Clamp(middle + rate * duration.TotalHours, range);
            if (action is not null && action.Effects.OnEnd.TryGetValue(vital, out var delta))
            {
                if (delta < 0 && vital == VitalName.Boredom && action.Effects.RepeatDecay is { } decay)
                {
                    delta *= ActionRules.RepeatDecayFactor(decay, consecutive);
                }

                end = VitalTrack.Clamp(end + delta, range);
            }

            if (action is not null && action.Effects.Restore.TryGetValue(vital, out var restored))
            {
                end = VitalTrack.Clamp(restored, range);
            }

            var urgency = Math.Pow((current - target) / width, 2);
            var gain = (Math.Abs(current - target) - Math.Abs(end - target)) / width;
            sum += urgency * gain;
        }

        return Math.Exp(Beta * sum);
    }
}
