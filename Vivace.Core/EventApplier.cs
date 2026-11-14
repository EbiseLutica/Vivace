using System.Collections.Immutable;
using Vivace.Core.Domain.State;
using Vivace.Core.Events.Guards;
using Vivace.Core.Events;
using Vivace.Core.EventRules;

namespace Vivace.Core;

/// <summary>イベントの適用のエントリポイント。イベントを適用し、新しい状態と後続のイベントとシグナルを返す。同じ入力なら同じ結果になる。</summary>
public sealed class EventApplier
{
    /// <summary>扱うイベントタイプとルールのテーブル。起動時に組み、以後は変えない。キーは序数で比べる。</summary>
    internal static readonly ImmutableDictionary<string, IEventRule> Table = BuildTable();

    /// <summary>規則のコンテキスト。</summary>
    private readonly RuleContext _ctx;

    /// <summary>規則のコンテキストを受け取って作る。</summary>
    /// <param name="ctx">規則のコンテキスト。</param>
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
        if (ev.Guard is { } guard && CheckGuard(state, ev, guard) is { } failure)
        {
            return Rejected(state, ev, guard, failure);
        }

        if (!Table.TryGetValue(ev.EventType, out var rule))
        {
            throw new NotSupportedException($"非対応のイベントタイプです: {ev.EventType}");
        }

        return rule.Apply(_ctx, state, ev);
    }

    /// <summary>guard を順に評価する。通らなければ理由を返す。</summary>
    private string? CheckGuard(SimState state, SimEvent ev, Guard guard)
    {
        var persona = ev.PersonaId is { } id && state.Personas.TryGetValue(id, out var found) ? found : null;
        foreach (var check in guard.Checks)
        {
            switch (check)
            {
                case ActivityVersionCheck version:
                    if (persona is null || persona.ActivityVersion != version.Expected)
                    {
                        return "行動のバージョンが違う";
                    }

                    break;
                case FeasibleCheck feasible:
                    if (persona is null || !ActionRules.CanChoose(_ctx, state, persona, feasible.ActionId, feasible.Place, ev.Time))
                    {
                        return "行動が実行できない";
                    }

                    break;
                case RngCursorCheck cursor:
                    if (persona is null || persona.Rng.GetValueOrDefault(cursor.Purpose) != cursor.Position)
                    {
                        return "乱数の位置が違う";
                    }

                    break;
                case PlanVersionCheck plan:
                    if (persona is null || persona.PlanVersion != plan.Expected)
                    {
                        return "プランのバージョンが違う";
                    }

                    break;
                default:
                    return "段階2 では扱わない検査";
            }
        }

        return null;
    }

    /// <summary>却下の結果を作り、決め直しが要れば決めどきを積む。</summary>
    private static ApplyResult Rejected(SimState state, SimEvent ev, Guard guard, string reason)
    {
        var followUps = ImmutableArray<EventDraft>.Empty;
        if (guard.OnReject == RejectAction.Redecide && state.AlivePersona(ev.PersonaId) is { } persona)
        {
            state = state.WithPersona(persona with { Deciding = false });
            followUps = [EventDraft.DecisionRequested(ev.Time, persona, DecisionReason.Retry, null)];
        }

        return new ApplyResult(state, EventStatus.Rejected, reason, followUps, []);
    }

    /// <summary>全ルールからテーブルを組む。同じイベントタイプのルールがあれば例外になる。</summary>
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
