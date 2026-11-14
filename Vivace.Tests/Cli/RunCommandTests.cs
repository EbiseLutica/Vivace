using System.CommandLine;
using Vivace.Cli;
using Vivace.Core.Time;
using Vivace.Testing;
using Vivace.Tests.Support;

namespace Vivace.Tests.Cli;

/// <summary>vivace run。仮想時計で回し、流れる出力と件数、止め方、定義のエラー、倍率の検証を確かめる。実時間には依存しない。</summary>
public class RunCommandTests
{
    /// <summary>件数の行。末尾に改行。</summary>
    private const string CountPattern = @"適用 [0-9]+ 件、捨てた [0-9]+ 件、却下 [0-9]+ 件\n\z";

    /// <summary>倍率 1 は実時間の時計、それ以外は倍速の時計になる。</summary>
    [Fact]
    public void ClockForOneIsRealTimeAndOtherSpeedsScale()
    {
        Assert.IsType<SystemClock>(RunCommand.ClockFor(1));
        Assert.IsType<ScaledClock>(RunCommand.ClockFor(60));
    }

    /// <summary>0 より大きい有限の倍率は受け付ける。</summary>
    [Theory]
    [InlineData("1")]
    [InlineData("60")]
    [InlineData("0.5")]
    public void PositiveFiniteSpeedIsAccepted(string speed)
    {
        var parse = Parse("run", "dir", "--speed", speed);

        Assert.Empty(parse.Errors);
    }

    /// <summary>0、負の数、無限大の倍率は、日本語のエラーで拒否する。</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1e999")]
    public void NonPositiveOrInfiniteSpeedIsRejected(string speed)
    {
        var parse = Parse("run", "dir", "--speed", speed);

        Assert.Contains(parse.Errors, error => error.Message == "--speed は 0 より大きい有限の数で書いてください。");
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

        var code = await RunCommand.RunAsync(folder.Path, new VirtualClock(Scenario.Start), Scenario.WorldSeed, false, output, CancellationToken.None, Scenario.Start.AddDays(1));

        Assert.Equal(1, code);
        Assert.Equal(
            "worlds/testville.toml(7,1): Error InvalidValue: utc_offset は+09:00 の形の時差で書いてください: \"+9\"\n"
            + "エラー 1 件、警告 0 件\n",
            output.ToString());
    }

    /// <summary>定義のフォルダがなければ、validate と同じ文面で 1 を返す。</summary>
    [Fact]
    public async Task MissingFolderExitsOne()
    {
        var output = new StringWriter { NewLine = "\n" };

        var code = await RunCommand.RunAsync("no-such-definitions-folder", new VirtualClock(Scenario.Start), Scenario.WorldSeed, false, output, CancellationToken.None);

        Assert.Equal(1, code);
        Assert.Equal("定義のフォルダがありません: no-such-definitions-folder\n", output.ToString());
    }

    /// <summary>
    /// 既定の出力は、祝日の知らせ、行動と移動の行、件数の行。行動の行は dry-run の行動表と同じ時刻と内容（DryRunCommand のスナップショットと同じシード）。
    /// </summary>
    [Fact]
    public async Task DefaultModePrintsActionAndTravelLinesThenCounts()
    {
        using var folder = new DefinitionFolder(TestWorld.Files());
        var output = new StringWriter { NewLine = "\n" };

        var code = await RunCommand.RunAsync(folder.Path, new VirtualClock(Scenario.Start), Scenario.WorldSeed, false, output, CancellationToken.None, Scenario.Start.AddDays(1));

        var text = output.ToString();
        Assert.Equal(0, code);
        Assert.StartsWith("testville: 祝日のカレンダー jp は未対応のため、祝日なしで回した\n", text, StringComparison.Ordinal);
        Assert.Contains("11-01 23:00 kaori [自由] core.go_to_next @kaori_room\n", text, StringComparison.Ordinal);
        Assert.Contains("11-01 23:45 kaori [予定] core.sleep @kaori_room\n", text, StringComparison.Ordinal);
        Assert.Contains("11-02 07:50 kaori 移動 kaori_room → school_2a\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("day.started", text, StringComparison.Ordinal);
        Assert.Matches(CountPattern, text);
    }

    /// <summary>--all では、行動と移動の行に加えて、ほかのイベントもタイプの名前で出る。</summary>
    [Fact]
    public async Task AllModeAlsoPrintsOtherEventTypes()
    {
        using var folder = new DefinitionFolder(TestWorld.Files());
        var output = new StringWriter { NewLine = "\n" };

        var code = await RunCommand.RunAsync(folder.Path, new VirtualClock(Scenario.Start), Scenario.WorldSeed, true, output, CancellationToken.None, Scenario.Start.AddDays(1));

        var text = output.ToString();
        Assert.Equal(0, code);
        Assert.Contains("11-01 23:00 testville day.started\n", text, StringComparison.Ordinal);
        Assert.Contains("11-01 23:00 kaori [自由] core.go_to_next @kaori_room\n", text, StringComparison.Ordinal);
        Assert.Matches(CountPattern, text);
    }

    /// <summary>
    /// 止められたら（キャンセル）、件数の行を出して 0 を返す。止める時点は、待ちの3回目（時計の待ちで止める）。
    /// </summary>
    [Fact]
    public async Task CancelledRunPrintsCountsAndExitsZero()
    {
        using var folder = new DefinitionFolder(TestWorld.Files());
        var output = new StringWriter { NewLine = "\n" };
        using var stop = new CancellationTokenSource();
        var clock = new ActionClock(new VirtualClock(Scenario.Start), 3, stop.Cancel);

        var code = await RunCommand.RunAsync(folder.Path, clock, Scenario.WorldSeed, false, output, stop.Token);

        Assert.Equal(0, code);
        Assert.Matches(CountPattern, output.ToString());
    }

    /// <summary>時計が例外で止まったら、例外の型と文面を1行出し、件数の行を出して 1 を返す。</summary>
    [Fact]
    public async Task ClockErrorIsReportedAndExitsOne()
    {
        using var folder = new DefinitionFolder(TestWorld.Files());
        var output = new StringWriter { NewLine = "\n" };
        var clock = new ActionClock(new VirtualClock(Scenario.Start), 3, () => throw new InvalidOperationException("時計が止まった"));

        var code = await RunCommand.RunAsync(folder.Path, clock, Scenario.WorldSeed, false, output, CancellationToken.None);

        var text = output.ToString();
        Assert.Equal(1, code);
        Assert.Contains("InvalidOperationException: 時計が止まった\n", text, StringComparison.Ordinal);
        Assert.Matches(CountPattern, text);
    }

    /// <summary>止めるためでないキャンセル（時計が投げた OperationCanceledException）は、正常終了ではなく 1 を返す。</summary>
    [Fact]
    public async Task CancellationNotFromStopIsAnError()
    {
        using var folder = new DefinitionFolder(TestWorld.Files());
        var output = new StringWriter { NewLine = "\n" };
        var clock = new ActionClock(new VirtualClock(Scenario.Start), 3, () => throw new OperationCanceledException("外から取り消された"));

        var code = await RunCommand.RunAsync(folder.Path, clock, Scenario.WorldSeed, false, output, CancellationToken.None);

        var text = output.ToString();
        Assert.Equal(1, code);
        Assert.Contains("OperationCanceledException: 外から取り消された\n", text, StringComparison.Ordinal);
        Assert.Matches(CountPattern, text);
    }

    /// <summary>
    /// 時計の時刻が読むたびに進んでも、ワールドの生成は拒否されない。投入の時刻は、処理した時点の時計の時刻を使う。
    /// </summary>
    [Fact]
    public async Task ClockMovingWhileReadingDoesNotRejectWorldCreation()
    {
        using var folder = new DefinitionFolder(TestWorld.Files());
        var output = new StringWriter { NewLine = "\n" };
        var clock = new TickingClock(new VirtualClock(Scenario.Start));

        var code = await RunCommand.RunAsync(folder.Path, clock, Scenario.WorldSeed, false, output, CancellationToken.None, Scenario.Start.AddDays(1));

        Assert.Equal(0, code);
        Assert.DoesNotContain("投入が拒否", output.ToString(), StringComparison.Ordinal);
        Assert.Matches(CountPattern, output.ToString());
    }

    /// <summary>コマンドの構文を、ルートのコマンドで解く。</summary>
    private static ParseResult Parse(params string[] args)
    {
        var root = new RootCommand();
        root.Subcommands.Add(RunCommand.Create());
        return root.Parse(args);
    }

    /// <summary>時刻を読むたびに1ミリ秒進める時計（実時間の時計が読みのあいだに進むことの代わり）。</summary>
    private sealed class TickingClock(VirtualClock inner) : IClock
    {
        /// <summary>いまの時刻。読んだあとに1ミリ秒進める。</summary>
        public DateTimeOffset UtcNow
        {
            get
            {
                var now = inner.UtcNow;
                inner.AdvanceTo(now.AddMilliseconds(1));
                return now;
            }
        }

        /// <summary>仮想時計で待つ。</summary>
        /// <param name="t">待つ先の時刻。</param>
        /// <param name="ct">キャンセル。</param>
        public ValueTask WaitUntilAsync(DateTimeOffset t, CancellationToken ct) => inner.WaitUntilAsync(t, ct);
    }

    /// <summary>待ちの回数を数え、決まった回数の待ちで処理を実行する（Ctrl+C や時計の故障の代わり）。時刻は仮想時計に任せる。</summary>
    private sealed class ActionClock(VirtualClock inner, int waitsBeforeAction, Action action) : IClock
    {
        /// <summary>いままでの待ちの回数。</summary>
        private int _waits;

        /// <summary>いまの仮想の時刻。</summary>
        public DateTimeOffset UtcNow => inner.UtcNow;

        /// <summary>待ちを数え、決まった回数に達したら処理を実行してから、仮想時計で待つ。</summary>
        /// <param name="t">待つ先の時刻。</param>
        /// <param name="ct">キャンセル。</param>
        public ValueTask WaitUntilAsync(DateTimeOffset t, CancellationToken ct)
        {
            _waits++;
            if (_waits == waitsBeforeAction)
            {
                action();
            }

            return inner.WaitUntilAsync(t, ct);
        }
    }
}
