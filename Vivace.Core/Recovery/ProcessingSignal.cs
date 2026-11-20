using Vivace.Core.Domain;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Handlers;
using Vivace.Core.Signals;

namespace Vivace.Core.Recovery;

/// <summary>
/// 処理中のイベントから、起動し直すハンドラのシグナルを作る。処理中なら、そのイベントはシグナルを出していたので、
/// 種類は定義と中身から決まる（睡眠の記憶の整理と効果のハンドラは、定義の検証で同じ行動に持たせない）。
/// </summary>
internal static class ProcessingSignal
{
    /// <summary>処理中のイベントに対応するシグナルを返す。知らないイベントタイプなら例外。</summary>
    /// <param name="ctx">規則のコンテキスト（行動の定義を引く）。</param>
    /// <param name="ev">処理中のイベント。</param>
    /// <exception cref="InvalidOperationException">処理中にならないイベントタイプ、または行動の定義がない。</exception>
    public static EngineSignal For(RuleContext ctx, SimEvent ev) => ev.EventType switch
    {
        EventTypes.DecisionRequested => Decider(ev),
        EventTypes.ActionStarted => Action(ctx, ev),
        EventTypes.PersonaCreated or EventTypes.BlockStarted or EventTypes.PlanDue => Planner(ev),
        _ => throw new InvalidOperationException($"処理中になるはずのないイベントタイプです: {ev.EventType}（seq {ev.Seq}）"),
    };

    /// <summary>決定タイミングの Decider のシグナル。</summary>
    private static EngineSignal Decider(SimEvent ev)
    {
        var payload = (DecisionRequestedPayload)ev.Payload;
        return new StartDeciderSignal(ev.Seq, Persona(ev), ev.Time, payload.Reason, payload.BlockId);
    }

    /// <summary>行動の開始のシグナル。効果のハンドラがあれば効果、なければ記憶の整理を起こす Planner。</summary>
    private static EngineSignal Action(RuleContext ctx, SimEvent ev)
    {
        var payload = (ActionStartedPayload)ev.Payload;
        if (!ctx.Defs.Actions.TryGetValue(payload.ActionId, out var action))
        {
            throw new InvalidOperationException($"行動の定義がありません: {payload.ActionId}（seq {ev.Seq}）");
        }

        if (action.Effects.Handler == SnsPostHandler.Name)
        {
            return new StartEffectSignal(ev.Seq, Persona(ev), ev.Time + payload.Duration, payload.ActionId, payload.Place, payload.ActivityVersion);
        }

        return Planner(ev);
    }

    /// <summary>Planner のシグナル。トリガーの時刻はイベントの時刻。</summary>
    private static StartPlannerSignal Planner(SimEvent ev) => new(ev.Seq, Persona(ev), ev.Time);

    /// <summary>
    /// イベントのペルソナ。生成のイベントは行にペルソナを持たないので、定義 ID から引く（PersonaCreatedRule と同じ ID）。
    /// </summary>
    private static PersonaId Persona(SimEvent ev)
    {
        if (ev.Payload is PersonaCreatedPayload created)
        {
            return new PersonaId(created.DefId.Value);
        }

        return ev.PersonaId ?? throw new InvalidOperationException($"処理中のイベントにペルソナがありません（seq {ev.Seq}）");
    }
}
