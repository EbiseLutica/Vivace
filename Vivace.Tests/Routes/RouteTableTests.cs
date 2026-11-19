using Vivace.Core.Domain;
using Vivace.Core.Routes;

namespace Vivace.Tests.Routes;

/// <summary>最短経路のテーブル（Floyd–Warshall）を、テスト用の小さな町で確かめる。</summary>
public class RouteTableTests
{
    private static readonly PlaceId TaroRoom = new("taro_room");
    private static readonly PlaceId HomeLiving = new("home_living");
    private static readonly PlaceId Street = new("street");
    private static readonly PlaceId Park = new("park");
    private static readonly PlaceId Cafe = new("cafe");
    private static readonly PlaceId School = new("school_2a");

    private static readonly RouteEdge[] Edges =
    [
        new(TaroRoom, HomeLiving, 1),
        new(HomeLiving, Street, 3),
        new(Street, Park, 10),
        new(Street, School, 15),
        new(Street, Cafe, 5),
    ];

    /// <summary>最短の移動時間と、経路の場所の列が求まる。</summary>
    [Fact]
    public void FindsShortestMinutesAndPath()
    {
        var table = RouteTable.Create([TaroRoom, HomeLiving, Street, Park, Cafe, School], Edges);

        Assert.Equal(19, table.Minutes(TaroRoom, School));
        Assert.Equal(0, table.Minutes(Park, Park));
        var path = table.Path(TaroRoom, School);
        Assert.NotNull(path);
        Assert.Equal(new[] { TaroRoom, HomeLiving, Street, School }, path.Value);
    }

    /// <summary>同じ組の区間が複数あれば、いちばん短いものを使う。</summary>
    [Fact]
    public void ParallelEdgesUseTheShortest()
    {
        var table = RouteTable.Create(
            [TaroRoom, HomeLiving],
            [new RouteEdge(TaroRoom, HomeLiving, 4), new RouteEdge(HomeLiving, TaroRoom, 2)]);

        Assert.Equal(2, table.Minutes(TaroRoom, HomeLiving));
    }

    /// <summary>閉鎖中の場所は、場所の列に入れなければ、行き先にも経路の途中にもならない。</summary>
    [Fact]
    public void ClosedPlaceIsNotRoutedThrough()
    {
        var table = RouteTable.Create([TaroRoom, HomeLiving, Park, School], Edges);

        Assert.Null(table.Minutes(TaroRoom, School));
        Assert.Null(table.Path(TaroRoom, School));
        Assert.Null(table.Minutes(TaroRoom, Cafe));
    }

    /// <summary>道路の工事などで区間の時間を変えたテーブルでは、その区間を通る経路の時間が変わる。</summary>
    [Fact]
    public void SlowedEdgeChangesTheRouteTime()
    {
        var slowed = Edges.Select(edge => edge.A == Street && edge.B == School ? edge with { Minutes = 30 } : edge).ToArray();

        var table = RouteTable.Create([TaroRoom, HomeLiving, Street, School], slowed);

        Assert.Equal(34, table.Minutes(TaroRoom, School));
    }

    /// <summary>つながらない場所は、移動時間も経路も null になる。</summary>
    [Fact]
    public void UnconnectedPlacesHaveNoRoute()
    {
        var table = RouteTable.Create([TaroRoom, Park], []);

        Assert.Null(table.Minutes(TaroRoom, Park));
        Assert.Null(table.Path(TaroRoom, Park));
    }

    /// <summary>移動時間が正でない区間は、作れない。</summary>
    [Fact]
    public void NonPositiveEdgeIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RouteTable.Create([TaroRoom, HomeLiving], [new RouteEdge(TaroRoom, HomeLiving, 0)]));
    }

    /// <summary>経路は、移動時間つきの区間の列として取れる。</summary>
    [Fact]
    public void StepsGiveEdgesWithMinutes()
    {
        var table = RouteTable.Create([TaroRoom, HomeLiving, Street, School], Edges);

        var steps = table.Steps(TaroRoom, School);

        Assert.NotNull(steps);
        Assert.Equal(
            new[]
            {
                new RouteStep(TaroRoom, HomeLiving, 1),
                new RouteStep(HomeLiving, Street, 3),
                new RouteStep(Street, School, 15),
            },
            steps.Value);
    }

    /// <summary>同じ長さの経路が2つあっても、場所の渡す順に関係なく同じ経路を選ぶ。</summary>
    [Fact]
    public void EqualLengthRoutesDoNotDependOnInputOrder()
    {
        var a = new PlaceId("a");
        var b = new PlaceId("b");
        var c = new PlaceId("c");
        var d = new PlaceId("d");
        var edges = new[] { new RouteEdge(a, b, 1), new RouteEdge(b, d, 1), new RouteEdge(a, c, 1), new RouteEdge(c, d, 1) };

        var forward = RouteTable.Create([a, b, c, d], edges).Path(a, d);
        var backward = RouteTable.Create([d, c, b, a], edges.Reverse()).Path(a, d);

        Assert.NotNull(forward);
        Assert.Equal(forward, backward);
    }
}
