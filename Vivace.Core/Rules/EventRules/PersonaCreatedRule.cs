using System.Collections.Immutable;
using Vivace.Core.Domain;
using Vivace.Core.Events;

namespace Vivace.Core.Rules.EventRules;

/// <summary>ペルソナを家に生成し、定義の初期値で状態変数を始める。</summary>
internal sealed class PersonaCreatedRule : EventRule<PersonaCreatedPayload>
{
    /// <summary>扱うイベントタイプ。</summary>
    public override string EventType => EventTypes.PersonaCreated;

    /// <summary>ペルソナを家に生成し、定義の初期値で状態変数を始める。</summary>
    protected override ApplyResult Apply(RuleContext ctx, SimState state, SimEvent ev, PersonaCreatedPayload payload)
    {
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
            new VitalTrack(t, VitalTrack.Clamp(0, socialRange), 0),
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
            PlannedFor: PlannerHelper.PeriodKeyAt(t));
        var next = VitalRules.Rebalance(ctx, state.WithPersona(persona), t, id, payload.Home);
        return ApplyResult.Applied(next, signals: [new StartPlanner(ev.Seq, id, t)]);
    }
}
