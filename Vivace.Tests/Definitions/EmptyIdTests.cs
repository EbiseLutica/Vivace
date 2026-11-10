using Vivace.Core.Definitions;

namespace Vivace.Tests.Definitions;

/// <summary>ID が読めなかった定義は登録せず、ID のエラーだけを出すことを確かめる。</summary>
public class EmptyIdTests
{
    private const string EmptyIdMessage = "ID は空にできません。";

    /// <summary>ID が空のワールドが2つあっても、空の ID のエラーが1件ずつ出るだけで、重複のエラーは重ならない。</summary>
    [Fact]
    public void TwoWorldsWithEmptyIdAreNotReportedAsDuplicate()
    {
        var files = WithCopies("worlds/testville.toml", "id = \"testville\"", "id = \"\"", "worlds/blank1.toml", "worlds/blank2.toml");

        var result = DefinitionLoader.LoadFiles(files);

        AssertIssues(
            result,
            (DefinitionIssueCode.InvalidValue, new SourceLocation("worlds/blank1.toml", 2, 1), EmptyIdMessage),
            (DefinitionIssueCode.InvalidValue, new SourceLocation("worlds/blank2.toml", 2, 1), EmptyIdMessage));
    }

    /// <summary>ID が空のペルソナが2つあっても、空の ID のエラーが1件ずつ出るだけで、重複のエラーは重ならない。</summary>
    [Fact]
    public void TwoPersonasWithEmptyIdAreNotReportedAsDuplicate()
    {
        var files = WithCopies("personas/shiori.toml", "id = \"shiori\"", "id = \"\"", "personas/blank1.toml", "personas/blank2.toml");

        var result = DefinitionLoader.LoadFiles(files);

        AssertIssues(
            result,
            (DefinitionIssueCode.InvalidValue, new SourceLocation("personas/blank1.toml", 2, 1), EmptyIdMessage),
            (DefinitionIssueCode.InvalidValue, new SourceLocation("personas/blank2.toml", 2, 1), EmptyIdMessage));
    }

    /// <summary>ID が空の行動、出来事、状態が同じファイルに2つあっても、空の ID のエラーが1件ずつ出るだけで、重複のエラーは重ならない。</summary>
    [Theory]
    [InlineData("actions/blank.toml", "[[action]]\nid = \"\"\ndisplay = \"無名\"\nduration = \"10m\"\n", 6)]
    [InlineData("incidents/blank.toml", "[[incident]]\nid = \"\"\ndisplay = \"無名\"\ntarget = \"world\"\nchance = 0.01\nduration = \"1h\"\ncooldown = \"1d\"\n", 9)]
    [InlineData("incidents/blank.toml", "[[condition]]\nid = \"\"\ndisplay = \"無名\"\n", 5)]
    public void TwoTablesWithEmptyIdAreNotReportedAsDuplicate(string path, string table, int secondIdLine)
    {
        var files = DefinitionFixtures.Original();
        files.Add(new DefinitionFile(path, table + table));

        var result = DefinitionLoader.LoadFiles(files);

        AssertIssues(
            result,
            (DefinitionIssueCode.InvalidValue, new SourceLocation(path, 2, 1), EmptyIdMessage),
            (DefinitionIssueCode.InvalidValue, new SourceLocation(path, secondIdLine, 1), EmptyIdMessage));
    }

    /// <summary>ID が読めなかったペルソナは登録されず、定義どうしの検証にも回らない。出る問題は ID の1件だけ。</summary>
    [Theory]
    [InlineData("id = \"\"", DefinitionIssueCode.InvalidValue, 2, EmptyIdMessage)]
    [InlineData("id = 5", DefinitionIssueCode.InvalidValue, 2, "id は文字列で書いてください。")]
    [InlineData("# id", DefinitionIssueCode.MissingRequired, 1, "必須の項目がありません: id")]
    public void PersonaWithoutReadableIdIsNotRegistered(string idLine, DefinitionIssueCode code, int line, string message)
    {
        var files = WithCopies("personas/shiori.toml", "id = \"shiori\"", idLine, "personas/blank.toml");

        var result = DefinitionLoader.LoadFiles(files);

        AssertIssues(result, (code, new SourceLocation("personas/blank.toml", line, 1), message));
    }

    /// <summary>テスト用の世界に、既存のファイルの1か所を書き換えた写しを、別のパスで足す。</summary>
    private static List<DefinitionFile> WithCopies(string source, string from, string to, params string[] paths)
    {
        var files = DefinitionFixtures.Original();
        var text = DefinitionFixtures.Replaced(source, from, to).Single(file => file.Path == source).Text;
        files.AddRange(paths.Select(path => new DefinitionFile(path, text)));
        return files;
    }

    /// <summary>問題の全件が、期待した種類、位置、説明の並びと一致し、定義が作られないことを確かめる。</summary>
    private static void AssertIssues(DefinitionLoadResult result, params (DefinitionIssueCode Code, SourceLocation Location, string Message)[] expected)
    {
        Assert.Equal(expected, result.Issues.Select(issue => (issue.Code, issue.Location, issue.Message)).ToArray());
        Assert.Null(result.Definitions);
    }
}
