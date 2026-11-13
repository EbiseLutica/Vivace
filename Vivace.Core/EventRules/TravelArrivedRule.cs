using System.Collections.Immutable;
using Vivace.Core.Domain.State;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;

namespace Vivace.Core.EventRules;

/// <summary>場所に着いた状態にし、次の行動かブロックか決めどきを積む。</summary>
internal sealed class TravelArrivedRule : EventRule<TravelArrivedPayload>
{
    /// <summary>扱うイベントタイプ。</summary>
    public override string EventType => EventTypes.TravelArrived;

    /// <summary>場所に着いた状態にし、次の行動かブロックか決めどきを積む。</summary>
    protected override ApplyResult Apply(RuleContext ctx, SimState state, SimEvent ev, TravelArrivedPayload payload)
    {
        if (state.AlivePersona(ev.PersonaId) is not { } persona)
        {
            return ApplyResult.Skipped(state, "ペルソナがない");
        }

        if (payload.ActivityVersion != persona.ActivityVersion)
        {
            return ApplyResult.Skipped(state, "古い決定");
        }

        if (persona.Location is not Traveling travel || travel.To != payload.Place)
        {
            return ApplyResult.Skipped(state, "移動中でない");
        }

        var arrived = persona with { Location = new AtPlace(payload.Place) };
        var next = VitalRules.Rebalance(ctx, state.WithPersona(arrived), ev.Time, arrived.Id, payload.Place);
        var followUps = ImmutableArray<EventDraft>.Empty;
        if (travel.Next is { } pending)
        {
            followUps = ImmutableArray.Create(EventDraft.ActionStarted(ev.Time, arrived, pending.ActionId, pending.Place, pending.Duration, pending.Reason, pending.BlockId));
        }
        else if (CoveringFixedBlock(arrived, ev.Time) is { } block)
        {
            followUps = ImmutableArray.Create(EventDraft.Next(ev.Time, EventTypes.BlockStarted, arrived, new BlockStartedPayload(block.BlockId, arrived.PlanVersion)));
        }
        else if (arrived.Plan?.FreeAt(ev.Time) is not null)
        {
            followUps = ImmutableArray.Create(EventDraft.DecisionRequested(ev.Time, arrived, DecisionReason.Ended, null));
        }

        return ApplyResult.Applied(next, followUps);
    }

    /// <summary>到着の時刻に、まだ始めていない固定・計画のブロックがあれば、それを返す（移動中に始まったブロックの取りこぼしを防ぐ）。</summary>
    private static PlanBlock? CoveringFixedBlock(PersonaState persona, DateTimeOffset now) =>
        persona.Plan?.Blocks.FirstOrDefault(block =>
            block.Kind != BlockKind.Free
            && block.BlockId is not null
            && block.BlockId != persona.LastBlockId
            && block.Start <= now
            && now < block.End);
}
