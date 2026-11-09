namespace Vivace.Core.Events;

/// <summary>ワールドの生成（段 3）。</summary>
/// <param name="DefId">ワールドの定義 ID。</param>
/// <param name="Seed">乱数のワールドの種。</param>
public sealed record WorldCreatedPayload(string DefId, ulong Seed) : EventPayload(1);
