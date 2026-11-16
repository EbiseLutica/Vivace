using Vivace.Core.Definitions.Model;
using Vivace.Core.Domain;

namespace Vivace.Cli;

/// <summary>
/// 定義から、ID を表示名に変える。名前がなければ ID をそのまま返す。ワールドの ID は定義の ID と同じ値、ペルソナの ID も定義の ID と同じ値。
/// </summary>
public sealed class DisplayNames(DefinitionPack definitions)
{
    /// <summary>ペルソナの ID の表示名（定義の名前）。</summary>
    /// <param name="id">ペルソナの ID。</param>
    public string Persona(PersonaId id) =>
        definitions.Personas.TryGetValue(new PersonaDefId(id.Value), out var def) ? def.Name : id.Value;

    /// <summary>ワールドの ID の表示名（定義の名前）。</summary>
    /// <param name="id">ワールドの ID。</param>
    public string World(WorldId id) =>
        definitions.Worlds.TryGetValue(new WorldDefId(id.Value), out var def) ? def.Name : id.Value;

    /// <summary>
    /// 場所の ID の表示名（定義の場所の名前）。ワールドが分かれば、まずそのワールドの場所で引く。なければ、ワールドの ID の順に探す。
    /// </summary>
    /// <param name="id">場所の ID。</param>
    /// <param name="world">場所のあるワールド。分からなければ null。</param>
    public string Place(PlaceId id, WorldId? world)
    {
        if (world is { } owner && definitions.Worlds.TryGetValue(new WorldDefId(owner.Value), out var ownerDef)
            && ownerDef.Places.FirstOrDefault(place => place.Id == id.Value) is { } own)
        {
            return own.Name;
        }

        foreach (var key in definitions.Worlds.Keys.OrderBy(key => key.Value, StringComparer.Ordinal))
        {
            if (definitions.Worlds[key].Places.FirstOrDefault(place => place.Id == id.Value) is { } found)
            {
                return found.Name;
            }
        }

        return id.Value;
    }

    /// <summary>ペルソナが住むワールドの ID（ペルソナの定義のワールド）。定義がなければ null。</summary>
    /// <param name="id">ペルソナの ID。</param>
    public WorldId? WorldOf(PersonaId id) =>
        definitions.Personas.TryGetValue(new PersonaDefId(id.Value), out var def) ? new WorldId(def.World.Value) : null;

    /// <summary>行動の定義 ID の表示名（行動の display）。</summary>
    /// <param name="actionId">行動の定義 ID。</param>
    public string Action(string actionId) =>
        definitions.Actions.TryGetValue(actionId, out var def) ? def.Display : actionId;
}
