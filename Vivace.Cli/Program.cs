using System.CommandLine;
using System.Text;
using Vivace.Cli;
using Vivace.Testing;

Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

var rootCommand = new RootCommand("Vivace の CLI");

var validateDir = new Argument<string>("dir") { Description = "定義のフォルダ" };
var validate = new Command("validate", "定義を検証して、エラーと警告を出す");
validate.Arguments.Add(validateDir);
validate.SetAction(parseResult => ValidateCommand.Run(parseResult.GetValue(validateDir)!, Console.Out));
rootCommand.Subcommands.Add(validate);

var dryRunDir = new Argument<string>("dir") { Description = "定義のフォルダ" };
var days = new Option<int>("--days") { Description = "回す日数（1 以上）" };
days.Validators.Add(result =>
{
    if (result.GetValueOrDefault<int>() < 1)
    {
        result.AddError("--days は 1 以上で書いてください。");
    }
});
var seed = new Option<ulong>("--seed") { Description = "ワールドのシード", DefaultValueFactory = _ => Scenario.WorldSeed };
var start = new Option<string>("--start")
{
    Description = "開始の時刻（オフセット付きの ISO 8601）",
    DefaultValueFactory = _ => StartTimeParser.Format(Scenario.Start),
};
start.Validators.Add(result =>
{
    if (!StartTimeParser.TryParse(result.GetValueOrDefault<string>(), out _))
    {
        result.AddError("--start はオフセット付きの ISO 8601 で書いてください（例: 2026-11-01T23:00+09:00）。");
    }
});
var dryRun = new Command("dry-run", "仮想時計で回して、行動表を出す");
dryRun.Arguments.Add(dryRunDir);
dryRun.Options.Add(days);
dryRun.Options.Add(seed);
dryRun.Options.Add(start);
dryRun.Validators.Add(result =>
{
    if (result.GetResult(days) is null)
    {
        result.AddError("--days を指定してください。");
    }
});
dryRun.SetAction((parseResult, ct) =>
{
    StartTimeParser.TryParse(parseResult.GetValue(start), out var at);
    return DryRunCommand.RunAsync(
        parseResult.GetValue(dryRunDir)!,
        parseResult.GetValue(days),
        parseResult.GetValue(seed),
        at,
        Console.Out,
        ct);
});
rootCommand.Subcommands.Add(dryRun);

return await rootCommand.Parse(args).InvokeAsync();
