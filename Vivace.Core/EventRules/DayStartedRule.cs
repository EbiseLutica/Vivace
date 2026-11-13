using Vivace.Core.Domain.State;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;
using Vivace.Core.Time;

namespace Vivace.Core.EventRules;

/// <summary>次の日の始まりを積む。</summary>
internal sealed class DayStartedRule : EventRule<DayStartedPayload>
{
    /// <summary>扱うイベントタイプ。</summary>
    public override string EventType => EventTypes.DayStarted;

    /// <summary>次の日の始まりを積む。</summary>
    protected override ApplyResult Apply(RuleContext ctx, SimState state, SimEvent ev, DayStartedPayload payload)
    {
        if (ev.WorldId is not { } world || !state.Worlds.TryGetValue(world, out var worldState) || worldState.Status != WorldStatus.Alive)
        {
            return ApplyResult.Skipped(state, "ワールドがない");
        }

        var next = payload.Date.AddDays(1);
        var time = LocalTimeHelper.ToUtc(next, TimeOnly.MinValue, ctx.Defs.Worlds[worldState.DefId].UtcOffset);
        return ApplyResult.Applied(state, [new EventDraft(time, EventTypes.DayStarted, world, null, new DayStartedPayload(next))]);
    }
}
