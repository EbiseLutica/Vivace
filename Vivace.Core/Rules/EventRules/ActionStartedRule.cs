using System.Collections.Immutable;
using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Time;
using static Vivace.Core.Rules.FollowUpEvents;
using static Vivace.Core.Rules.PeriodKeys;

namespace Vivace.Core.Rules.EventRules;

/// <summary>行動を始め、率を切り替え、終了を積む。</summary>
internal sealed class ActionStartedRule : EventRule<ActionStartedPayload>
{
    /// <summary>扱うイベントタイプ。</summary>
    public override string EventType => EventTypes.ActionStarted;

    /// <summary>行動を始め、率を切り替え、終了を積む。</summary>
    protected override ApplyResult Apply(RuleContext ctx, SimState state, SimEvent ev, ActionStartedPayload payload)
    {
        if (state.AlivePersona(ev.PersonaId) is not { } persona)
        {
            return ApplyResult.Skipped(state, "ペルソナがない");
        }

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

        var next = VitalRules.Rebalance(ctx, state.WithPersona(started), now, started.Id);
        var followUps = ImmutableArray.Create(Next(now + payload.Duration, EventTypes.ActionEnded, started, new ActionEndedPayload(payload.ActionId, payload.ActivityVersion)));
        return ApplyResult.Applied(next, followUps, signals);
    }
}
