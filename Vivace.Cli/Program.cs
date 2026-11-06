using System.CommandLine;

var rootCommand = new RootCommand("Vivace の CLI");

return await rootCommand.Parse(args).InvokeAsync();
