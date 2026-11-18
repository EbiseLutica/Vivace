using System.Collections.Immutable;
using Vivace.Core.Definitions.Model;
using Vivace.Core.Domain.State;
using Vivace.Core.Handlers;
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

        signals = signals.AddRange(EffectSignals(ev, action, started, payload));
        var next = VitalRules.Rebalance(ctx, state.WithPersona(started), now, started.Id);
        var followUps = ImmutableArray.Create(EventDraft.Next(now + payload.Duration, EventTypes.ActionEnded, started, new ActionEndedPayload(payload.ActionId, payload.ActivityVersion)));
        return ApplyResult.Applied(next, followUps, signals);
    }

    /// <summary>副行動を Overlay に入れて終了を積む。主の行動、率、バージョン、利用の記録は変えない。</summary>
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

        if (payload.Duration <= TimeSpan.Zero || !ctx.Defs.Actions.TryGetValue(payload.ActionId, out var action))
        {
            return ApplyResult.Skipped(state, "所要時間か行動がおかしい");
        }

        var now = ev.Time;
        var overlay = new Activity(payload.ActionId, payload.Place, now, now + payload.Duration, payload.Reason, null);
        var followUp = EventDraft.Next(now + payload.Duration, EventTypes.ActionEnded, persona, new ActionEndedPayload(payload.ActionId, payload.ActivityVersion, true));
        return ApplyResult.Applied(state.WithPersona(persona with { Overlay = overlay }), [followUp], EffectSignals(ev, action, persona, payload));
    }

    /// <summary>行動の効果のハンドラ（effects.handler）を起動するシグナル。効果がなければ空。行動の終わりを出力の時刻にする。</summary>
    /// <param name="ev">行動の開始のイベント。</param>
    /// <param name="action">行動の定義。</param>
    /// <param name="persona">ペルソナの状態。</param>
    /// <param name="payload">行動の開始の中身。</param>
    private static ImmutableArray<EngineSignal> EffectSignals(SimEvent ev, ActionDef action, PersonaState persona, ActionStartedPayload payload)
    {
        if (action.Effects.Handler != SnsPostHandler.Name)
        {
            return ImmutableArray<EngineSignal>.Empty;
        }

        return [new StartEffectSignal(ev.Seq, persona.Id, ev.Time + payload.Duration, SnsPostHandler.Name, payload.ActionId, payload.Place, payload.ActivityVersion)];
    }
}
