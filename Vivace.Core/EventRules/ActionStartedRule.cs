using System.Collections.Immutable;
using Vivace.Core.Domain.State;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Events;
using Vivace.Core.Signals;
using Vivace.Core.Time;

namespace Vivace.Core.EventRules;

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

        if (payload.IsSubAction)
        {
            return StartSub(ctx, state, ev, persona, payload);
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
        var localDate = LocalTimeHelper.ToLocal(now, ctx.Defs.Worlds[state.Worlds[persona.World].DefId].UtcOffset).Date;
        var started = persona.StartUsage(payload.ActionId, now, localDate) with
        {
            Activity = new Activity(payload.ActionId, payload.Place, now, now + payload.Duration, payload.Reason, payload.BlockId),
            Overlay = null,
        };
        foreach (var (vital, delta) in action.Effects.OnStart)
        {
            started = VitalRules.Change(started, vital, now, ctx.Defs, delta, null);
        }

        var signals = ImmutableArray<EngineSignal>.Empty;
        var periodKey = payload.BlockId ?? PlannerHelper.PeriodKeyAt(now);
        if (action.Effects.Sleep is { Consolidate: true } && PlannerHelper.Wake(started, periodKey, ev.Seq, now) is { } wake)
        {
            started = wake.Persona;
            signals = ImmutableArray.Create<EngineSignal>(wake.Signal);
        }

        var next = VitalRules.Rebalance(ctx, state.WithPersona(started), now, started.Id);
        var followUps = ImmutableArray.Create(EventDraft.Next(now + payload.Duration, EventTypes.ActionEnded, started, new ActionEndedPayload(payload.ActionId, payload.ActivityVersion)));
        return ApplyResult.Applied(next, followUps, signals);
    }

    /// <summary>
    /// 副行動を始める。主の行動、率、バージョン、利用の記録は変えず、Overlay に入れて、終了を積む。効果はまだ当てない。
    /// </summary>
    /// <param name="ctx">規則のコンテキスト。</param>
    /// <param name="state">適用の前の状態。</param>
    /// <param name="ev">適用するイベント。</param>
    /// <param name="persona">ペルソナ。</param>
    /// <param name="payload">副行動の開始。</param>
    private static ApplyResult StartSub(RuleContext ctx, SimState state, SimEvent ev, PersonaState persona, ActionStartedPayload payload)
    {
        if (payload.ActivityVersion != persona.ActivityVersion || persona.Activity is null || persona.Overlay is not null)
        {
            return ApplyResult.Skipped(state, "古い決定か、主の行動がないか、副行動の途中");
        }

        if (persona.Location is not AtPlace at || at.Place != payload.Place)
        {
            return ApplyResult.Skipped(state, "居場所が違う");
        }

        if (payload.Duration <= TimeSpan.Zero || !ctx.Defs.Actions.ContainsKey(payload.ActionId))
        {
            return ApplyResult.Skipped(state, "所要時間か行動がおかしい");
        }

        var now = ev.Time;
        var overlay = new Activity(payload.ActionId, payload.Place, now, now + payload.Duration, payload.Reason, null);
        var followUp = EventDraft.Next(now + payload.Duration, EventTypes.ActionEnded, persona, new ActionEndedPayload(payload.ActionId, payload.ActivityVersion, true));
        return ApplyResult.Applied(state.WithPersona(persona with { Overlay = overlay }), [followUp]);
    }
}
