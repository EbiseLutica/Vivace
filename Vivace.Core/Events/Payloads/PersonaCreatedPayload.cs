using Vivace.Core.Domain;

namespace Vivace.Core.Events;

/// <summary>ペルソナの生成。</summary>
/// <param name="DefId">ペルソナの定義 ID。</param>
/// <param name="Home">最初の居場所（家）。</param>
public sealed record PersonaCreatedPayload(PersonaDefId DefId, PlaceId Home) : EventPayload(1);
