using System.Collections.Immutable;
using Vivace.Core.Definitions;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Randomness;
using Vivace.Core.Routes;
using Vivace.Core.Rules;

namespace Vivace.Core.Decision;

/// <summary>効用で自由時間の行動を決める。同じ写しと同じ乱数の位置なら、同じ結果になる。</summary>
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

    /// <summary>次の予定の場所へ向かう行動の ID。所要時間を持たず、次の予定の開始まで待つ。</summary>
    public const string GoToNext = "core.go_to_next";

    /// <inheritdoc />
    public DeciderOutput Decide(DecisionContext context)
    {
        var defs = context.Rules.Defs;
        var state = context.State;
        var now = context.Now;
        var persona = state.Personas[context.Persona];
        if (persona.Location is not AtPlace at)
        {
            throw new InvalidOperationException("決めどきは、ペルソナが場所にいるときだけ起きる");
        }

        var personaDef = defs.Personas[persona.DefId];
        var worldDef = defs.Worlds[persona.World.Value];
        var routes = context.Rules.Routes.For(persona.World);
        var plan = persona.Plan;
        var next = PlanRules.NextFixedAfter(plan, now);
        var deadline = next?.Start ?? plan?.HorizonEnd ?? now;
        var from = context.Random.Position;
        var random = context.Random;

        var options = Candidates(defs, persona, personaDef, worldDef, at.Place, now, routes, next, deadline);
        var drawn = options.Select(option => (Option: option, Duration: option.FixedDuration ?? DurationOf(option, random))).ToList();
        var feasible = drawn.Where(entry => IsFeasible(entry.Option, entry.Duration, now, deadline, next, routes)).ToList();

        var weights = feasible.Select(entry => Weight(
            entry.Option.Action?.Weight ?? 1.0,
            entry.Option.Preference,
            Need(state, persona, personaDef, now, entry.Option.Action, entry.Option.Place, entry.Option.Minutes, Positive(entry.Duration), ConsecutiveOf(persona, entry.Option.ActionId)),
            ConsecutiveOf(persona, entry.Option.ActionId),
            entry.Option.Minutes)).ToArray();
        var total = weights.Sum();
        var chosenIndex = feasible.FindIndex(entry => entry.Option.GoToNext);
        if (total > 0)
        {
            var draw = random.NextDouble() * total;
            var cumulative = 0.0;
            chosenIndex = weights.Length - 1;
            for (var i = 0; i < weights.Length; i++)
            {
                cumulative += weights[i];
                if (draw < cumulative)
                {
                    chosenIndex = i;
                    break;
                }
            }
        }

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

    /// <summary>候補（行動と場所の組）。</summary>
    private sealed record Option(string ActionId, PlaceId Place, int Minutes, double Preference, ActionDef? Action, TimeSpan? FixedDuration)
    {
        /// <summary>次の予定の場所へ向かう行動か。</summary>
        public bool GoToNext => ActionId == UtilityDecider.GoToNext;
    }

    /// <summary>候補を、行動 ID と場所 ID の順に並べて返す。</summary>
    private static List<Option> Candidates(Definitions.Definitions defs, PersonaState persona, PersonaDef personaDef, WorldDef worldDef, PlaceId here, DateTimeOffset now, RouteTable routes, PlanBlock? next, DateTimeOffset deadline)
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
        options.Add(new Option(GoToNext, goPlace, goMinutes.Value, goPreference, goAction, deadline - now.AddMinutes(goMinutes.Value)));
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
                if (!ActionRules.PlaceAllows(action, personaDef, place) || routes.Minutes(here, placeId) is not { } minutes)
                {
                    continue;
                }

                if (!ActionRules.WithinLimits(action, persona.Usage.GetValueOrDefault(action.Id), now.AddMinutes(minutes), worldDef.UtcOffset))
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
        var together = state.Personas.Values.Any(other =>
            other.Id != persona.Id
            && other.Status == PersonaStatus.Alive
            && other.Location is AtPlace otherAt
            && otherAt.Place == place);
        var sum = 0.0;
        foreach (var vital in VitalRules.AllVitals)
        {
            var range = VitalRules.Range(vital, personaDef.Vitals);
            var (target, width) = vital is VitalName.Energy or VitalName.Health ? (100.0, 100.0) : (0.0, 100.0);
            var current = VitalRules.Read(VitalRules.Get(persona.Vitals, vital), now, range);
            var defaultRate = vital switch
            {
                VitalName.Energy => VitalRules.AwakeEnergyRate,
                VitalName.Health => VitalRules.DefaultHealthRate,
                VitalName.Boredom => VitalRules.DefaultBoredomRate,
                _ => together ? personaDef.Vitals.SocialRateTogether : personaDef.Vitals.SocialRateAlone,
            };
            var middle = VitalRules.Clamp(current + defaultRate * travelMinutes / 60.0, range);
            var rate = action is not null && action.Effects.Rates.TryGetValue(vital, out var actionRate) ? actionRate : defaultRate;
            var end = VitalRules.Clamp(middle + rate * duration.TotalHours, range);
            if (action is not null && action.Effects.OnEnd.TryGetValue(vital, out var delta))
            {
                if (delta < 0 && vital == VitalName.Boredom && action.Effects.RepeatDecay is { } decay)
                {
                    delta *= ActionRules.RepeatDecayFactor(decay, consecutive);
                }

                end = VitalRules.Clamp(end + delta, range);
            }

            if (action is not null && action.Effects.Restore.TryGetValue(vital, out var restored))
            {
                end = VitalRules.Clamp(restored, range);
            }

            var urgency = Math.Pow((current - target) / width, 2);
            var gain = (Math.Abs(current - target) - Math.Abs(end - target)) / width;
            sum += urgency * gain;
        }

        return Math.Exp(Beta * sum);
    }
}
