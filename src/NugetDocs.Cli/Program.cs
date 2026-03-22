using System.CommandLine;
using NugetDocs.Cli.Commands;

var rootCommand = new RootCommand("Inspect public API documentation from any NuGet package")
{
    new ListCommand(),
    new ShowCommand(),
    new SearchCommand(),
    new InfoCommand(),
    new DiffCommand(),
};

return await rootCommand.Parse(args).InvokeAsync().ConfigureAwait(false);
