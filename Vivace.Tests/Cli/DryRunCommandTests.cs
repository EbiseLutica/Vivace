using Vivace.Cli;
using Vivace.Testing;
using Vivace.Tests.Support;

namespace Vivace.Tests.Cli;

/// <summary>vivace dry-run。テスト用の定義を仮想時計で回し、行動表を出す。</summary>
public class DryRunCommandTests
{
    /// <summary>テスト用の定義を、既定の開始とシードで2日回した行動表が、保存された期待値と一致する。</summary>
    [Fact]
    public async Task TwoDaysMatchSnapshot()
    {
        using var folder = new DefinitionFolder(TestWorld.Files());
        var output = new StringWriter { NewLine = "\n" };

        var code = await DryRunCommand.RunAsync(folder.Path, 2, Scenario.WorldSeed, Scenario.Start, output, CancellationToken.None);

        Assert.Equal(0, code);
        await Snapshot.Text(output.ToString());
    }

    /// <summary>定義にエラーがあれば、validate と同じ問題の出力で、回さずに 1 を返す。</summary>
    [Fact]
    public async Task BrokenDefinitionsStopWithValidateOutput()
    {
        var files = TestWorld.Files().Select(file => file.Path == "worlds/testville.toml"
            ? file with { Text = file.Text.Replace("utc_offset = \"+09:00\"", "utc_offset = \"+9\"", StringComparison.Ordinal) }
            : file).ToList();
        using var folder = new DefinitionFolder(files);
        var output = new StringWriter { NewLine = "\n" };

        var code = await DryRunCommand.RunAsync(folder.Path, 2, Scenario.WorldSeed, Scenario.Start, output, CancellationToken.None);

        Assert.Equal(1, code);
        Assert.Equal(
            "worlds/testville.toml(7,1): Error InvalidValue: utc_offset は+09:00 の形の時差で書いてください: \"+9\"\n"
            + "エラー 1 件、警告 0 件\n",
            output.ToString());
    }
}
