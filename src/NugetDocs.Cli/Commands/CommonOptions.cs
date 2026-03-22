using System.CommandLine;

namespace NugetDocs.Cli.Commands;

internal static class CommonOptions
{
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
}
