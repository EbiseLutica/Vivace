using Vivace.Cli;
using Vivace.Core.Domain;
using Vivace.Testing;

namespace Vivace.Tests.Cli;

/// <summary>ID から表示名を引く。テスト用の定義の名前と、名前のないものは ID に戻ることを確かめる。</summary>
public class DisplayNamesTests
{
    private static readonly DisplayNames Names = new(TestWorld.LoadOrThrow());
    private static readonly WorldId Town = new("testville");

    /// <summary>ペルソナは定義の名前で引く。</summary>
    [Fact]
    public void PersonaUsesDefinitionName()
    {
        Assert.Equal("香織", Names.Persona(new PersonaId("kaori")));
        Assert.Equal("栞", Names.Persona(new PersonaId("shiori")));
    }

    /// <summary>ワールドは定義の名前で引く。</summary>
    [Fact]
    public void WorldUsesDefinitionName()
    {
        Assert.Equal("テストの町", Names.World(Town));
    }

    /// <summary>ペルソナのワールドは定義のワールドで、名前のないペルソナは null。</summary>
    [Fact]
    public void WorldOfPersonaUsesDefinitionWorld()
    {
        Assert.Equal(Town, Names.WorldOf(new PersonaId("kaori")));
        Assert.Null(Names.WorldOf(new PersonaId("nobody")));
    }

    /// <summary>場所は、ワールドが分かればそのワールドの場所の名前で引く。</summary>
    [Fact]
    public void PlaceUsesNameOfItsWorld()
    {
        Assert.Equal("香織の部屋", Names.Place(new PlaceId("kaori_room"), Town));
        Assert.Equal("2年A組の教室", Names.Place(new PlaceId("school_2a"), Town));
    }

    /// <summary>ワールドが分からなくても、定義のどこかの場所の名前で引く。</summary>
    [Fact]
    public void PlaceWithoutWorldSearchesDefinitions()
    {
        Assert.Equal("公園", Names.Place(new PlaceId("park"), null));
    }

    /// <summary>行動は、定義の行動の display で引く。組み込みの行動も同じ。</summary>
    [Fact]
    public void ActionUsesDisplay()
    {
        Assert.Equal("本を読む", Names.Action("read_book"));
        Assert.Equal("次の予定の場所へ向かう", Names.Action("core.go_to_next"));
    }

    /// <summary>名前のない ID は、ID をそのまま返す（落ちない）。</summary>
    [Fact]
    public void UnknownIdsReturnThemselves()
    {
        Assert.Equal("nobody", Names.Persona(new PersonaId("nobody")));
        Assert.Equal("no_such_world", Names.World(new WorldId("no_such_world")));
        Assert.Equal("no_such_place", Names.Place(new PlaceId("no_such_place"), Town));
        Assert.Equal("no_such_place", Names.Place(new PlaceId("no_such_place"), null));
        Assert.Equal("no.such", Names.Action("no.such"));
    }
}
