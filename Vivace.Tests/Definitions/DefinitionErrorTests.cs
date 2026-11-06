using Vivace.Core.Definitions;
using Vivace.Testing;

namespace Vivace.Tests.Definitions;

/// <summary>
/// D7 7.2 のエラーの各項目に、それを起こす壊れた定義を1本ずつ当てる。位置（ファイル、行、列）もそろえて確かめる。
/// </summary>
public class DefinitionErrorTests
{
    /// <summary>TOML の構文が壊れていれば、構文のエラーが行の位置つきで出る。</summary>
    [Fact]
    public void SyntaxErrorIsReportedWithLine()
    {
        var files = DefinitionFixtures.Replaced("personas/kaori.toml", "id = \"kaori\"", "id = = \"kaori\"");

        var result = DefinitionLoader.LoadFiles(files);

        Assert.True(result.HasErrors);
        Assert.All(result.Issues.Where(issue => issue.Code == DefinitionIssueCode.SyntaxError), issue => Assert.Equal("personas/kaori.toml", issue.Location.Path));
        Assert.Contains(result.Issues, issue => issue.Code == DefinitionIssueCode.SyntaxError && issue.Location.Line == 2);
        Assert.Null(result.Definitions);
    }

    /// <summary>必須の項目（ここでは場所の name）がなければ、その表の見出しの位置にエラーが出る。</summary>
    [Fact]
    public void MissingRequiredItemIsReportedAtTableHeader()
    {
        var files = DefinitionFixtures.Replaced("worlds/testville.toml", "name = \"居間\"\n", string.Empty);
        var header = DefinitionFixtures.At(files, "worlds/testville.toml", "[[place]]\nid = \"home_living\"");

        var result = DefinitionLoader.LoadFiles(files);

        var issue = DefinitionFixtures.Find(result, DefinitionIssueCode.MissingRequired, header);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Contains("name", issue.Message);
    }

    /// <summary>同じ ID の定義が2つあれば、後から読んだほうの位置にエラーが出る。</summary>
    [Fact]
    public void DuplicateIdIsReportedAtSecondDefinition()
    {
        var files = DefinitionFixtures.Original();
        var copy = files.Single(file => file.Path == "personas/kaori.toml") with { Path = "personas/kaori_copy.toml" };
        files.Add(copy);

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.DuplicateId, new SourceLocation("personas/kaori_copy.toml", 1, 1));
        Assert.Null(result.Definitions);
    }

    /// <summary>ユーザーの定義に名前空間（.）を書けば、名前空間の違反として、ID の位置にエラーが出る。</summary>
    [Fact]
    public void NamespaceViolationIsReportedAtId()
    {
        var files = DefinitionFixtures.Replaced("actions/actions.toml", "id = \"walk_park\"", "id = \"misskey.walk\"");
        var location = DefinitionFixtures.At(files, "actions/actions.toml", "id = \"misskey.walk\"");

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.Namespace, location);
    }

    /// <summary>
    /// 参照先がなければ、参照しているキーの位置にエラーが出る。辺、居住ワールド、固定予定の行動と場所、home、同行者の各場合。
    /// </summary>
    [Theory]
    [InlineData("worlds/testville.toml", "park = 10", "nowhere = 10")]
    [InlineData("personas/kaori.toml", "world = \"testville\"", "world = \"nowhere\"")]
    [InlineData("personas/kaori.toml", "action = \"core.sleep\"", "action = \"core.snooze\"")]
    [InlineData("personas/kaori.toml", "place = \"kaori_room\"", "place = \"nowhere\"")]
    [InlineData("personas/kaori.toml", "home = \"kaori_room\"", "home = \"nowhere\"")]
    [InlineData("personas/kaori.toml", "with = [\"shiori\"]", "with = [\"nobody\"]")]
    public void MissingReferenceIsReportedAtKey(string path, string needle, string replacement)
    {
        var files = DefinitionFixtures.Replaced(path, needle, replacement);
        var location = DefinitionFixtures.At(files, path, replacement);

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.MissingReference, location);
        Assert.Null(result.Definitions);
    }

    /// <summary>両側に書いた辺の移動時間が食い違えば、後から書いたほうの辺の位置にエラーが出る。</summary>
    [Fact]
    public void EdgeMismatchIsReportedAtLaterEdge()
    {
        var files = DefinitionFixtures.Replaced("worlds/testville.toml", "links = { street = 3 }", "links = { street = 3, kaori_room = 2 }");
        var location = DefinitionFixtures.At(files, "worlds/testville.toml", "kaori_room = 2");

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.EdgeMismatch, location);
    }

    /// <summary>固定予定のブロックが重なれば、後のブロックの見出しの位置にエラーが出る。</summary>
    [Fact]
    public void OverlappingFixedBlocksAreReported()
    {
        var files = DefinitionFixtures.Replaced(
            "personas/kaori.toml",
            "[[schedule.exception]]\n",
            "[[schedule.block]]\nid = \"clash\"\nfrom = \"09:00\"\nto = \"10:00\"\naction = \"walk_park\"\nplace = \"park\"\n\n[[schedule.exception]]\n");
        var header = DefinitionFixtures.At(files, "personas/kaori.toml", "[[schedule.block]]\nid = \"clash\"");

        var result = DefinitionLoader.LoadFiles(files);

        var issue = DefinitionFixtures.Find(result, DefinitionIssueCode.ScheduleOverlap, header);
        Assert.Contains("clash", issue.Message);
    }

    /// <summary>反射の行動の所要時間が範囲なら、所要時間のキーの位置にエラーが出る。</summary>
    [Fact]
    public void ReflexActionWithRangeDurationIsReported()
    {
        var files = DefinitionFixtures.Replaced("personas/kaori.toml", "action = \"core.go_home_rest\"", "action = \"walk_park\"");
        var location = DefinitionFixtures.At(files, "actions/actions.toml", "duration = [\"20m\", \"60m\"]");

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.ReflexRange, location);
    }

    /// <summary>core.go_to_next を forbid すれば、ペルソナの好みの位置にエラーが出る。</summary>
    [Fact]
    public void ForbiddingGoToNextIsReported()
    {
        var files = DefinitionFixtures.Replaced(
            "personas/kaori.toml",
            "prefs = { read_book = \"like\"",
            "prefs = { \"core.go_to_next\" = \"forbid\", read_book = \"like\"");
        var location = DefinitionFixtures.At(files, "personas/kaori.toml", "\"core.go_to_next\" = \"forbid\"");

        var result = DefinitionLoader.LoadFiles(files);

        DefinitionFixtures.Find(result, DefinitionIssueCode.ForbidGoToNext, location);
    }

    /// <summary>値の範囲の外（出来事の確率が 0 から 1 の外）は、値の位置にエラーが出る。</summary>
    [Fact]
    public void ValueOutOfRangeIsReportedAtValue()
    {
        var files = DefinitionFixtures.Replaced("incidents/incidents.toml", "chance = 0.03", "chance = 1.5");
        var location = DefinitionFixtures.At(files, "incidents/incidents.toml", "chance = 1.5");

        var result = DefinitionLoader.LoadFiles(files);

        var issue = DefinitionFixtures.Find(result, DefinitionIssueCode.InvalidValue, location);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
    }

    /// <summary>テスト用の世界は、読み込みのエラーも警告もなく読める。</summary>
    [Fact]
    public void TestWorldLoadsWithoutIssues()
    {
        var result = TestWorld.Load();

        Assert.Empty(result.Issues);
        Assert.NotNull(result.Definitions);
    }
}
