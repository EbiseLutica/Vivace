using System.Collections.Immutable;
using Vivace.Core.Definitions;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Time;

namespace Vivace.Core.Rules;

/// <summary>イベントを適用し、新しい状態と後続のイベントとシグナルを返す。同じ入力なら同じ結果になる。</summary>
public static class Rules
{
    /// <summary>1つのイベントを適用する。guard を先に評価し、通らなければ却下する。</summary>
    /// <param name="ctx">規則の文脈。</param>
    /// <param name="state">適用の前の状態。</param>
    /// <param name="ev">適用するイベント。</param>
    /// <exception cref="NotSupportedException">扱わないイベントタイプ。</exception>
    public static ApplyResult Apply(RuleContext ctx, SimState state, SimEvent ev)
    {
        if (ev.Guard is { } guard && CheckGuard(ctx, state, ev, guard) is { } failure)
        {
            return Rejected(state, ev, guard, failure);
        }

        return ev.EventType switch
        {
            EventTypes.WorldCreated => ApplyWorldCreated(ctx, state, ev),
            EventTypes.DayStarted => ApplyDayStarted(ctx, state, ev),
            EventTypes.PersonaCreated => ApplyPersonaCreated(ctx, state, ev),
            EventTypes.PlanCommitted => ApplyPlanCommitted(state, ev),
            EventTypes.PlanDue => ApplyPlanDue(ctx, state, ev),
            EventTypes.BlockStarted => ApplyBlockStarted(ctx, state, ev),
            EventTypes.DecisionRequested => ApplyDecisionRequested(state, ev),
            EventTypes.ActivityChosen => ApplyActivityChosen(ctx, state, ev),
            EventTypes.TravelStarted => ApplyTravelStarted(ctx, state, ev),
            EventTypes.TravelArrived => ApplyTravelArrived(ctx, state, ev),
            EventTypes.ActionStarted => ApplyActionStarted(ctx, state, ev),
            EventTypes.ActionEnded => ApplyActionEnded(ctx, state, ev),
            _ => throw new NotSupportedException($"段階2 では扱わないイベントタイプです: {ev.EventType}"),
        };
    }

    private static ApplyResult ApplyWorldCreated(RuleContext ctx, SimState state, SimEvent ev)
    {
        var payload = (WorldCreatedPayload)ev.Payload;
        if (!ctx.Defs.Worlds.TryGetValue(payload.DefId, out var def))
        {
            return ApplyResult.Skipped(state, "定義がない");
        }

        var id = new WorldId(payload.DefId);
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
        var date = LocalClock.ToLocal(ev.Time, def.UtcOffset).Date;
        return ApplyResult.Applied(next, [new NewEvent(ev.Time, EventTypes.DayStarted, id, null, new DayStartedPayload(date))]);
    }

    private static ApplyResult ApplyDayStarted(RuleContext ctx, SimState state, SimEvent ev)
    {
        var payload = (DayStartedPayload)ev.Payload;
        if (ev.WorldId is not { } world || !state.Worlds.TryGetValue(world, out var worldState) || worldState.Status != WorldStatus.Alive)
        {
            return ApplyResult.Skipped(state, "ワールドがない");
        }

        var next = payload.Date.AddDays(1);
        var time = LocalClock.ToUtc(next, TimeOnly.MinValue, ctx.Defs.Worlds[worldState.DefId].UtcOffset);
        return ApplyResult.Applied(state, [new NewEvent(time, EventTypes.DayStarted, world, null, new DayStartedPayload(next))]);
    }

    private static ApplyResult ApplyPersonaCreated(RuleContext ctx, SimState state, SimEvent ev)
    {
        var payload = (PersonaCreatedPayload)ev.Payload;
        if (ev.WorldId is not { } world || !state.Worlds.TryGetValue(world, out var worldState) || worldState.Status != WorldStatus.Alive)
        {
            return ApplyResult.Skipped(state, "ワールドがない");
        }

        if (!ctx.Defs.Personas.TryGetValue(payload.DefId, out var personaDef) || personaDef.World != worldState.DefId)
        {
            return ApplyResult.Skipped(state, "定義がない");
        }

        var worldDef = ctx.Defs.Worlds[worldState.DefId];
        if (!worldDef.Places.Any(place => place.Id == payload.Home.Value))
        {
            return ApplyResult.Skipped(state, "家の場所がない");
        }

        var id = new PersonaId($"{personaDef.Id}@{world.Value}");
        if (state.Personas.ContainsKey(id))
        {
            return ApplyResult.Skipped(state, "すでに生成済み");
        }

        var t = ev.Time;
        var socialRange = VitalRules.Range(VitalName.Social, personaDef.Vitals);
        var vitals = new Vitals(
            new VitalTrack(t, personaDef.Vitals.EnergyInitial, 0),
            new VitalTrack(t, 100, 0),
            new VitalTrack(t, 30, 0),
            new VitalTrack(t, VitalRules.Clamp(0, socialRange), 0),
            1);
        var persona = new PersonaState(
            id,
            personaDef.Id,
            world,
            PersonaStatus.Alive,
            new AtPlace(payload.Home),
            null,
            0,
            null,
            vitals,
            [],
            null,
            0,
            ImmutableDictionary<string, ActionUsage>.Empty,
            ImmutableDictionary<string, ulong>.Empty,
            new PostingState(null, null, null),
            ImmutableList<InboxItem>.Empty,
            ImmutableList<OutboundOp>.Empty,
            null,
            Deciding: false,
            LastBlockId: null,
            PlannedFor: PeriodKeyAt(t));
        var next = Rebalance(ctx, Put(state, persona), t, id, payload.Home);
        return ApplyResult.Applied(next, signals: [new StartPlanner(ev.Seq, id, t)]);
    }

    private static ApplyResult ApplyPlanCommitted(SimState state, SimEvent ev)
    {
        if (!TryPersona(state, ev, out var persona))
        {
            return ApplyResult.Skipped(state, "ペルソナがない");
        }

        var payload = (PlanCommittedPayload)ev.Payload;
        var plan = payload.Plan with { Version = persona.PlanVersion + 1 };
        var next = Put(state, persona with { Plan = plan, PlanVersion = plan.Version });
        var followUps = new List<NewEvent>();
        foreach (var block in plan.Blocks.Where(block => block.End > ev.Time))
        {
            if (block.Start >= ev.Time)
            {
                if (!IsInPlan(persona.Plan, block))
                {
                    followUps.Add(Next(block.Start, EventTypes.BlockStarted, persona, new BlockStartedPayload(block.BlockId, plan.Version)));
                }
            }
            else if (block.BlockId is null || block.BlockId != persona.LastBlockId)
            {
                followUps.Add(Next(ev.Time, EventTypes.BlockStarted, persona, new BlockStartedPayload(block.BlockId, plan.Version)));
            }
        }

        if (plan.NextRebuildAt > ev.Time)
        {
            followUps.Add(Next(plan.NextRebuildAt, EventTypes.PlanDue, persona, new PlanDuePayload(plan.Version)));
        }

        return ApplyResult.Applied(next, followUps.ToImmutableArray());
    }

    private static ApplyResult ApplyPlanDue(RuleContext ctx, SimState state, SimEvent ev)
    {
        if (!TryPersona(state, ev, out var persona))
        {
            return ApplyResult.Skipped(state, "ペルソナがない");
        }

        if (((PlanDuePayload)ev.Payload).PlanVersion != persona.PlanVersion)
        {
            return ApplyResult.Skipped(state, "古いプラン");
        }

        if (ConsolidatingBlockAt(ctx, persona.Plan, ev.Time) is not null)
        {
            return ApplyResult.Skipped(state, "同じ時刻に睡眠が始まり、その開始が Planner を起こす");
        }

        var key = PeriodKeyAt(ev.Time);
        if (persona.PlannedFor == key)
        {
            return ApplyResult.Skipped(state, "この期間の Planner は動いている");
        }

        return ApplyResult.Applied(Put(state, persona with { PlannedFor = key }), signals: [new StartPlanner(ev.Seq, persona.Id, ev.Time)]);
    }

    /// <summary>期間の鍵（時刻）。記憶の整理を起こすブロックのない期間で使う。</summary>
    private static string PeriodKeyAt(DateTimeOffset time) => "time:" + time.ToUniversalTime().ToString("o", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>時刻 t に、記憶の整理を起こす行動（睡眠など）のブロックが始まるなら、それを返す。</summary>
    private static PlanBlock? ConsolidatingBlockAt(RuleContext ctx, DayPlan? plan, DateTimeOffset t) =>
        plan?.Blocks.FirstOrDefault(block =>
            block.Kind != BlockKind.Free
            && block.Start == t
            && block.ActionId is { } action
            && ctx.Defs.Actions.TryGetValue(action, out var def)
            && def.Effects.Sleep is { Consolidate: true });

    /// <summary>ブロックが、プランに同じ種類・ID・時刻で入っているか。</summary>
    private static bool IsInPlan(DayPlan? plan, PlanBlock block) =>
        plan?.Blocks.Any(old => old.Kind == block.Kind && old.BlockId == block.BlockId && old.Start == block.Start && old.End == block.End) ?? false;

    private static ApplyResult ApplyBlockStarted(RuleContext ctx, SimState state, SimEvent ev)
    {
        if (!TryPersona(state, ev, out var persona))
        {
            return ApplyResult.Skipped(state, "ペルソナがない");
        }

        var payload = (BlockStartedPayload)ev.Payload;
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

        var next = Rebalance(ctx, Put(state, current with { LastBlockId = blockId, PlannedFor = planning ? blockId : current.PlannedFor }), now, current.Id, from.Place);
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
        return ApplyResult.Applied(Put(state, updated), signals: signals);
    }

    private static ApplyResult ApplyDecisionRequested(SimState state, SimEvent ev)
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

        var payload = (DecisionRequestedPayload)ev.Payload;
        return ApplyResult.Applied(
            Put(state, persona with { Deciding = true }),
            signals: [new StartDecider(ev.Seq, persona.Id, ev.Time, payload.Reason, payload.BlockId)]);
    }

    private static ApplyResult ApplyActivityChosen(RuleContext ctx, SimState state, SimEvent ev)
    {
        if (!TryPersona(state, ev, out var persona))
        {
            return ApplyResult.Skipped(state, "ペルソナがない");
        }

        if (!persona.Deciding)
        {
            return ApplyResult.Skipped(state, "決めどきでない");
        }

        var payload = (ActivityChosenPayload)ev.Payload;
        var from = ((AtPlace)persona.Location).Place;
        var rng = payload.Draws is { } draws ? persona.Rng.SetItem(draws.Purpose, draws.To) : persona.Rng;
        var updated = persona with { Deciding = false, ActivityVersion = persona.ActivityVersion + 1, Rng = rng };
        var next = Put(state, updated);
        if (payload.Duration <= TimeSpan.Zero)
        {
            NewEvent? moveOnly = payload.Place == from
                ? null
                : TravelStarted(ev.Time, updated, from, payload.Place, payload.Route, ev.Time.AddMinutes(payload.TravelMinutes), null);
            return moveOnly is null
                ? ApplyResult.Applied(next)
                : ApplyResult.Applied(next, [moveOnly]);
        }

        var followUp = payload.Place == from
            ? ActionStarted(ev.Time, updated, payload.ActionId, payload.Place, payload.Duration, ActivityReason.Chosen, null)
            : TravelStarted(ev.Time, updated, from, payload.Place, payload.Route, ev.Time.AddMinutes(payload.TravelMinutes),
                new PendingAction(payload.ActionId, payload.Place, payload.Duration, ActivityReason.Chosen, null));
        return ApplyResult.Applied(next, [followUp]);
    }

    private static ApplyResult ApplyTravelStarted(RuleContext ctx, SimState state, SimEvent ev)
    {
        if (!TryPersona(state, ev, out var persona))
        {
            return ApplyResult.Skipped(state, "ペルソナがない");
        }

        var payload = (TravelStartedPayload)ev.Payload;
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
        var next = Rebalance(ctx, Put(state, moving), ev.Time, moving.Id, payload.From);
        return ApplyResult.Applied(next, [Next(payload.ArrivesAt, EventTypes.TravelArrived, persona, new TravelArrivedPayload(payload.To, payload.ActivityVersion))]);
    }

    private static ApplyResult ApplyTravelArrived(RuleContext ctx, SimState state, SimEvent ev)
    {
        if (!TryPersona(state, ev, out var persona))
        {
            return ApplyResult.Skipped(state, "ペルソナがない");
        }

        var payload = (TravelArrivedPayload)ev.Payload;
        if (payload.ActivityVersion != persona.ActivityVersion)
        {
            return ApplyResult.Skipped(state, "古い決定");
        }

        if (persona.Location is not Traveling travel || travel.To != payload.Place)
        {
            return ApplyResult.Skipped(state, "移動中でない");
        }

        var arrived = persona with { Location = new AtPlace(payload.Place) };
        var next = Rebalance(ctx, Put(state, arrived), ev.Time, arrived.Id, payload.Place);
        var followUps = ImmutableArray<NewEvent>.Empty;
        if (travel.Next is { } pending)
        {
            followUps = ImmutableArray.Create(ActionStarted(ev.Time, arrived, pending.ActionId, pending.Place, pending.Duration, pending.Reason, pending.BlockId));
        }
        else if (CoveringFixedBlock(arrived, ev.Time) is { } block)
        {
            followUps = ImmutableArray.Create(Next(ev.Time, EventTypes.BlockStarted, arrived, new BlockStartedPayload(block.BlockId, arrived.PlanVersion)));
        }
        else if (PlanRules.FreeAt(arrived.Plan, ev.Time) is not null)
        {
            followUps = ImmutableArray.Create(DecisionRequested(ev.Time, arrived, DecisionReason.Ended, null));
        }

        return ApplyResult.Applied(next, followUps);
    }

    private static ApplyResult ApplyActionStarted(RuleContext ctx, SimState state, SimEvent ev)
    {
        if (!TryPersona(state, ev, out var persona))
        {
            return ApplyResult.Skipped(state, "ペルソナがない");
        }

        var payload = (ActionStartedPayload)ev.Payload;
        if (payload.ActivityVersion != persona.ActivityVersion || persona.Activity is not null)
        {
            return ApplyResult.Skipped(state, "古い決定か、行動中");
        }

        if (persona.Location is not AtPlace at || at.Place != payload.Place)
        {
            return ApplyResult.Skipped(state, "居場所が違う");
        }

        if (payload.Duration <= TimeSpan.Zero || !ctx.Defs.Actions.TryGetValue(payload.ActionId, out var action))
        {
            return ApplyResult.Skipped(state, "所要時間か行動がおかしい");
        }

        var now = ev.Time;
        var localDate = LocalClock.ToLocal(now, ctx.Defs.Worlds[persona.World.Value].UtcOffset).Date;
        var started = persona with
        {
            Activity = new Activity(payload.ActionId, payload.Place, now, now + payload.Duration, payload.Reason, payload.BlockId),
            Usage = ActionRules.RecordStart(persona.Usage, payload.ActionId, now, localDate),
        };
        foreach (var (vital, delta) in action.Effects.OnStart)
        {
            started = VitalRules.Change(started, vital, now, ctx.Defs, delta, null);
        }

        var signals = ImmutableArray<EngineSignal>.Empty;
        var periodKey = payload.BlockId ?? PeriodKeyAt(now);
        if (action.Effects.Sleep is { Consolidate: true } && started.PlannedFor != periodKey)
        {
            started = started with { PlannedFor = periodKey };
            signals = ImmutableArray.Create<EngineSignal>(new StartPlanner(ev.Seq, started.Id, now));
        }

        var next = Rebalance(ctx, Put(state, started), now, started.Id);
        var followUps = ImmutableArray.Create(Next(now + payload.Duration, EventTypes.ActionEnded, started, new ActionEndedPayload(payload.ActionId, payload.ActivityVersion)));
        return ApplyResult.Applied(next, followUps, signals);
    }

    private static ApplyResult ApplyActionEnded(RuleContext ctx, SimState state, SimEvent ev)
    {
        if (!TryPersona(state, ev, out var persona))
        {
            return ApplyResult.Skipped(state, "ペルソナがない");
        }

        var payload = (ActionEndedPayload)ev.Payload;
        if (persona.Activity is not { } activity || activity.ActionId != payload.ActionId || payload.ActivityVersion != persona.ActivityVersion)
        {
            return ApplyResult.Skipped(state, "古い行動の終わり");
        }

        var now = ev.Time;
        var action = ctx.Defs.Actions[payload.ActionId];
        var ended = persona with { Activity = null };
        var priorRuns = persona.Usage.TryGetValue(payload.ActionId, out var usage) ? usage.Consecutive - 1 : 0;
        foreach (var (vital, delta) in action.Effects.OnEnd)
        {
            var adjusted = delta < 0 && vital == VitalName.Boredom && action.Effects.RepeatDecay is { } decay
                ? delta * ActionRules.RepeatDecayFactor(decay, priorRuns)
                : delta;
            ended = VitalRules.Change(ended, vital, now, ctx.Defs, adjusted, null);
        }

        foreach (var (vital, value) in action.Effects.Restore)
        {
            ended = VitalRules.Change(ended, vital, now, ctx.Defs, null, value);
        }

        if (action.Effects.Sleep is { Debt: true })
        {
            var lacking = ctx.Defs.Personas[persona.DefId].Vitals.SleepTarget - (now - activity.StartedAt);
            var delta = lacking > TimeSpan.Zero ? -Math.Min(4 * lacking.TotalHours, 20.0) : 5.0;
            ended = VitalRules.Change(ended, VitalName.Health, now, ctx.Defs, delta, null);
        }

        if (action.Effects.Sleep is not null)
        {
            ended = ended with { LastWake = now };
        }

        var next = Rebalance(ctx, Put(state, ended), now, ended.Id);
        var followUps = ImmutableArray<NewEvent>.Empty;
        if (!ended.Deciding && !PlanRules.BlockStartsAt(ended.Plan, now))
        {
            if (PlanRules.FreeAt(ended.Plan, now) is not null)
            {
                followUps = [DecisionRequested(now, ended, DecisionReason.Ended, null)];
            }
            else if (PlanRules.BlockCoveringAt(ended.Plan, now) is { } covering)
            {
                followUps = [DecisionRequested(now, ended, DecisionReason.Disruption, covering.BlockId)];
            }
        }

        return ApplyResult.Applied(next, followUps);
    }

    private static bool PlaceAllowsBlock(RuleContext ctx, PersonaState persona, ActionDef action, PlaceId place)
    {
        var worldDef = ctx.Defs.Worlds[persona.World.Value];
        var personaDef = ctx.Defs.Personas[persona.DefId];
        return worldDef.Places.FirstOrDefault(p => p.Id == place.Value) is { } placeDef
            && ActionRules.PlaceAllows(action, personaDef, placeDef);
    }

    /// <summary>guard を順に評価する。通らなければ理由を返す。</summary>
    private static string? CheckGuard(RuleContext ctx, SimState state, SimEvent ev, Guard guard)
    {
        var persona = ev.PersonaId is { } id && state.Personas.TryGetValue(id, out var found) ? found : null;
        foreach (var check in guard.Checks)
        {
            switch (check)
            {
                case ActivityVersionCheck version:
                    if (persona is null || persona.ActivityVersion != version.Expected)
                    {
                        return "行動の版が違う";
                    }

                    break;
                case FeasibleCheck feasible:
                    if (persona is null || !ActionRules.CanChoose(ctx, state, persona, feasible.ActionId, feasible.Place, ev.Time))
                    {
                        return "行動が実行できない";
                    }

                    break;
                case RngCursorCheck cursor:
                    if (persona is null || persona.Rng.GetValueOrDefault(cursor.Purpose) != cursor.Position)
                    {
                        return "乱数の位置が違う";
                    }

                    break;
                case PlanVersionCheck plan:
                    if (persona is null || persona.PlanVersion != plan.Expected)
                    {
                        return "プランの版が違う";
                    }

                    break;
                default:
                    return "段階2 では扱わない検査";
            }
        }

        return null;
    }

    private static ApplyResult Rejected(SimState state, SimEvent ev, Guard guard, string reason)
    {
        var followUps = ImmutableArray<NewEvent>.Empty;
        if (guard.OnReject == RejectAction.Redecide
            && ev.PersonaId is { } id
            && state.Personas.TryGetValue(id, out var persona)
            && persona.Status == PersonaStatus.Alive)
        {
            state = Put(state, persona with { Deciding = false });
            followUps = [DecisionRequested(ev.Time, persona, DecisionReason.Retry, null)];
        }

        return new ApplyResult(state, EventOutcome.Rejected, reason, followUps, []);
    }

    /// <summary>ペルソナが生きていれば取り出す。</summary>
    private static bool TryPersona(SimState state, SimEvent ev, out PersonaState persona)
    {
        if (ev.PersonaId is { } id && state.Personas.TryGetValue(id, out var found) && found.Status == PersonaStatus.Alive)
        {
            persona = found;
            return true;
        }

        persona = null!;
        return false;
    }

    private static SimState Put(SimState state, PersonaState persona) =>
        state with { Personas = state.Personas.SetItem(persona.Id, persona) };

    /// <summary>
    /// 状態変数の率を計算し直す。mover と、places にいる人が対象（居合わせが変わったので、社交欲求の率が変わりうる）。
    /// </summary>
    private static SimState Rebalance(RuleContext ctx, SimState state, DateTimeOffset now, PersonaId mover, params PlaceId[] places)
    {
        foreach (var id in state.Personas.Keys.OrderBy(key => key.Value, StringComparer.Ordinal).ToList())
        {
            var persona = state.Personas[id];
            if (persona.Status != PersonaStatus.Alive)
            {
                continue;
            }

            var affected = id == mover || (persona.Location is AtPlace at && places.Contains(at.Place));
            if (!affected)
            {
                continue;
            }

            var refreshed = VitalRules.Refresh(state, persona, now, ctx.Defs);
            if (!ReferenceEquals(refreshed, persona))
            {
                state = Put(state, refreshed);
            }
        }

        return state;
    }

    private static NewEvent Next(DateTimeOffset time, string eventType, PersonaState persona, EventPayload payload) =>
        new(time, eventType, persona.World, persona.Id, payload);

    private static NewEvent DecisionRequested(DateTimeOffset now, PersonaState persona, DecisionReason reason, string? blockId) =>
        Next(now, EventTypes.DecisionRequested, persona, new DecisionRequestedPayload(reason, blockId));

    private static NewEvent ActionStarted(DateTimeOffset now, PersonaState persona, string actionId, PlaceId place, TimeSpan duration, ActivityReason reason, string? blockId) =>
        Next(now, EventTypes.ActionStarted, persona, new ActionStartedPayload(actionId, place, duration, reason, persona.ActivityVersion, blockId));

    /// <summary>到着の時刻に、まだ始めていない固定・計画のブロックがあれば、それを返す（移動中に始まったブロックの取りこぼしを防ぐ）。</summary>
    private static PlanBlock? CoveringFixedBlock(PersonaState persona, DateTimeOffset now) =>
        persona.Plan?.Blocks.FirstOrDefault(block =>
            block.Kind != BlockKind.Free
            && block.BlockId is not null
            && block.BlockId != persona.LastBlockId
            && block.Start <= now
            && now < block.End);

    private static NewEvent TravelStarted(DateTimeOffset now, PersonaState persona, PlaceId from, PlaceId to, ImmutableArray<RouteStep> route, DateTimeOffset arrival, PendingAction? next) =>
        Next(now, EventTypes.TravelStarted, persona, new TravelStartedPayload(from, to, route, arrival, persona.ActivityVersion, next));
}
