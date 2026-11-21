using System.CommandLine;
using Vivace.Core;
using Vivace.Core.Definitions;
using Vivace.Core.Definitions.Model;
using Vivace.Core.Llm;
using Vivace.Core.Domain;
using Vivace.Core.Storage;
using Vivace.Core.Time;
using Vivace.Storage.Sqlite;
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
        var seed = new Option<ulong?>("--seed") { Description = "ワールドのシード（記録があるときは記録のシードを使う。省略すると既定の値）" };
        var all = new Option<bool>("--all") { Description = "適用されたすべてのイベントを出す（既定は行動と移動だけ）" };
        var db = new Option<string?>("--db") { Description = "記録の SQLite ファイル。あれば続きから動かし、なければ始めて記録する。省略するとメモリで動く（終わると消える）" };

        var command = new Command("run", "定義を読み、いまの時刻から回して流れる出力を書く（Ctrl+C で止める）");
        command.Arguments.Add(dir);
        command.Options.Add(speed);
        command.Options.Add(seed);
        command.Options.Add(all);
        command.Options.Add(db);
        command.SetAction(async (parseResult, ct) =>
        {
            var directory = parseResult.GetValue(dir)!;
            var rate = parseResult.GetValue(speed);
            var worldSeed = parseResult.GetValue(seed);
            var showAll = parseResult.GetValue(all);
            var path = parseResult.GetValue(db);
            if (path is null)
            {
                return await RunAsync(directory, ClockFor(rate), worldSeed, showAll, Console.Out, ct,
                    llmFactory: output => LlmSetup.Create(Environment.GetEnvironmentVariable, output),
                    storage: new InMemoryStorage());
            }

            await using var database = await SqliteDatabase.OpenAsync(path, ct: ct);
            var storage = new SqliteStorage(database);
            var recorded = (await storage.LoadAsync(ct)).State?.Cursor.Time;
            return await RunAsync(directory, ClockFor(rate, recorded), worldSeed, showAll, Console.Out, ct,
                llmFactory: output => LlmSetup.Create(Environment.GetEnvironmentVariable, output),
                storage: storage);
        });
        return command;
    }

    /// <summary>
    /// 倍率から時計を作る。1 は実時間の時計、それ以外は倍速の時計。開始は、実時間のいまと、記録の最後に適用した時刻の遅いほう（記録が先に進んでいたら、時刻が戻らないように）。
    /// </summary>
    /// <param name="speed">倍率。0 より大きい有限の値。</param>
    /// <param name="recorded">記録の最後に適用した時刻。記録がなければ null。</param>
    public static IClock ClockFor(double speed, DateTimeOffset? recorded = null)
    {
        var now = new SystemClock().UtcNow;
        var ahead = recorded is { } last && last > now;
        if (speed == 1 && !ahead)
        {
            return new SystemClock();
        }

        return new ScaledClock(ahead ? recorded!.Value : now, speed);
    }

    /// <summary>
    /// 定義を読み、時計のいまの時刻でワールドとペルソナを生成して回す。until まで（既定は Ctrl+C まで）流れる出力を書き、止めたら件数を書く。
    /// 定義にエラーがあれば問題を書いて 1 を返す。購読があふれて止めたときも 1 を返す。
    /// </summary>
    /// <param name="dir">定義のフォルダ。</param>
    /// <param name="clock">時計。</param>
    /// <param name="seed">ワールドのシード。省略すると既定の値。記録があるときは、記録のシードを使う（違えば1行知らせる）。</param>
    /// <param name="all">すべてのイベントを出すかどうか。</param>
    /// <param name="output">出力先。</param>
    /// <param name="ct">止めるためのキャンセル（Ctrl+C）。</param>
    /// <param name="until">回す先の時刻。省略するとキャンセルまで回す。</param>
    /// <param name="subscriberCapacity">流す購読の容量。省略すると既定の容量（テストで、あふれる経路を作るために指定する）。</param>
    /// <param name="channelOutput">投稿の行を書く先。省略すると出力と同じ。</param>
    /// <param name="llm">文面を作る LLM。省略すると llmFactory を使う。どちらもなければ、投稿は文面を作れずにスキップされる。</param>
    /// <param name="llmFactory">定義の検証が通ったあとに LLM を作る関数。通知は出力に書く。</param>
    /// <param name="errors">LLM の失敗を書く先。省略するとエラーの出力（Console.Error）。</param>
    /// <param name="storage">保存先。省略するとメモリ（InMemoryStorage）。エンジンを組んだあとは、エンジンが閉じる。組めなかったときは呼び出し側が閉じる。</param>
    internal static async Task<int> RunAsync(
        string dir,
        IClock clock,
        ulong? seed,
        bool all,
        TextWriter output,
        CancellationToken ct,
        DateTimeOffset? until = null,
        int? subscriberCapacity = null,
        TextWriter? channelOutput = null,
        ILlmClient? llm = null,
        Func<TextWriter, ILlmClient?>? llmFactory = null,
        TextWriter? errors = null,
        IStorage? storage = null)
    {
        output = TextWriter.Synchronized(output);
        channelOutput = TextWriter.Synchronized(channelOutput ?? output);
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
        var client = llm ?? llmFactory?.Invoke(output);

        var names = new DisplayNames(definitions);
        var offsets = Offsets(definitions);
        var builder = VivaceEngine.Create()
            .UseDefinitions(definitions)
            .UseStorage(storage ?? new InMemoryStorage())
            .UseClock(clock)
            .AddChannel(new ConsoleChannel(names, offsets, channelOutput ?? output));
        if (client is not null)
        {
            builder.UseLlm(new LoggingLlmClient(client, TextWriter.Synchronized(errors ?? Console.Error)));
        }

        var engine = builder.Build();
        using var run = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var feed = subscriberCapacity is { } capacity
            ? engine.Subscribe(capacity, CancellationToken.None)
            : engine.Subscribe(CancellationToken.None);
        var printing = PrintAsync(feed, names, offsets, all, output, run);

        var code = 0;
        try
        {
            await engine.StartAsync(run.Token);
            var resumed = await engine.RecoverAsync(run.Token);
            if (resumed)
            {
                WriteRecordedSeed(engine, seed, output);
            }

            var started = resumed || await WorldSetup.InjectAsync(engine, definitions, seed ?? Scenario.WorldSeed, null, output, run.Token);
            if (started)
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
        output.Write(ReportText.CountLine(engine.Log));
        return code == 0 && !overflowed ? 0 : 1;
    }

    /// <summary>
    /// 記録から続けるとき、指定のシードが記録と違えば、記録のシードで続けることを1行書く。記録のシードは、ワールドの状態から読む。
    /// </summary>
    /// <param name="engine">記録から起動したエンジン。</param>
    /// <param name="seed">指定のシード。省略なら何も書かない。</param>
    /// <param name="output">出力先。</param>
    private static void WriteRecordedSeed(VivaceEngine engine, ulong? seed, TextWriter output)
    {
        if (seed is not { } given || engine.View.Worlds.Values.FirstOrDefault() is not { } world || world.Seed == given)
        {
            return;
        }

        output.WriteLine($"記録のシード {world.Seed} で続ける（--seed {given} は無視した）");
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
