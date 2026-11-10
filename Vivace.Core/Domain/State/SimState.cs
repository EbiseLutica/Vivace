using System.Collections.Immutable;

namespace Vivace.Core.Domain;

/// <summary>シミュレーションの全体の状態（不変）。</summary>
/// <param name="Position">最後に適用した位置。</param>
/// <param name="Worlds">ワールドの状態。</param>
/// <param name="Personas">ペルソナの状態。</param>
public sealed record SimState(
    Position Position,
    ImmutableDictionary<WorldId, WorldState> Worlds,
    ImmutableDictionary<PersonaId, PersonaState> Personas)
{
    /// <summary>ペルソナの ID で、生きているペルソナの状態を取り出す。ID がない、または生きていなければ null。</summary>
    /// <param name="id">ペルソナの ID。</param>
    public PersonaState? AlivePersona(PersonaId? id) =>
        id is { } key && Personas.TryGetValue(key, out var found) && found.Status == PersonaStatus.Alive ? found : null;

    /// <summary>ペルソナの状態を差し替えた全体の状態を返す。</summary>
    /// <param name="persona">差し替えるペルソナの状態。</param>
    public SimState WithPersona(PersonaState persona) =>
        this with { Personas = Personas.SetItem(persona.Id, persona) };
}
