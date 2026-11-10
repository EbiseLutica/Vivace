using System.Collections.Immutable;
using Vivace.Core.Definitions;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Routes;
using static Vivace.Core.Rules.FollowUpEvents;

namespace Vivace.Core.Rules.EventRules;

/// <summary>ブロックを始める。自由時間なら決めどきを積む。</summary>
internal sealed class BlockStartedRule : EventRule<BlockStartedPayload>
{
    /// <summary>扱うイベントタイプ。</summary>
    public override string EventType => EventTypes.BlockStarted;

    /// <summary>ブロックを始める。自由時間なら決めどきを積む。</summary>
    protected override ApplyResult Apply(RuleContext ctx, SimState state, SimEvent ev, BlockStartedPayload payload)
    {
        if (state.AlivePersona(ev.PersonaId) is not { } persona)
        {
            return ApplyResult.Skipped(state, "ペルソナがない");
        }

        if (payload.BlockId is null)
        {
            if (persona.Activity is not null || persona.Location is not AtPlace || persona.Deciding)
            {
                return ApplyResult.Skipped(state, "行動中か、決め中");
            }

            return PlanRules.FreeAt(persona.Plan, ev.Time) is null
                ? ApplyResult.Skipped(state, "自由時間でない")
                : ApplyResult.Applied(state, [DecisionRequested(ev.Time, persona, DecisionReason.Free, null)]);
        }

        return StartFixedBlock(ctx, state, persona, payload.BlockId, ev.Time, ev.Seq);
    }

    /// <summary>固定・計画のブロックを始める。いまの行動を打ち切り、行動の場所まで移動してから、行動を始める。満たせなければ、予定の乱れの決めどきを積む。</summary>
    private static ApplyResult StartFixedBlock(RuleContext ctx, SimState state, PersonaState persona, string blockId, DateTimeOffset now, long seq)
    {
        var block = PlanRules.CurrentBlock(persona.Plan, blockId, now);
        if (block is null || block.ActionId is null)
        {
            return ApplyResult.Skipped(state, "ブロックがない");
        }

        if (persona.LastBlockId == blockId)
        {
            return ApplyResult.Skipped(state, "すでに始めた");
        }

        if (persona.Location is not AtPlace from)
        {
            return IsConsolidating(ctx, block)
                ? PlannerFor(state, persona, block, seq, now)
                : ApplyResult.Skipped(state, "移動中");
        }

        var cut = persona.Activity is not null || persona.Deciding;
        var current = cut
            ? persona with { Activity = null, Deciding = false, ActivityVersion = persona.ActivityVersion + 1 }
            : persona;
        var signals = cut ? ImmutableArray.Create<EngineSignal>(new CancelHandler(persona.Id)) : ImmutableArray<EngineSignal>.Empty;
        var place = block.Place ?? from.Place;
        var routes = ctx.Routes.For(persona.World);
        var minutes = routes.Minutes(from.Place, place);
        var feasible = minutes is not null
            && ctx.Defs.Actions.TryGetValue(block.ActionId, out var action)
            && PlaceAllowsBlock(ctx, persona, action, place)
            && now.AddMinutes(minutes.Value) < block.End;

        var followUps = ImmutableArray<NewEvent>.Empty;
        if (!feasible)
        {
            followUps = [DecisionRequested(now, current, DecisionReason.Disruption, blockId)];
        }
        else if (minutes == 0)
        {
            followUps = [ActionStarted(now, current, block.ActionId, place, block.End - now, ActivityReason.Block, blockId)];
        }
        else
        {
            var arrival = now.AddMinutes(minutes ?? 0);
            var pending = new PendingAction(block.ActionId, place, block.End - arrival, ActivityReason.Block, blockId);
            followUps = [TravelStarted(now, current, from.Place, place, routes.Steps(from.Place, place) ?? ImmutableArray<RouteStep>.Empty, arrival, pending)];
        }

        var planning = !feasible && IsConsolidating(ctx, block) && current.PlannedFor != blockId;
        if (planning)
        {
            signals = signals.Add(new StartPlanner(seq, persona.Id, now));
        }

        var next = VitalRules.Rebalance(ctx, state.WithPersona(current with { LastBlockId = blockId, PlannedFor = planning ? blockId : current.PlannedFor }), now, current.Id, from.Place);
        return ApplyResult.Applied(next, followUps, signals);
    }

    /// <summary>記憶の整理を起こす行動（睡眠など）のブロックか。</summary>
    private static bool IsConsolidating(RuleContext ctx, PlanBlock block) =>
        block.ActionId is { } action && ctx.Defs.Actions.TryGetValue(action, out var def) && def.Effects.Sleep is { Consolidate: true };

    /// <summary>睡眠のブロックが始まれないときに、Planner を起こす。同じ期間では一度だけ。</summary>
    private static ApplyResult PlannerFor(SimState state, PersonaState persona, PlanBlock block, long seq, DateTimeOffset now)
    {
        var blockId = block.BlockId!;
        var updated = persona with
        {
            PlannedFor = blockId,
        };
        var signals = persona.PlannedFor == blockId
            ? ImmutableArray<EngineSignal>.Empty
            : ImmutableArray.Create<EngineSignal>(new StartPlanner(seq, persona.Id, now));
        return ApplyResult.Applied(state.WithPersona(updated), signals: signals);
    }

    /// <summary>場所が行動の条件とつながりを満たすか。</summary>
    private static bool PlaceAllowsBlock(RuleContext ctx, PersonaState persona, ActionDef action, PlaceId place)
    {
        var worldDef = ctx.Defs.Worlds[persona.World.Value];
        var personaDef = ctx.Defs.Personas[persona.DefId];
        return worldDef.Places.FirstOrDefault(p => p.Id == place.Value) is { } placeDef
            && ActionRules.PlaceAllows(action, personaDef, placeDef);
    }
}
