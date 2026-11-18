using Vivace.Core.Domain;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;

namespace Vivace.Core.EventRules;

/// <summary>スマホを開く機会を決定タイミングにし、phone_every があれば次の機会も積む。</summary>
internal sealed class PhoneOpportunityRule : EventRule<PhoneOpportunityPayload>
{
    /// <summary>扱うイベントタイプ。</summary>
    public override string EventType => EventTypes.PhoneOpportunity;

    /// <summary>ブロックがプランにあり、スマホを開けて、移動中でなければ、決定タイミングを積む。</summary>
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
        var inBlock = persona.Activity?.BlockId == payload.BlockId || persona.LastBlockId == payload.BlockId;
        if (inBlock && block.PhoneEvery is { } every && ev.Time + every < block.End)
        {
            var later = EventDraft.PhoneOpportunity(ev.Time + every, persona, payload.BlockId);
            return ApplyResult.Applied(state, [request, later]);
        }

        return ApplyResult.Applied(state, [request]);
    }
}
