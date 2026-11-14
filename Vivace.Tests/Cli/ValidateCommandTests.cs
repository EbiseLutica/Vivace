using Vivace.Cli;
using Vivace.Testing;
using Vivace.Tests.Support;

namespace Vivace.Tests.Cli;

/// <summary>vivace validate。テスト用の定義は問題なし、壊した定義は位置つきのエラーになり、警告だけなら読めて終了コード 0 になる。</summary>
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

    /// <summary>警告だけの定義は、警告の件数を出し、終了コード 0 になる。</summary>
    [Fact]
    public void WarningsOnlyReportCountAndExitZero()
    {
        var files = TestWorld.Files().Select(file => file.Path == "worlds/testville.toml"
            ? file with { Text = file.Text.Replace("generated_places = \"auto\"\n", "generated_places = \"auto\"\ncolour = \"red\"\n", StringComparison.Ordinal) }
            : file).ToList();
        using var folder = new DefinitionFolder(files);
        var output = new StringWriter { NewLine = "\n" };

        var code = ValidateCommand.Run(folder.Path, output);

        Assert.Equal(0, code);
        Assert.Equal(
            "worlds/testville.toml(9,1): Warning UnknownKey: 未知のキーです（無視します）: colour\n"
            + "エラー 0 件、警告 1 件\n",
            output.ToString());
    }

    /// <summary>定義のフォルダがなければ、文面を出して終了コード 1 になる。</summary>
    [Fact]
    public void MissingFolderExitsOne()
    {
        var output = new StringWriter { NewLine = "\n" };

        var code = ValidateCommand.Run("no-such-definitions-folder", output);

        Assert.Equal(1, code);
        Assert.Equal("定義のフォルダがありません: no-such-definitions-folder\n", output.ToString());
    }
}
