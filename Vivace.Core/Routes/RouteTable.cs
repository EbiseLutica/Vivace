using System.Collections.Immutable;
using Vivace.Core.Domain;

namespace Vivace.Core.Routes;

/// <summary>全点対の最短経路のテーブル。閉鎖中の場所は、渡す場所の列に入れない。</summary>
public sealed class RouteTable
{
    private const int Unreachable = int.MaxValue / 4;

    private readonly ImmutableArray<PlaceId> _places;
    private readonly ImmutableDictionary<PlaceId, int> _index;
    private readonly int[,] _distance;
    private readonly int[,] _next;

    /// <summary>計算済みの距離と次の場所から作る。</summary>
    private RouteTable(ImmutableArray<PlaceId> places, int[,] distance, int[,] next)
    {
        _places = places;
        _index = places.Select((place, i) => (place, i)).ToImmutableDictionary(pair => pair.place, pair => pair.i);
        _distance = distance;
        _next = next;
    }

    /// <summary>このテーブルに入っている場所。</summary>
    public ImmutableArray<PlaceId> Places => _places;

    /// <summary>
    /// 場所の列と辺の列から、最短経路のテーブルを作る。辺の端が場所の列にないものは無視する。
    /// </summary>
    /// <param name="places">使ってよい場所（閉鎖中のものは入れない）。</param>
    /// <param name="edges">辺の列。同じ組が複数あれば、いちばん短いものを使う。</param>
    /// <exception cref="ArgumentOutOfRangeException">移動時間が正でない辺がある。</exception>
    public static RouteTable Create(IEnumerable<PlaceId> places, IEnumerable<RouteEdge> edges)
    {
        var placeList = places.Distinct().OrderBy(place => place.Value, StringComparer.Ordinal).ToImmutableArray();
        var index = placeList.Select((place, i) => (place, i)).ToDictionary(pair => pair.place, pair => pair.i);
        var n = placeList.Length;

        var distance = new int[n, n];
        var next = new int[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                distance[i, j] = i == j ? 0 : Unreachable;
                next[i, j] = j;
            }
        }

        foreach (var edge in edges)
        {
            if (edge.Minutes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(edges), $"移動時間は正である必要があります: {edge.A} - {edge.B}");
            }

            if (!index.TryGetValue(edge.A, out var a) || !index.TryGetValue(edge.B, out var b) || a == b)
            {
                continue;
            }

            if (edge.Minutes < distance[a, b])
            {
                distance[a, b] = edge.Minutes;
                distance[b, a] = edge.Minutes;
                next[a, b] = b;
                next[b, a] = a;
            }
        }

        for (var k = 0; k < n; k++)
        {
            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < n; j++)
                {
                    if (distance[i, k] >= Unreachable || distance[k, j] >= Unreachable)
                    {
                        continue;
                    }

                    var through = distance[i, k] + distance[k, j];
                    if (through < distance[i, j])
                    {
                        distance[i, j] = through;
                        next[i, j] = next[i, k];
                    }
                }
            }
        }

        return new RouteTable(placeList, distance, next);
    }

    /// <summary>
    /// 2 つの場所の間の最短の移動時間（分）。つながらなければ null。
    /// </summary>
    /// <param name="from">出発地。</param>
    /// <param name="to">目的地。</param>
    public int? Minutes(PlaceId from, PlaceId to)
    {
        if (!_index.TryGetValue(from, out var i) || !_index.TryGetValue(to, out var j))
        {
            return null;
        }

        return _distance[i, j] >= Unreachable ? null : _distance[i, j];
    }

    /// <summary>最短経路を辺の列で返す。つながらなければ null。出発と同じ場所なら空の列。</summary>
    /// <param name="from">出発地。</param>
    /// <param name="to">目的地。</param>
    public ImmutableArray<RouteStep>? Steps(PlaceId from, PlaceId to)
    {
        var path = Path(from, to);
        if (path is null)
        {
            return null;
        }

        var steps = ImmutableArray.CreateBuilder<RouteStep>();
        for (var i = 0; i + 1 < path.Value.Length; i++)
        {
            var minutes = Minutes(path.Value[i], path.Value[i + 1]) ?? 0;
            steps.Add(new RouteStep(path.Value[i], path.Value[i + 1], minutes));
        }

        return steps.ToImmutable();
    }

    /// <summary>
    /// 最短経路の場所の列（出発地と目的地を含む）。つながらなければ null。
    /// </summary>
    /// <param name="from">出発地。</param>
    /// <param name="to">目的地。</param>
    public ImmutableArray<PlaceId>? Path(PlaceId from, PlaceId to)
    {
        if (!_index.TryGetValue(from, out var i) || !_index.TryGetValue(to, out var j) || _distance[i, j] >= Unreachable)
        {
            return null;
        }

        var path = ImmutableArray.CreateBuilder<PlaceId>();
        path.Add(from);
        var current = i;
        while (current != j)
        {
            current = _next[current, j];
            path.Add(_places[current]);
        }

        return path.ToImmutable();
    }
}
