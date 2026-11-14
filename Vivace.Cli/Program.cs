using System.CommandLine;
using System.Globalization;
using Vivace.Cli;
using Vivace.Testing;

var rootCommand = new RootCommand("Vivace の CLI");

var validateDir = new Argument<string>("dir") { Description = "定義のフォルダ" };
var validate = new Command("validate", "定義を検証して、エラーと警告を出す");
validate.Arguments.Add(validateDir);
validate.SetAction(parseResult => ValidateCommand.Run(parseResult.GetValue(validateDir)!, Console.Out));
rootCommand.Subcommands.Add(validate);

var dryRunDir = new Argument<string>("dir") { Description = "定義のフォルダ" };
var days = new Option<int>("--days") { Description = "回す日数（1 以上）", Required = true };
days.Validators.Add(result =>
{
    if (result.GetValueOrDefault<int>() < 1)
    {
        result.AddError("--days は 1 以上で書いてください。");
    }
});
var seed = new Option<ulong>("--seed") { Description = "ワールドのシード", DefaultValueFactory = _ => Scenario.WorldSeed };
var start = new Option<DateTimeOffset>("--start")
{
    Description = "開始の時刻（ISO 8601）",
    DefaultValueFactory = _ => Scenario.Start,
    CustomParser = result =>
    {
        if (DateTimeOffset.TryParse(result.Tokens[0].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        result.AddError("--start は ISO 8601 の時刻で書いてください（例: 2026-11-01T23:00+09:00）。");
        return default;
    },
};
var dryRun = new Command("dry-run", "仮想時計で回して、行動表を出す");
dryRun.Arguments.Add(dryRunDir);
dryRun.Options.Add(days);
dryRun.Options.Add(seed);
dryRun.Options.Add(start);
dryRun.SetAction((parseResult, ct) => DryRunCommand.RunAsync(
    parseResult.GetValue(dryRunDir)!,
    parseResult.GetValue(days),
    parseResult.GetValue(seed),
    parseResult.GetValue(start),
    Console.Out,
    ct));
rootCommand.Subcommands.Add(dryRun);

return await rootCommand.Parse(args).InvokeAsync();
