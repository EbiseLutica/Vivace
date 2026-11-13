namespace Vivace.Core.Events.Payloads;

/// <summary>状態の解除。</summary>
/// <param name="ConditionId">状態の定義 ID。</param>
public sealed record ConditionDetachedPayload(string ConditionId) : EventPayload(1);
