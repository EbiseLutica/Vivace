using Vivace.Core.Domain;
using Vivace.Core.Routes;

namespace Vivace.Tests.Routes;

/// <summary>最短経路の表（Floyd–Warshall）を、テスト用の小さな町で確かめる。</summary>
public class RouteTableTests
{
    private static readonly PlaceId KaoriRoom = new("kaori_room");
    private static readonly PlaceId HomeLiving = new("home_living");
    private static readonly PlaceId Street = new("street");
    private static readonly PlaceId Park = new("park");
    private static readonly PlaceId Cafe = new("cafe");
    private static readonly PlaceId School = new("school_2a");

    private static readonly RouteEdge[] Edges =
    [
        new(KaoriRoom, HomeLiving, 1),
        new(HomeLiving, Street, 3),
        new(Street, Park, 10),
        new(Street, School, 15),
        new(Street, Cafe, 5),
    ];

    /// <summary>最短の移動時間と、経路の場所の列が求まる。</summary>
    [Fact]
    public void FindsShortestMinutesAndPath()
    {
        var table = RouteTable.Create([KaoriRoom, HomeLiving, Street, Park, Cafe, School], Edges);

        Assert.Equal(19, table.Minutes(KaoriRoom, School));
        Assert.Equal(0, table.Minutes(Park, Park));
        var path = table.Path(KaoriRoom, School);
        Assert.NotNull(path);
        Assert.Equal(new[] { KaoriRoom, HomeLiving, Street, School }, path.Value);
    }

    /// <summary>同じ組の辺が複数あれば、いちばん短いものを使う。</summary>
    [Fact]
    public void ParallelEdgesUseTheShortest()
    {
        var table = RouteTable.Create(
            [KaoriRoom, HomeLiving],
            [new RouteEdge(KaoriRoom, HomeLiving, 4), new RouteEdge(HomeLiving, KaoriRoom, 2)]);

        Assert.Equal(2, table.Minutes(KaoriRoom, HomeLiving));
    }

    /// <summary>閉鎖中の場所は、場所の列に入れなければ、行き先にも経路の途中にもならない。</summary>
    [Fact]
    public void ClosedPlaceIsNotRoutedThrough()
    {
        var table = RouteTable.Create([KaoriRoom, HomeLiving, Park, School], Edges);

        Assert.Null(table.Minutes(KaoriRoom, School));
        Assert.Null(table.Path(KaoriRoom, School));
        Assert.Null(table.Minutes(KaoriRoom, Cafe));
    }

    /// <summary>道路の工事などで辺の時間を変えた表では、その辺を通る経路の時間が変わる。</summary>
    [Fact]
    public void SlowedEdgeChangesTheRouteTime()
    {
        var slowed = Edges.Select(edge => edge.A == Street && edge.B == School ? edge with { Minutes = 30 } : edge).ToArray();

        var table = RouteTable.Create([KaoriRoom, HomeLiving, Street, School], slowed);

        Assert.Equal(34, table.Minutes(KaoriRoom, School));
    }

    /// <summary>つながらない場所は、移動時間も経路も null になる。</summary>
    [Fact]
    public void UnconnectedPlacesHaveNoRoute()
    {
        var table = RouteTable.Create([KaoriRoom, Park], []);

        Assert.Null(table.Minutes(KaoriRoom, Park));
        Assert.Null(table.Path(KaoriRoom, Park));
    }

    /// <summary>移動時間が正でない辺は、作れない。</summary>
    [Fact]
    public void NonPositiveEdgeIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RouteTable.Create([KaoriRoom, HomeLiving], [new RouteEdge(KaoriRoom, HomeLiving, 0)]));
    }

    /// <summary>S9：経路は、移動時間つきの辺の列として取れる。</summary>
    [Fact]
    public void StepsGiveEdgesWithMinutes()
    {
        var table = RouteTable.Create([KaoriRoom, HomeLiving, Street, School], Edges);

        var steps = table.Steps(KaoriRoom, School);

        Assert.NotNull(steps);
        Assert.Equal(
            new[]
            {
                new RouteStep(KaoriRoom, HomeLiving, 1),
                new RouteStep(HomeLiving, Street, 3),
                new RouteStep(Street, School, 15),
            },
            steps.Value);
    }

    /// <summary>S12：同じ長さの経路が2つあっても、場所の渡す順に関係なく同じ経路を選ぶ。</summary>
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
