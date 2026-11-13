using System.Collections.Immutable;
using Vivace.Core.Domain;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;
using Vivace.Core.Time;

namespace Vivace.Core.EventRules;

/// <summary>ワールドを生成し、最初の日の始まりを積む。</summary>
internal sealed class WorldCreatedRule : EventRule<WorldCreatedPayload>
{
    /// <summary>扱うイベントタイプ。</summary>
    public override string EventType => EventTypes.WorldCreated;

    /// <summary>ワールドを生成し、最初の日の始まりを積む。</summary>
    protected override ApplyResult Apply(RuleContext ctx, SimState state, SimEvent ev, WorldCreatedPayload payload)
    {
        if (!ctx.Defs.Worlds.TryGetValue(payload.DefId, out var def))
        {
            return ApplyResult.Skipped(state, "定義がない");
        }

        var id = new WorldId(payload.DefId.Value);
        if (state.Worlds.ContainsKey(id))
        {
            return ApplyResult.Skipped(state, "すでに生成済み");
        }

        var terms = def.Terms.ToImmutableDictionary(
            term => term.From,
            term => new TermEntry(term.From, term.Verdict, term.To, null, ev.Seq),
            StringComparer.Ordinal);
        var world = new WorldState(
            id,
            def.Id,
            payload.Seed,
            WorldStatus.Alive,
            ImmutableDictionary<PlaceId, RuntimePlace>.Empty,
            terms,
            ImmutableDictionary<long, ActiveIncident>.Empty,
            1);
        var next = state with { Worlds = state.Worlds.SetItem(id, world) };
        var date = LocalTimeHelper.ToLocal(ev.Time, def.UtcOffset).Date;
        return ApplyResult.Applied(next, [new EventDraft(ev.Time, EventTypes.DayStarted, id, null, new DayStartedPayload(date))]);
    }
}
