using System.Collections.Immutable;
using Vivace.Core.Definitions.Model;
using Vivace.Core.Domain;

namespace Vivace.Core.Routes;

/// <summary>ワールドごとの最短経路のテーブル。定義が変わるまで作り直さない。</summary>
public sealed class RouteCache
{
    private readonly ImmutableDictionary<WorldDefId, RouteTable> _tables;

    /// <summary>組み終えたテーブルから作る。</summary>
    private RouteCache(ImmutableDictionary<WorldDefId, RouteTable> tables)
    {
        _tables = tables;
    }

    /// <summary>定義から、すべてのワールドの最短経路のテーブルを組む。</summary>
    /// <param name="definitions">読み込んだ定義。</param>
    public static RouteCache Create(DefinitionPack definitions)
    {
        var tables = ImmutableDictionary.CreateBuilder<WorldDefId, RouteTable>();
        foreach (var world in definitions.Worlds.Values.OrderBy(w => w.Id.Value, StringComparer.Ordinal))
        {
            tables[world.Id] = BuildTable(world);
        }

        return new RouteCache(tables.ToImmutable());
    }

    /// <summary>1つのワールドの、場所と辺から最短経路のテーブルを組む。</summary>
    /// <param name="world">ワールドの定義。</param>
    public static RouteTable BuildTable(WorldDef world)
    {
        var places = world.Places.Select(place => new PlaceId(place.Id)).ToList();
        var edges = world.Places
            .SelectMany(place => place.Links.Select(link => new RouteEdge(new PlaceId(place.Id), new PlaceId(link.To), link.Minutes)))
            .ToList();
        return RouteTable.Create(places, edges);
    }

    /// <summary>ワールドの最短経路のテーブル。定義にないワールドなら例外。</summary>
    /// <param name="world">ワールドの定義 ID。</param>
    public RouteTable For(WorldDefId world) => _tables[world];
}
