using Vivace.Core.Domain;
using Vivace.Core.Events;
using static Vivace.Core.Rules.PersonaAccess;

namespace Vivace.Core.Rules.EventRules;

/// <summary>決めどきの印を立て、Decider の起動を依頼する。決め中なら捨てる。</summary>
internal sealed class DecisionRequestedRule : EventRule<DecisionRequestedPayload>
{
    /// <summary>扱うイベントタイプ。</summary>
    public override string EventType => EventTypes.DecisionRequested;

    /// <summary>決めどきの印を立て、Decider の起動を依頼する。決め中なら捨てる。</summary>
    protected override ApplyResult Apply(RuleContext ctx, SimState state, SimEvent ev, DecisionRequestedPayload payload)
    {
        if (!TryPersona(state, ev, out var persona))
        {
            return ApplyResult.Skipped(state, "ペルソナがない");
        }

        if (persona.Deciding)
        {
            return ApplyResult.Skipped(state, "決め中");
        }

        if (persona.Activity is not null || persona.Location is not AtPlace)
        {
            return ApplyResult.Skipped(state, "行動中");
        }

        return ApplyResult.Applied(
            Put(state, persona with { Deciding = true }),
            signals: [new StartDecider(ev.Seq, persona.Id, ev.Time, payload.Reason, payload.BlockId)]);
    }
}
