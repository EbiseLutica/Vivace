using System.Collections.Immutable;
using Vivace.Core.Definitions;
using Vivace.Core.Domain;

namespace Vivace.Core.Routes;

/// <summary>
/// ワールドごとの最短経路の表。定義が変わるまで作り直さない。ワールドの ID は定義 ID と同じ。
/// 固定予定の隙間の警告は、定義の検証（<see cref="DefinitionValidator"/>）で出す（D7 7.2、J-147）。
/// </summary>
public sealed class RouteCache
{
    private readonly ImmutableDictionary<string, RouteTable> _tables;

    private RouteCache(ImmutableDictionary<string, RouteTable> tables)
    {
        _tables = tables;
    }

    /// <summary>定義から、すべてのワールドの最短経路の表を組む。</summary>
    /// <param name="definitions">読み込んだ定義。</param>
    public static RouteCache Create(Definitions.Definitions definitions)
    {
        var tables = ImmutableDictionary.CreateBuilder<string, RouteTable>(StringComparer.Ordinal);
        foreach (var world in definitions.Worlds.Values.OrderBy(w => w.Id, StringComparer.Ordinal))
        {
            tables[world.Id] = BuildTable(world);
        }

        return new RouteCache(tables.ToImmutable());
    }

    /// <summary>1つのワールドの、場所と辺から最短経路の表を組む。</summary>
    /// <param name="world">ワールドの定義。</param>
    public static RouteTable BuildTable(WorldDef world)
    {
        var places = world.Places.Select(place => new PlaceId(place.Id)).ToList();
        var edges = world.Places
            .SelectMany(place => place.Links.Select(link => new RouteEdge(new PlaceId(place.Id), new PlaceId(link.To), link.Minutes)))
            .ToList();
        return RouteTable.Create(places, edges);
    }

    /// <summary>ワールドの最短経路の表。定義にないワールドなら例外。</summary>
    /// <param name="world">ワールド ID。</param>
    public RouteTable For(WorldId world) => _tables[world.Value];
}
