using Vivace.Core.Domain;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;

namespace Vivace.Core.EventRules;

/// <summary>移動中の状態にし、到着を積む。</summary>
internal sealed class TravelStartedRule : EventRule<TravelStartedPayload>
{
    /// <summary>扱うイベントタイプ。</summary>
    public override string EventType => EventTypes.TravelStarted;

    /// <summary>移動中の状態にし、到着を積む。</summary>
    protected override ApplyResult Apply(RuleContext ctx, SimState state, SimEvent ev, TravelStartedPayload payload)
    {
        if (state.AlivePersona(ev.PersonaId) is not { } persona)
        {
            return ApplyResult.Skipped(state, "ペルソナがない");
        }

        if (payload.ActivityVersion != persona.ActivityVersion || persona.Activity is not null)
        {
            return ApplyResult.Skipped(state, "古い決定");
        }

        if (persona.Location is not AtPlace at || at.Place != payload.From)
        {
            return ApplyResult.Skipped(state, "居場所が違う");
        }

        if (payload.ArrivesAt <= ev.Time)
        {
            return ApplyResult.Skipped(state, "到着が過去");
        }

        var moving = persona with { Location = new Traveling(payload.From, payload.To, payload.Route, ev.Time, payload.ArrivesAt, payload.Next) };
        var next = VitalRules.Rebalance(ctx, state.WithPersona(moving), ev.Time, moving.Id, payload.From);
        return ApplyResult.Applied(next, [EventDraft.Next(payload.ArrivesAt, EventTypes.TravelArrived, persona, new TravelArrivedPayload(payload.To, payload.ActivityVersion))]);
    }
}
