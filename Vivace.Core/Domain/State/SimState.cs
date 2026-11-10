using System.Collections.Immutable;

namespace Vivace.Core.Domain;

/// <summary>シミュレーションの全体の状態（不変）。</summary>
/// <param name="Position">最後に適用した位置。</param>
/// <param name="Worlds">ワールドの状態。</param>
/// <param name="Personas">ペルソナの状態。</param>
public sealed record SimState(
    Position Position,
    ImmutableDictionary<WorldId, WorldState> Worlds,
    ImmutableDictionary<PersonaId, PersonaState> Personas);
