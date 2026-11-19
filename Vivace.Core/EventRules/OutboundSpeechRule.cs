using Vivace.Core.Domain.State;
using Vivace.Core.Events;
using Vivace.Core.Events.Payloads;

namespace Vivace.Core.EventRules;

/// <summary>発話を当てる。投稿の記録（最後の投稿と直近の投稿）を更新する。送信はしない。</summary>
internal sealed class OutboundSpeechRule : EventRule<OutboundSpeechPayload>
{
    /// <summary>扱うイベントタイプ。</summary>
    public override string EventType => EventTypes.OutboundSpeech;

    /// <summary>発話を当てる。投稿の記録を更新する。</summary>
    protected override ApplyResult Apply(RuleContext ctx, SimState state, SimEvent ev, OutboundSpeechPayload payload)
    {
        if (state.AlivePersona(ev.PersonaId) is not { } persona)
        {
            return ApplyResult.Skipped(state, "ペルソナがない");
        }

        return ApplyResult.Applied(state.WithPersona(persona with { Posting = persona.Posting.Posted(ev.Time, payload.Body) }));
    }
}
