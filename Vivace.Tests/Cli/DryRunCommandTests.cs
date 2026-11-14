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

    /// <summary>祝日のカレンダーを用意できないワールドは、行動表の前に1行で知らせる。</summary>
    [Fact]
    public async Task HolidayCalendarWithoutProviderIsNoticedBeforeReport()
    {
        using var folder = new DefinitionFolder(TestWorld.Files());
        var output = new StringWriter { NewLine = "\n" };

        var code = await DryRunCommand.RunAsync(folder.Path, 1, Scenario.WorldSeed, Scenario.Start, output, CancellationToken.None);

        Assert.Equal(0, code);
        Assert.StartsWith("testville: 祝日のカレンダー jp は未対応のため、祝日なしで回した\n2026-11-01（日）\n", output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>祝日のカレンダーを指定していないワールドには、知らせる行を出さない。</summary>
    [Fact]
    public async Task WorldWithoutHolidayCalendarIsNotNoticed()
    {
        var files = TestWorld.Files().Select(file => file.Path == "worlds/testville.toml"
            ? file with { Text = file.Text.Replace("holidays = \"jp\"\n", string.Empty, StringComparison.Ordinal) }
            : file).ToList();
        using var folder = new DefinitionFolder(files);
        var output = new StringWriter { NewLine = "\n" };

        var code = await DryRunCommand.RunAsync(folder.Path, 1, Scenario.WorldSeed, Scenario.Start, output, CancellationToken.None);

        Assert.Equal(0, code);
        Assert.StartsWith("2026-11-01（日）\n", output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>定義のフォルダがなければ、validate と同じ文面で 1 を返す。</summary>
    [Fact]
    public async Task MissingFolderExitsOne()
    {
        var output = new StringWriter { NewLine = "\n" };

        var code = await DryRunCommand.RunAsync("no-such-definitions-folder", 1, Scenario.WorldSeed, Scenario.Start, output, CancellationToken.None);

        Assert.Equal(1, code);
        Assert.Equal("定義のフォルダがありません: no-such-definitions-folder\n", output.ToString());
    }

    /// <summary>終わりの時刻が計算できない日数は、日本語のエラーで 1 を返し、回さない。</summary>
    [Fact]
    public async Task DaysBeyondDateRangeExitsOne()
    {
        using var folder = new DefinitionFolder(TestWorld.Files());
        var output = new StringWriter { NewLine = "\n" };

        var code = await DryRunCommand.RunAsync(folder.Path, int.MaxValue, Scenario.WorldSeed, Scenario.Start, output, CancellationToken.None);

        Assert.Equal(1, code);
        Assert.Equal("--days が大きすぎて、終わりの時刻を計算できません。\n", output.ToString());
    }
}
