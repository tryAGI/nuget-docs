using System.CommandLine;
using System.CommandLine.Invocation;

namespace NugetDocs.Cli.Commands;

internal static class CommonOptions
{
    /// <summary>
    /// Returns true if output should be JSON (either --output json or --json was specified).
    /// </summary>
    public static bool IsJsonOutput(ParseResult parseResult, Option<string?> outputOption, Option<bool> jsonOption)
    {
        return parseResult.GetValue(jsonOption)
            || string.Equals(parseResult.GetValue(outputOption), "json", StringComparison.OrdinalIgnoreCase);
    }

    public static Argument<string> Package => new(
        name: "package")
    {
        Description = "NuGet package name",
    };

    public static Option<string?> Version => new(
        name: "--version",
        aliases: ["-v"])
    {
        Description = "Package version (default: latest stable)",
        DefaultValueFactory = _ => null,
    };

    public static Option<string?> Framework => new(
        name: "--framework",
        aliases: ["-f"])
    {
        Description = "Target framework moniker (default: auto-select best)",
        DefaultValueFactory = _ => null,
    };

    public static Option<string?> Output => new(
        name: "--output",
        aliases: ["-o"])
    {
        Description = "Output format: text (default) or json",
        DefaultValueFactory = _ => null,
    };

    public static Option<bool> Json => new(
        name: "--json",
        aliases: ["-j"])
    {
        Description = "Shorthand for --output json",
        DefaultValueFactory = _ => false,
    };
}
