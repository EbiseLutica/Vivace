using Vivace.Cli;
using Vivace.Core.Definitions;
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
        Assert.Equal("詩織", Names.Persona(new PersonaId("shiori")));
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

    /// <summary>
    /// ブロックの行動は、ペルソナの定義のブロックから引く。同じ日の2回目の ".N" は外し、行動の書いていないブロックは親から引き継ぐ。引けなければ null。
    /// </summary>
    [Fact]
    public void BlockActionFindsActivityOfPersonaBlock()
    {
        var kaori = new PersonaId("kaori");

        Assert.Equal("授業を受ける", Names.BlockAction(kaori, "school@2026-11-02"));
        Assert.Equal("授業を受ける", Names.BlockAction(kaori, "school.3@2026-11-02"));
        Assert.Equal("眠る", Names.BlockAction(kaori, "sleep_weekday@2026-11-02"));
        Assert.Null(Names.BlockAction(kaori, "no_such@2026-11-02"));
        Assert.Null(Names.BlockAction(kaori, "school"));
        Assert.Null(Names.BlockAction(new PersonaId("nobody"), "school@2026-11-02"));
    }

    /// <summary>行動の書いていないブロックは、親のブロックの行動を引き継ぐ（short_break は within = school）。</summary>
    [Fact]
    public void BlockWithoutActionInheritsParentActivity()
    {
        Assert.Equal("授業を受ける", Names.BlockAction(new PersonaId("kaori"), "short_break@2026-11-02"));
    }

    /// <summary>
    /// 例外日のブロックは、その日付で引く。別の日付では例外のブロックは見ず、通常のブロックを引く（xmas_party は 2026-12-24 だけ）。
    /// </summary>
    [Fact]
    public void ExceptionBlockIsFoundOnItsDateOnly()
    {
        var kaori = new PersonaId("kaori");

        Assert.Equal("テレビを見る", Names.BlockAction(kaori, "xmas_party@2026-12-24"));
        Assert.Null(Names.BlockAction(kaori, "xmas_party@2026-12-23"));
    }

    /// <summary>
    /// 例外日のブロックと通常のブロックが同じ ID のとき、日付で選ぶ。例外日は例外の行動（テレビを見る）、ほかの日は通常の行動（授業を受ける）。
    /// </summary>
    [Fact]
    public void ExceptionWithSameIdAsNormalBlockIsChosenByDate()
    {
        var names = WithKaoriExceptions("""

            [[schedule.exception]]
            date = "2026-12-25"

            [[schedule.exception.block]]
            id = "school"
            from = "08:30"
            to = "15:40"
            action = "watch_tv"
            place = "home_living"
            """);
        var kaori = new PersonaId("kaori");

        Assert.Equal("テレビを見る", names.BlockAction(kaori, "school@2026-12-25"));
        Assert.Equal("授業を受ける", names.BlockAction(kaori, "school@2026-11-02"));
    }

    /// <summary>テスト用の定義の kaori に、例外の書き足しを付けた名前の表を作る。</summary>
    /// <param name="extra">kaori.toml の末尾に足す TOML。</param>
    private static DisplayNames WithKaoriExceptions(string extra)
    {
        var files = TestWorld.Files()
            .Select(file => file.Path == "personas/kaori.toml" ? file with { Text = file.Text + extra } : file)
            .ToList();
        var result = DefinitionLoader.LoadFiles(files);
        return new DisplayNames(result.Definitions ?? throw new InvalidOperationException(string.Join("\n", result.Issues)));
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
