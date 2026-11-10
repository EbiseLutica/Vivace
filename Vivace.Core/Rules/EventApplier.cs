using System.Collections.Immutable;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Rules.EventRules;
using static Vivace.Core.Rules.Guards;

namespace Vivace.Core.Rules;

/// <summary>イベントの適用の入口。イベントを適用し、新しい状態と後続のイベントとシグナルを返す。同じ入力なら同じ結果になる。</summary>
public sealed class EventApplier
{
    /// <summary>扱うイベントタイプとルールの表。起動時に組み、以後は変えない。キーは序数で比べる。</summary>
    internal static readonly ImmutableDictionary<string, IEventRule> Table = BuildTable();

    /// <summary>規則の文脈。</summary>
    private readonly RuleContext _ctx;

    /// <summary>規則の文脈を受け取って作る。</summary>
    /// <param name="ctx">規則の文脈。</param>
    public EventApplier(RuleContext ctx)
    {
        _ctx = ctx;
    }

    /// <summary>1つのイベントを適用する。guard を先に評価し、通らなければ却下する。</summary>
    /// <param name="state">適用の前の状態。</param>
    /// <param name="ev">適用するイベント。</param>
    /// <exception cref="NotSupportedException">扱わないイベントタイプ。</exception>
    public ApplyResult Apply(SimState state, SimEvent ev)
    {
        if (ev.Guard is { } guard && CheckGuard(_ctx, state, ev, guard) is { } failure)
        {
            return Rejected(state, ev, guard, failure);
        }

        if (!Table.TryGetValue(ev.EventType, out var rule))
        {
            throw new NotSupportedException($"非対応のイベントタイプです: {ev.EventType}");
        }

        return rule.Apply(_ctx, state, ev);
    }

    /// <summary>全ルールから表を組む。同じイベントタイプのルールがあれば例外になる。</summary>
    private static ImmutableDictionary<string, IEventRule> BuildTable() =>
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
