using System.CommandLine;
using Vivace.Core;
using Vivace.Core.Definitions;
using Vivace.Core.Definitions.Model;
using Vivace.Core.Domain;
using Vivace.Core.Storage;
using Vivace.Core.Time;
using Vivace.Testing;

namespace Vivace.Cli;

/// <summary>定義を読み、いまの時刻（倍速なら倍率つき）から回して、流れる出力を書く。Ctrl+C で止めたら件数を書く。</summary>
public static class RunCommand
{
    /// <summary>vivace run のコマンドを作る。引数と既定値の検証は、ここで決める。</summary>
    public static Command Create()
    {
        var dir = new Argument<string>("dir") { Description = "定義のフォルダ" };
        var speed = new Option<double>("--speed") { Description = "時計の倍率（0 より大きい有限の数。1 で実時間）", DefaultValueFactory = _ => 1 };
        speed.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<double>();
            if (!double.IsFinite(value) || value <= 0)
            {
                result.AddError("--speed は 0 より大きい有限の数で書いてください。");
            }
        });
        var seed = new Option<ulong>("--seed") { Description = "ワールドのシード", DefaultValueFactory = _ => Scenario.WorldSeed };
        var all = new Option<bool>("--all") { Description = "適用されたすべてのイベントを出す（既定は行動と移動だけ）" };

        var command = new Command("run", "定義を読み、いまの時刻から回して流れる出力を書く（Ctrl+C で止める）");
        command.Arguments.Add(dir);
        command.Options.Add(speed);
        command.Options.Add(seed);
        command.Options.Add(all);
        command.SetAction((parseResult, ct) => RunAsync(
            parseResult.GetValue(dir)!,
            ClockFor(parseResult.GetValue(speed)),
            parseResult.GetValue(seed),
            parseResult.GetValue(all),
            Console.Out,
            ct));
        return command;
    }

    /// <summary>倍率から時計を作る。1 は実時間の時計、それ以外は開始を実時間のいまとする倍速の時計。</summary>
    /// <param name="speed">倍率。0 より大きい有限の値。</param>
    public static IClock ClockFor(double speed)
    {
        var real = new SystemClock();
        return speed == 1 ? real : new ScaledClock(real.UtcNow, speed);
    }

    /// <summary>
    /// 定義を読み、時計のいまの時刻でワールドとペルソナを生成して回す。until まで（既定は Ctrl+C まで）流れる出力を書き、止めたら件数を書く。
    /// 定義にエラーがあれば問題を書いて 1 を返す。購読があふれて止めたときも 1 を返す。
    /// </summary>
    /// <param name="dir">定義のフォルダ。</param>
    /// <param name="clock">時計。</param>
    /// <param name="seed">ワールドのシード（すべてのワールドに同じ値を使う）。</param>
    /// <param name="all">すべてのイベントを出すかどうか。</param>
    /// <param name="output">出力先。</param>
    /// <param name="ct">止めるためのキャンセル（Ctrl+C）。</param>
    /// <param name="until">回す先の時刻。省略するとキャンセルまで回す。</param>
    /// <param name="subscriberCapacity">流す購読の容量。省略すると既定の容量（テストで、あふれる経路を作るために指定する）。</param>
    internal static async Task<int> RunAsync(
        string dir,
        IClock clock,
        ulong seed,
        bool all,
        TextWriter output,
        CancellationToken ct,
        DateTimeOffset? until = null,
        int? subscriberCapacity = null)
    {
        if (!Directory.Exists(dir))
        {
            return ValidateCommand.Run(dir, output);
        }

        var load = DefinitionLoader.LoadDirectory(dir);
        if (load.Definitions is not { } definitions)
        {
            return ValidateCommand.Write(load, output);
        }

        WorldSetup.WriteHolidayNotices(definitions, output);

        var storage = new InMemoryStorage();
        var engine = VivaceEngine.Create()
            .UseDefinitions(definitions)
            .UseStorage(storage)
            .UseClock(clock)
            .Build();
        using var run = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var feed = subscriberCapacity is { } capacity
            ? engine.Subscribe(capacity, CancellationToken.None)
            : engine.Subscribe(CancellationToken.None);
        var printing = PrintAsync(feed, new DisplayNames(definitions), Offsets(definitions), all, output, run);

        var code = 0;
        try
        {
            if (await WorldSetup.InjectAsync(engine, definitions, seed, null, output, run.Token))
            {
                await engine.RunUntilAsync(until ?? DateTimeOffset.MaxValue, run.Token);
            }
            else
            {
                code = 1;
            }
        }
        catch (OperationCanceledException) when (run.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            output.WriteLine($"{ex.GetType().Name}: {ex.Message}");
            code = 1;
        }
        finally
        {
            await engine.DisposeAsync();
        }

        var overflowed = await printing;
        output.Write(ReportText.CountLine(storage.Events));
        return code == 0 && !overflowed ? 0 : 1;
    }

    /// <summary>
    /// 購読を読み、既定の書式で1行ずつ書く。購読があふれたら、理由を書いて run を止め、true を返す。
    /// </summary>
    /// <param name="feed">適用済みのイベントの購読。</param>
    /// <param name="names">表示名を引く表。</param>
    /// <param name="offsets">ワールド ID ごとの現地の時差。</param>
    /// <param name="all">すべてのイベントを出すかどうか。</param>
    /// <param name="output">出力先。</param>
    /// <param name="run">止めるためのキャンセル元。</param>
    private static async Task<bool> PrintAsync(
        IAsyncEnumerable<LoggedEvent> feed,
        DisplayNames names,
        IReadOnlyDictionary<WorldId, TimeSpan> offsets,
        bool all,
        TextWriter output,
        CancellationTokenSource run)
    {
        try
        {
            await foreach (var logged in feed)
            {
                if (EventLineFormatter.Format(logged, names, offsets, all) is { } line)
                {
                    output.WriteLine(line);
                }
            }

            return false;
        }
        catch (InvalidOperationException ex)
        {
            output.WriteLine(ex.Message);
            run.Cancel();
            return true;
        }
    }

    /// <summary>ワールド ID ごとの時差。ワールドの ID は定義の ID と同じ値（WorldCreatedRule）。</summary>
    /// <param name="definitions">定義。</param>
    private static Dictionary<WorldId, TimeSpan> Offsets(DefinitionPack definitions) =>
        definitions.Worlds.Values.ToDictionary(world => new WorldId(world.Id.Value), world => world.UtcOffset);
}
