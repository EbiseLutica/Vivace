using System.Collections.Immutable;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using static Vivace.Core.Rules.FollowUpEvents;

namespace Vivace.Core.Rules.EventRules;

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
                if (!IsInPlan(persona.Plan, block))
                {
                    followUps.Add(Next(block.Start, EventTypes.BlockStarted, persona, new BlockStartedPayload(block.BlockId, plan.Version)));
                }
            }
            else if (block.BlockId is null || block.BlockId != persona.LastBlockId)
            {
                followUps.Add(Next(ev.Time, EventTypes.BlockStarted, persona, new BlockStartedPayload(block.BlockId, plan.Version)));
            }
        }

        if (plan.NextRebuildAt > ev.Time)
        {
            followUps.Add(Next(plan.NextRebuildAt, EventTypes.PlanDue, persona, new PlanDuePayload(plan.Version)));
        }

        return ApplyResult.Applied(next, followUps.ToImmutableArray());
    }

    /// <summary>ブロックが、プランに同じ種類・ID・時刻で入っているか。</summary>
    private static bool IsInPlan(DayPlan? plan, PlanBlock block) =>
        plan?.Blocks.Any(old => old.Kind == block.Kind && old.BlockId == block.BlockId && old.Start == block.Start && old.End == block.End) ?? false;
}
