namespace Vivace.Core.Domain.State;

/// <summary>発生中の出来事。解決済みの効果を持つので、解消のときに定義を引き直さなくてよい。</summary>
/// <param name="Seq">発生のイベントの連番（ID を兼ねる）。</param>
/// <param name="DefId">出来事の定義 ID。その場の定義なら null。</param>
/// <param name="Persona">対象のペルソナ（ペルソナ向けのとき）。</param>
/// <param name="StartedAt">発生した時刻。</param>
/// <param name="EndsAt">解消の時刻。持続時間がなければ null。</param>
/// <param name="Effects">解決済みの効果（対象の場所を含む）。</param>
public sealed record ActiveIncident(
    long Seq,
    string? DefId,
    PersonaId? Persona,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndsAt,
    ResolvedIncidentEffects Effects);
