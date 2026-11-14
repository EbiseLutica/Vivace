using System.Collections.Immutable;
using Vivace.Core.Domain.State;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;

namespace Vivace.Core.EventRules;

/// <summary>行動を終え、終了時の効果を当て、次の決定タイミングを積む。</summary>
internal sealed class ActionEndedRule : EventRule<ActionEndedPayload>
{
    /// <summary>扱うイベントタイプ。</summary>
    public override string EventType => EventTypes.ActionEnded;

    /// <summary>行動を終え、終了時の効果を当て、次の決定タイミングを積む。</summary>
    protected override ApplyResult Apply(RuleContext ctx, SimState state, SimEvent ev, ActionEndedPayload payload)
    {
        if (state.AlivePersona(ev.PersonaId) is not { } persona)
        {
            return ApplyResult.Skipped(state, "ペルソナがない");
        }

        if (persona.Activity is not { } activity || activity.ActionId != payload.ActionId || payload.ActivityVersion != persona.ActivityVersion)
        {
            return ApplyResult.Skipped(state, "古い行動の終わり");
        }

        var now = ev.Time;
        var action = ctx.Defs.Actions[payload.ActionId];
        var ended = persona with { Activity = null };
        var priorRuns = persona.Usage.TryGetValue(payload.ActionId, out var usage) ? usage.Consecutive - 1 : 0;
        foreach (var (vital, delta) in action.Effects.OnEnd)
        {
            var adjusted = delta < 0 && vital == VitalName.Boredom && action.Effects.RepeatDecay is { } decay
                ? delta * ActionRules.RepeatDecayFactor(decay, priorRuns)
                : delta;
            ended = VitalRules.Change(ended, vital, now, ctx.Defs, adjusted, null);
        }

        foreach (var (vital, value) in action.Effects.Restore)
        {
            ended = VitalRules.Change(ended, vital, now, ctx.Defs, null, value);
        }

        if (action.Effects.Sleep is { Debt: true })
        {
            var lacking = ctx.Defs.Personas[persona.DefId].Vitals.SleepTarget - (now - activity.StartedAt);
            var delta = lacking > TimeSpan.Zero ? -Math.Min(4 * lacking.TotalHours, 20.0) : 5.0;
            ended = VitalRules.Change(ended, VitalName.Health, now, ctx.Defs, delta, null);
        }

        if (action.Effects.Sleep is not null)
        {
            ended = ended with { LastWake = now };
        }

        var next = VitalRules.Rebalance(ctx, state.WithPersona(ended), now, ended.Id);
        var followUps = ImmutableArray<EventDraft>.Empty;
        if (!ended.Deciding && ended.Plan?.BlockStartsAt(now) != true)
        {
            if (ended.Plan?.FreeAt(now) is not null)
            {
                followUps = [EventDraft.DecisionRequested(now, ended, DecisionReason.Ended, null)];
            }
            else if (ended.Plan?.BlockCoveringAt(now) is { } covering)
            {
                followUps = [EventDraft.DecisionRequested(now, ended, DecisionReason.Disruption, covering.BlockId)];
            }
        }

        return ApplyResult.Applied(next, followUps);
    }
}
