namespace Vivace.Core.Events.Payloads;

/// <summary>決定タイミングの要求。</summary>
/// <param name="Reason">理由。</param>
/// <param name="BlockId">関係するブロックの ID（あれば）。</param>
public sealed record DecisionRequestedPayload(DecisionReason Reason, string? BlockId) : EventPayload(1);
