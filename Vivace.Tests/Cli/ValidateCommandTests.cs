using Vivace.Cli;
using Vivace.Testing;
using Vivace.Tests.Support;

namespace Vivace.Tests.Cli;

/// <summary>vivace validate。テスト用の定義は問題なし、壊した定義は位置つきのエラーになる。</summary>
public class ValidateCommandTests
{
    /// <summary>テスト用の定義は、問題 0 件で終了コード 0 になる。</summary>
    [Fact]
    public void TestWorldHasNoIssuesAndExitsZero()
    {
        using var folder = new DefinitionFolder(TestWorld.Files());
        var output = new StringWriter { NewLine = "\n" };

        var code = ValidateCommand.Run(folder.Path, output);

        Assert.Equal(0, code);
        Assert.Equal("エラー 0 件、警告 0 件\n", output.ToString());
    }

    /// <summary>壊した定義は、位置つきのエラーを出し、終了コード 1 になる。</summary>
    [Fact]
    public void BrokenDefinitionReportsLocatedErrorAndExitsOne()
    {
        var files = TestWorld.Files().Select(file => file.Path == "worlds/testville.toml"
            ? file with { Text = file.Text.Replace("utc_offset = \"+09:00\"", "utc_offset = \"+9\"", StringComparison.Ordinal) }
            : file).ToList();
        using var folder = new DefinitionFolder(files);
        var output = new StringWriter { NewLine = "\n" };

        var code = ValidateCommand.Run(folder.Path, output);

        Assert.Equal(1, code);
        Assert.Equal(
            "worlds/testville.toml(7,1): Error InvalidValue: utc_offset は+09:00 の形の時差で書いてください: \"+9\"\n"
            + "エラー 1 件、警告 0 件\n",
            output.ToString());
    }
}
