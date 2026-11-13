using Vivace.Core.Domain;

namespace Vivace.Core.Events.Payloads;

/// <summary>ワールドの生成。</summary>
/// <param name="DefId">ワールドの定義 ID。</param>
/// <param name="Seed">乱数のワールドの種。</param>
public sealed record WorldCreatedPayload(WorldDefId DefId, ulong Seed) : EventPayload(1);
