using System.Collections.Immutable;
using Vivace.Core.Domain;
using Vivace.Core.Events;

namespace Vivace.Core.Rules;

/// <summary>イベントの guard の評価と、却下の結果。</summary>
internal static class Guards
{
    /// <summary>guard を順に評価する。通らなければ理由を返す。</summary>
    internal static string? CheckGuard(RuleContext ctx, SimState state, SimEvent ev, Guard guard)
    {
        var persona = ev.PersonaId is { } id && state.Personas.TryGetValue(id, out var found) ? found : null;
        foreach (var check in guard.Checks)
        {
            switch (check)
            {
                case ActivityVersionCheck version:
                    if (persona is null || persona.ActivityVersion != version.Expected)
                    {
                        return "行動の版が違う";
                    }

                    break;
                case FeasibleCheck feasible:
                    if (persona is null || !ActionRules.CanChoose(ctx, state, persona, feasible.ActionId, feasible.Place, ev.Time))
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
                        return "プランの版が違う";
                    }

                    break;
                default:
                    return "段階2 では扱わない検査";
            }
        }

        return null;
    }

    /// <summary>却下の結果を作り、決め直しが要れば決めどきを積む。</summary>
    internal static ApplyResult Rejected(SimState state, SimEvent ev, Guard guard, string reason)
    {
        var followUps = ImmutableArray<NewEvent>.Empty;
        if (guard.OnReject == RejectAction.Redecide
            && ev.PersonaId is { } id
            && state.Personas.TryGetValue(id, out var persona)
            && persona.Status == PersonaStatus.Alive)
        {
            state = PersonaAccess.Put(state, persona with { Deciding = false });
            followUps = [FollowUpEvents.DecisionRequested(ev.Time, persona, DecisionReason.Retry, null)];
        }

        return new ApplyResult(state, EventOutcome.Rejected, reason, followUps, []);
    }
}
