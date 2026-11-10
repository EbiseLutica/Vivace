using Vivace.Core.Domain;
using Vivace.Core.Events;

namespace Vivace.Core.Rules.EventRules;

/// <summary>行動の決定を当てる。場所が違えば移動を、同じなら行動の開始を積む。</summary>
internal sealed class ActivityChosenRule : EventRule<ActivityChosenPayload>
{
    /// <summary>扱うイベントタイプ。</summary>
    public override string EventType => EventTypes.ActivityChosen;

    /// <summary>行動の決定を当てる。場所が違えば移動を、同じなら行動の開始を積む。</summary>
    protected override ApplyResult Apply(RuleContext ctx, SimState state, SimEvent ev, ActivityChosenPayload payload)
    {
        if (state.AlivePersona(ev.PersonaId) is not { } persona)
        {
            return ApplyResult.Skipped(state, "ペルソナがない");
        }

        if (!persona.Deciding)
        {
            return ApplyResult.Skipped(state, "決めどきでない");
        }

        var from = ((AtPlace)persona.Location).Place;
        var rng = payload.Draws is { } draws ? persona.Rng.SetItem(draws.Purpose, draws.To) : persona.Rng;
        var updated = persona with { Deciding = false, ActivityVersion = persona.ActivityVersion + 1, Rng = rng };
        var next = state.WithPersona(updated);
        if (payload.Duration <= TimeSpan.Zero)
        {
            NewEvent? moveOnly = payload.Place == from
                ? null
                : NewEvent.TravelStarted(ev.Time, updated, from, payload.Place, payload.Route, ev.Time.AddMinutes(payload.TravelMinutes), null);
            return moveOnly is null
                ? ApplyResult.Applied(next)
                : ApplyResult.Applied(next, [moveOnly]);
        }

        var followUp = payload.Place == from
            ? NewEvent.ActionStarted(ev.Time, updated, payload.ActionId, payload.Place, payload.Duration, ActivityReason.Chosen, null)
            : NewEvent.TravelStarted(ev.Time, updated, from, payload.Place, payload.Route, ev.Time.AddMinutes(payload.TravelMinutes),
                new PendingAction(payload.ActionId, payload.Place, payload.Duration, ActivityReason.Chosen, null));
        return ApplyResult.Applied(next, [followUp]);
    }
}
