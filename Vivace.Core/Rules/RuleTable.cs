using System.Collections.Immutable;
using Vivace.Core.Rules.EventRules;

namespace Vivace.Core.Rules;

/// <summary>イベントタイプから、そのルールを引く表。起動時に組み、以後は変えない。</summary>
internal static class RuleTable
{
    /// <summary>扱うイベントタイプとルールの表。キーは序数で比べる。</summary>
    internal static readonly ImmutableDictionary<string, IEventRule> Entries = Build();

    /// <summary>全ルールから表を組む。同じイベントタイプのルールがあれば例外になる。</summary>
    private static ImmutableDictionary<string, IEventRule> Build() =>
        new IEventRule[]
        {
            new WorldCreatedRule(),
            new DayStartedRule(),
            new PersonaCreatedRule(),
            new PlanCommittedRule(),
            new PlanDueRule(),
            new BlockStartedRule(),
            new DecisionRequestedRule(),
            new ActivityChosenRule(),
            new TravelStartedRule(),
            new TravelArrivedRule(),
            new ActionStartedRule(),
            new ActionEndedRule(),
        }.ToImmutableDictionary(rule => rule.EventType, rule => rule, StringComparer.Ordinal);
}
