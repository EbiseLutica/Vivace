using System.Collections.Immutable;

namespace Vivace.Core.Domain;

/// <summary>シミュレーションの全体の状態（不変）。</summary>
/// <param name="Cursor">最後に適用した位置。</param>
/// <param name="Worlds">ワールドの状態。</param>
/// <param name="Personas">ペルソナの状態。</param>
public sealed record SimState(
    ApplyCursor Cursor,
    ImmutableDictionary<WorldId, WorldState> Worlds,
    ImmutableDictionary<PersonaId, PersonaState> Personas)
{
    /// <summary>ペルソナの ID で、生きているペルソナの状態を取り出す。ID がない、または生きていなければ null。</summary>
    /// <param name="id">ペルソナの ID。</param>
    public PersonaState? AlivePersona(PersonaId? id) =>
        id is { } key && Personas.TryGetValue(key, out var found) && found.Status == PersonaStatus.Alive ? found : null;

    /// <summary>定義 ID で、生きているワールドの状態を取り出す。生きたものがなければ null。複数あれば ID の序数で先頭のもの。</summary>
    /// <param name="defId">ワールドの定義 ID。</param>
    public WorldState? AliveWorld(WorldDefId defId) =>
        Worlds.Values
            .Where(world => world.DefId == defId && world.Status == WorldStatus.Alive)
            .OrderBy(world => world.Id.Value, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>ペルソナの状態を差し替えた全体の状態を返す。</summary>
    /// <param name="persona">差し替えるペルソナの状態。</param>
    public SimState WithPersona(PersonaState persona) =>
        this with { Personas = Personas.SetItem(persona.Id, persona) };

    /// <summary>
    /// ペルソナのいまの居場所に、ほかの人がいるか。移動中の人は、どの場所にもいないものとして扱う。
    /// </summary>
    /// <param name="persona">ペルソナ。</param>
    public bool IsTogether(PersonaState persona) =>
        persona.Location is AtPlace at && AnyoneElseAt(at.Place, persona.Id);

    /// <summary>場所 place に、self 以外の生きている人がいるか。移動中の人は、どの場所にもいないものとして扱う。</summary>
    /// <param name="place">場所。</param>
    /// <param name="self">自分の ID（数えない）。</param>
    public bool AnyoneElseAt(PlaceId place, PersonaId self) =>
        Personas.Values.Any(other =>
            other.Id != self
            && other.Status == PersonaStatus.Alive
            && other.Location is AtPlace otherAt
            && otherAt.Place == place);
}
