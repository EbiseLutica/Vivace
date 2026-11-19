using Vivace.Core.Definitions;

namespace Vivace.Tests.Definitions;

/// <summary>いま判定できる警告を、位置つきで確かめる。警告があっても定義は読める。</summary>
public class DefinitionWarningTests
{
    /// <summary>未知のキーは警告にし、キーの位置を出す。定義は読める。</summary>
    [Fact]
    public void UnknownKeyIsWarnedAtKey()
    {
        var files = DefinitionFixtures.Replaced("worlds/testville.toml", "name = \"テストの町\"", "name = \"テストの町\"\nnikki = 1");
        var location = DefinitionFixtures.At(files, "worlds/testville.toml", "nikki = 1");

        var result = DefinitionLoader.LoadFiles(files);

        var issue = DefinitionFixtures.Find(result, DefinitionIssueCode.UnknownKey, location);
        Assert.Equal(IssueSeverity.Warning, issue.Severity);
        Assert.NotNull(result.Definitions);
    }

    /// <summary>どの行動の requires にも出てこない場所のタグは警告にする。</summary>
    [Fact]
    public void UnusedPlaceTagIsWarned()
    {
        var files = DefinitionFixtures.Replaced("worlds/testville.toml", "tags = [\"desk\"]", "tags = [\"desk\", \"bed\"]");
        var location = DefinitionFixtures.At(files, "worlds/testville.toml", "tags = [\"desk\", \"bed\"]");

        var result = DefinitionLoader.LoadFiles(files);

        var issue = DefinitionFixtures.Find(result, DefinitionIssueCode.UnusedPlaceTag, location);
        Assert.Contains("bed", issue.Message);
        Assert.NotNull(result.Definitions);
    }

    /// <summary>requires のタグを、ペルソナのワールドのどの場所も持たなければ警告にする。</summary>
    [Fact]
    public void UnsatisfiableRequirementIsWarned()
    {
        var files = DefinitionFixtures.Replaced("worlds/testville.toml", "tags = [\"park\"]", "tags = [\"grass\"]");

        var result = DefinitionLoader.LoadFiles(files);

        Assert.Contains(result.Issues, issue =>
            issue.Code == DefinitionIssueCode.UnsatisfiableRequirement && issue.Message.Contains("\"park\"", StringComparison.Ordinal));
        Assert.NotNull(result.Definitions);
    }

    /// <summary>同行を片側にしか書いていなければ、同行として扱わない旨の警告を、同行のキーに出す。</summary>
    [Fact]
    public void OneSidedWithIsWarnedAtWithKey()
    {
        var files = DefinitionFixtures.Replaced("personas/hanako.toml", "with = [\"taro\"]", string.Empty);
        var location = DefinitionFixtures.At(files, "personas/taro.toml", "with = [\"hanako\"]");

        var result = DefinitionLoader.LoadFiles(files);

        var issue = DefinitionFixtures.Find(result, DefinitionIssueCode.OneSidedWith, location);
        Assert.Equal(IssueSeverity.Warning, issue.Severity);
        Assert.NotNull(result.Definitions);
    }
}
