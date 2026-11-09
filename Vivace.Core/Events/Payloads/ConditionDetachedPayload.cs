namespace Vivace.Core.Events;

/// <summary>状態の解除（段 3）。</summary>
/// <param name="ConditionId">状態の定義 ID。</param>
public sealed record ConditionDetachedPayload(string ConditionId) : EventPayload(1);
