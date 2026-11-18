using Vivace.Core.Domain.State;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;
using Vivace.Core.Signals;

namespace Vivace.Core.EventRules;

/// <summary>決定中のフラグを立て、Decider の起動を依頼する。決定中なら捨てる。</summary>
internal sealed class DecisionRequestedRule : EventRule<DecisionRequestedPayload>
{
    /// <summary>扱うイベントタイプ。</summary>
    public override string EventType => EventTypes.DecisionRequested;

    /// <summary>決定中のフラグを立て、Decider の起動を依頼する。決定中なら捨てる。</summary>
    protected override ApplyResult Apply(RuleContext ctx, SimState state, SimEvent ev, DecisionRequestedPayload payload)
    {
        if (state.AlivePersona(ev.PersonaId) is not { } persona)
        {
            return ApplyResult.Skipped(state, "ペルソナがない");
        }

        if (persona.Deciding)
        {
            return ApplyResult.Skipped(state, "決定中");
        }

        if (payload.Reason == DecisionReason.Phone)
        {
            if (persona.Overlay is not null || persona.Activity is not { } activity || activity.BlockId != payload.BlockId)
            {
                return ApplyResult.Skipped(state, "ブロックの行動中でない、または副行動の途中");
            }
        }
        else if (persona.Activity is not null || persona.Location is not AtPlace)
        {
            return ApplyResult.Skipped(state, "行動中");
        }

        return ApplyResult.Applied(
            state.WithPersona(persona with { Deciding = true }),
            signals: [new StartDeciderSignal(ev.Seq, persona.Id, ev.Time, payload.Reason, payload.BlockId)]);
    }
}
