namespace Vivace.Core.Domain;

/// <summary>ペルソナに付いている状態（風邪など）。</summary>
/// <param name="ConditionId">状態の定義 ID。</param>
/// <param name="Seq">付けたイベントの連番。</param>
/// <param name="EndsAt">外れる時刻。持続時間がなければ null。</param>
public sealed record AttachedCondition(string ConditionId, long Seq, DateTimeOffset? EndsAt);
