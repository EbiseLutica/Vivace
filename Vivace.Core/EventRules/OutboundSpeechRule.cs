using System.Collections.Immutable;
using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;
using Vivace.Core.Signals;

namespace Vivace.Core.EventRules;

/// <summary>発話を当てる。投稿の記録を更新し、未送信の操作を作って配信を依頼する。</summary>
internal sealed class OutboundSpeechRule : EventRule<OutboundSpeechPayload>
{
    /// <summary>扱うイベントタイプ。</summary>
    public override string EventType => EventTypes.OutboundSpeech;

    /// <summary>発話を当てる。投稿の記録を更新し、未送信の操作を積んで、配信のシグナルを出す。</summary>
    protected override ApplyResult Apply(RuleContext ctx, SimState state, SimEvent ev, OutboundSpeechPayload payload)
    {
        if (state.AlivePersona(ev.PersonaId) is not { } persona)
        {
            return ApplyResult.Skipped(state, "ペルソナがない");
        }

        var operation = OutboundOp.FromSpeech(ev.Seq, payload);
        var updated = persona with
        {
            Posting = persona.Posting.Posted(ev.Time, payload.Body),
            Outbound = persona.Outbound.Add(operation),
        };
        return ApplyResult.Applied(state.WithPersona(updated), signals: [new DeliverSignal(persona.Id, operation)]);
    }
}
