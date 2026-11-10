namespace Vivace.Core.Events;

/// <summary>状態の付与。</summary>
/// <param name="ConditionId">状態の定義 ID。</param>
/// <param name="Duration">持続時間。なければ null。</param>
public sealed record ConditionAttachedPayload(string ConditionId, TimeSpan? Duration) : EventPayload(1);
