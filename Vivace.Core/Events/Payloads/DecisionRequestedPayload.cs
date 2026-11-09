namespace Vivace.Core.Events;

/// <summary>決めどきの要求（段 7）。</summary>
/// <param name="Reason">理由。</param>
/// <param name="BlockId">関係するブロックの ID（あれば）。</param>
public sealed record DecisionRequestedPayload(DecisionReason Reason, string? BlockId) : EventPayload(1);
