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

    /// <summary>
    /// 固定・計画のブロックの行動の表示名。ブロック ID（定義 ID、同じ日の2回目以降は ".N"、"@日付" が続く）からペルソナの定義のブロックを引き、行動は親のブロックから引き継ぐ。引けなければ null。
    /// </summary>
    /// <param name="persona">ペルソナの ID。</param>
    /// <param name="blockId">ブロックの ID。</param>
    public string? BlockAction(PersonaId persona, string blockId)
    {
        var at = blockId.LastIndexOf('@');
        if (at < 0 || !definitions.Personas.TryGetValue(new PersonaDefId(persona.Value), out var def))
        {
            return null;
        }

        var blocks = def.Schedule.Blocks.Concat(def.Schedule.Exceptions.SelectMany(exception => exception.Blocks)).ToList();
        var defId = StripRepeat(blockId[..at]);
        var actionId = ActionOf(blocks.FirstOrDefault(block => block.Id == defId), blocks);
        return actionId is not null && definitions.Actions.TryGetValue(actionId, out var action) ? action.Display : null;
    }

    /// <summary>行動の定義 ID の表示名（行動の display）。</summary>
    /// <param name="actionId">行動の定義 ID。</param>
    public string Action(string actionId) =>
        definitions.Actions.TryGetValue(actionId, out var def) ? def.Display : actionId;

    /// <summary>ブロックの行動の定義 ID。ブロックに書いていなければ、親のブロックをたどる。</summary>
    /// <param name="block">ブロック。</param>
    /// <param name="blocks">同じペルソナのブロックの一覧（親を探す）。</param>
    private static string? ActionOf(BlockDef? block, IReadOnlyList<BlockDef> blocks)
    {
        var current = block;
        for (var steps = 0; current is not null && steps <= blocks.Count; steps++)
        {
            if (current.Action is { } action)
            {
                return action;
            }

            current = current.Within is { } parent ? blocks.FirstOrDefault(candidate => candidate.Id == parent) : null;
        }

        return null;
    }

    /// <summary>同じ日の2回目以降のブロックの ".N" の付け足しを外す。</summary>
    /// <param name="id">ブロックの定義 ID（付け足しを含むことがある）。</param>
    private static string StripRepeat(string id)
    {
        var dot = id.LastIndexOf('.');
        return dot > 0 && dot < id.Length - 1 && id[(dot + 1)..].All(char.IsAsciiDigit) ? id[..dot] : id;
    }
}
