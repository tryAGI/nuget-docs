using System.CommandLine;

namespace NugetDocs.Cli.Commands;

internal sealed class ListCommand : Command
{
    public Argument<string> PackageArgument { get; } = CommonOptions.Package;
    public Option<string?> VersionOption { get; } = CommonOptions.Version;
    public Option<string?> FrameworkOption { get; } = CommonOptions.Framework;
    public Option<bool> AllOption { get; } = new("--all", "-a")
    {
        Description = "Show all types including internal (default: public only)",
        DefaultValueFactory = _ => false,
    };
    public Option<string?> NamespaceOption { get; } = new("--namespace", "-n")
    {
        Description = "Filter types by namespace prefix",
        DefaultValueFactory = _ => null,
    };
    public Option<string?> OutputOption { get; } = CommonOptions.Output;
    public Option<bool> JsonOption { get; } = CommonOptions.Json;

    public ListCommand() : base("list", "List all public types in a NuGet package")
    {
        Arguments.Add(PackageArgument);
        Options.Add(VersionOption);
        Options.Add(FrameworkOption);
        Options.Add(AllOption);
        Options.Add(NamespaceOption);
        Options.Add(OutputOption);
        Options.Add(JsonOption);

        Action = new ListCommandAction(this);
    }
}
