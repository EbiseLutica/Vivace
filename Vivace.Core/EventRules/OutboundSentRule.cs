using Vivace.Core.Events;
using Vivace.Core.Domain.State;
using Vivace.Core.Events.Payloads;

namespace Vivace.Core.EventRules;

/// <summary>送信の完了を当てる。未送信の操作を外す。</summary>
internal sealed class OutboundSentRule : EventRule<OutboundSentPayload>
{
    /// <summary>扱うイベントタイプ。</summary>
    public override string EventType => EventTypes.OutboundSent;

    /// <summary>未送信の操作がなければ、投入を拒否する。</summary>
    protected override string? CheckInjection(RuleContext ctx, SimState state, IEnumerable<SimEvent> pending, OutboundSentPayload payload, DateTimeOffset at) =>
        state.PersonaWithOutbound(payload.OperationSeq) is null ? "未送信の操作がない" : null;

    /// <summary>未送信の操作を外す。</summary>
    protected override ApplyResult Apply(RuleContext ctx, SimState state, SimEvent ev, OutboundSentPayload payload)
    {
        if (state.PersonaWithOutbound(payload.OperationSeq) is not { } owner)
        {
            return ApplyResult.Skipped(state, "未送信の操作がない");
        }

        return ApplyResult.Applied(state.WithPersona(owner.WithoutOutbound(payload.OperationSeq)));
    }
}
