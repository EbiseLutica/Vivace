using System.CommandLine;
using Vivace.Cli;

var rootCommand = new RootCommand("Vivace の CLI");

var validateDir = new Argument<string>("dir") { Description = "定義のフォルダ" };
var validate = new Command("validate", "定義を検証して、エラーと警告を出す");
validate.Arguments.Add(validateDir);
validate.SetAction(parseResult => ValidateCommand.Run(parseResult.GetValue(validateDir)!, Console.Out));
rootCommand.Subcommands.Add(validate);

return await rootCommand.Parse(args).InvokeAsync();
