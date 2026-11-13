using Vivace.Core.Domain.State;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;

namespace Vivace.Core.EventRules;

/// <summary>プランの作り直しの時刻が来たら、Planner を起こす。</summary>
internal sealed class PlanDueRule : EventRule<PlanDuePayload>
{
    /// <summary>扱うイベントタイプ。</summary>
    public override string EventType => EventTypes.PlanDue;

    /// <summary>プランの作り直しの時刻が来たら、Planner を起こす。</summary>
    protected override ApplyResult Apply(RuleContext ctx, SimState state, SimEvent ev, PlanDuePayload payload)
    {
        if (state.AlivePersona(ev.PersonaId) is not { } persona)
        {
            return ApplyResult.Skipped(state, "ペルソナがない");
        }

        if (payload.PlanVersion != persona.PlanVersion)
        {
            return ApplyResult.Skipped(state, "古いプラン");
        }

        if (ConsolidatingBlockAt(ctx, persona.Plan, ev.Time) is not null)
        {
            return ApplyResult.Skipped(state, "同じ時刻に睡眠が始まり、その開始が Planner を起こす");
        }

        if (PlannerHelper.Wake(persona, PlannerHelper.PeriodKeyAt(ev.Time), ev.Seq, ev.Time) is not { } wake)
        {
            return ApplyResult.Skipped(state, "この期間の Planner は動いている");
        }

        return ApplyResult.Applied(state.WithPersona(wake.Persona), signals: [wake.Signal]);
    }

    /// <summary>時刻 t に、記憶の整理を起こす行動（睡眠など）のブロックが始まるなら、それを返す。</summary>
    private static PlanBlock? ConsolidatingBlockAt(RuleContext ctx, DayPlan? plan, DateTimeOffset t) =>
        plan?.Blocks.FirstOrDefault(block =>
            block.Kind != BlockKind.Free
            && block.Start == t
            && block.ActionId is { } action
            && ctx.Defs.Actions.TryGetValue(action, out var def)
            && def.Effects.Sleep is { Consolidate: true });
}
