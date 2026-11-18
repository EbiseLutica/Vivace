using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;

namespace Vivace.Core.EventRules;

/// <summary>
/// スマホを開く機会を、決定タイミングにする。長いブロックで間隔（phone_every）が決まっていれば、次の機会も積む。
/// </summary>
internal sealed class PhoneOpportunityRule : EventRule<PhoneOpportunityPayload>
{
    /// <summary>扱うイベントタイプ。</summary>
    public override string EventType => EventTypes.PhoneOpportunity;

    /// <summary>
    /// スマホを開く機会の決定タイミングを積む。ブロックがプランにあり、スマホを開けて、移動中でなければ。
    /// </summary>
    protected override ApplyResult Apply(RuleContext ctx, SimState state, SimEvent ev, PhoneOpportunityPayload payload)
    {
        if (state.AlivePersona(ev.PersonaId) is not { } persona)
        {
            return ApplyResult.Skipped(state, "ペルソナがない");
        }

        if (persona.Plan?.CurrentBlock(payload.BlockId, ev.Time) is not { } block || block.Phone != PhonePolicy.Open)
        {
            return ApplyResult.Skipped(state, "ブロックがない、またはスマホを開けない");
        }

        if (persona.Location is not AtPlace)
        {
            return ApplyResult.Skipped(state, "移動中");
        }

        var request = EventDraft.DecisionRequested(ev.Time, persona, DecisionReason.Phone, payload.BlockId);
        if (block.PhoneEvery is { } every && ev.Time + every < block.End)
        {
            var later = EventDraft.Next(ev.Time + every, EventTypes.PhoneOpportunity, persona, new PhoneOpportunityPayload(payload.BlockId));
            return ApplyResult.Applied(state, [request, later]);
        }

        return ApplyResult.Applied(state, [request]);
    }
}
