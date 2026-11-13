using Vivace.Core.Domain;

namespace Vivace.Core.Events.Payloads;

/// <summary>出来事の発生。解決済みの効果を持つので、定義を引かずに解消できる。</summary>
/// <param name="DefId">出来事の定義 ID。その場の定義（語彙だけで書かれたもの）なら null。</param>
/// <param name="Persona">対象のペルソナ（ペルソナ向けのとき）。</param>
/// <param name="Duration">持続時間。なければ null。</param>
/// <param name="Effects">解決済みの効果（対象の場所を含む）。</param>
public sealed record IncidentOccurredPayload(
    string? DefId,
    PersonaId? Persona,
    TimeSpan? Duration,
    ResolvedIncidentEffects Effects) : EventPayload(1);
