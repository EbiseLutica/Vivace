using Vivace.Core.Events;
using Vivace.Core.Domain.State;
using Vivace.Core.Events.Payloads;

namespace Vivace.Core.EventRules;

/// <summary>送信の失敗を当てる。未送信の操作を外し、次に投稿できる時刻があれば記録する。</summary>
internal sealed class OutboundFailedRule : EventRule<OutboundFailedPayload>
{
    /// <summary>扱うイベントタイプ。</summary>
    public override string EventType => EventTypes.OutboundFailed;

    /// <summary>未送信の操作がなければ、投入を拒否する。</summary>
    protected override string? CheckInjection(RuleContext ctx, SimState state, IEnumerable<SimEvent> pending, OutboundFailedPayload payload, DateTimeOffset at) =>
        state.PersonaWithOutbound(payload.OperationSeq) is null ? "未送信の操作がない" : null;

    /// <summary>未送信の操作を外す。次に投稿できる時刻があれば、投稿の状態に記録する。</summary>
    protected override ApplyResult Apply(RuleContext ctx, SimState state, SimEvent ev, OutboundFailedPayload payload)
    {
        if (state.PersonaWithOutbound(payload.OperationSeq) is not { } owner)
        {
            return ApplyResult.Skipped(state, "未送信の操作がない");
        }

        var posting = payload.RetryAfter is { } after ? owner.Posting with { RemoteNotBefore = after } : owner.Posting;
        return ApplyResult.Applied(state.WithPersona(owner.WithoutOutbound(payload.OperationSeq) with { Posting = posting }));
    }
}
