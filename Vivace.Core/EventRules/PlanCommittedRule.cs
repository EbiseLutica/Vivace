using System.Collections.Immutable;
using Vivace.Core.Domain;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;

namespace Vivace.Core.EventRules;

/// <summary>プランを差し替え、新しく入ったブロックの開始と、作り直しの時刻を積む。</summary>
internal sealed class PlanCommittedRule : EventRule<PlanCommittedPayload>
{
    /// <summary>扱うイベントタイプ。</summary>
    public override string EventType => EventTypes.PlanCommitted;

    /// <summary>プランを差し替え、新しく入ったブロックの開始と、作り直しの時刻を積む。</summary>
    protected override ApplyResult Apply(RuleContext ctx, SimState state, SimEvent ev, PlanCommittedPayload payload)
    {
        if (state.AlivePersona(ev.PersonaId) is not { } persona)
        {
            return ApplyResult.Skipped(state, "ペルソナがない");
        }

        var plan = payload.Plan with { Version = persona.PlanVersion + 1 };
        var next = state.WithPersona(persona with { Plan = plan, PlanVersion = plan.Version });
        var followUps = new List<NewEvent>();
        foreach (var block in plan.Blocks.Where(block => block.End > ev.Time))
        {
            if (block.Start >= ev.Time)
            {
                if (persona.Plan?.Contains(block) != true)
                {
                    followUps.Add(NewEvent.Next(block.Start, EventTypes.BlockStarted, persona, new BlockStartedPayload(block.BlockId, plan.Version)));
                }
            }
            else if (block.BlockId is null || block.BlockId != persona.LastBlockId)
            {
                followUps.Add(NewEvent.Next(ev.Time, EventTypes.BlockStarted, persona, new BlockStartedPayload(block.BlockId, plan.Version)));
            }
        }

        if (plan.NextRebuildAt > ev.Time)
        {
            followUps.Add(NewEvent.Next(plan.NextRebuildAt, EventTypes.PlanDue, persona, new PlanDuePayload(plan.Version)));
        }

        return ApplyResult.Applied(next, followUps.ToImmutableArray());
    }
}
