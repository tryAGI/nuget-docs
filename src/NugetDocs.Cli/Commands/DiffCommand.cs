using System.CommandLine;

namespace NugetDocs.Cli.Commands;

internal sealed class DiffCommand : Command
{
    public Argument<string> PackageArgument { get; } = CommonOptions.Package;
    public Option<string> FromOption { get; } = new("--from")
    {
        Description = "Version to compare from (older version)",
        Required = true,
    };
    public Option<string> ToOption { get; } = new("--to")
    {
        Description = "Version to compare to (newer version)",
        Required = true,
    };
    public Option<string?> FrameworkOption { get; } = CommonOptions.Framework;
    public Option<bool> TypeOnlyOption { get; } = new("--type-only", "-t")
    {
        Description = "Show only added/removed/changed type names without detailed source diffs",
        DefaultValueFactory = _ => false,
    };
    public Option<bool> BreakingOption { get; } = new("--breaking", "-b")
    {
        Description = "Highlight potentially breaking changes (removed types, removed/changed members)",
        DefaultValueFactory = _ => false,
    };
    public Option<string?> OutputOption { get; } = CommonOptions.Output;

    public DiffCommand() : base("diff", "Compare public API surface between two versions of a package")
    {
        Arguments.Add(PackageArgument);
        Options.Add(FromOption);
        Options.Add(ToOption);
        Options.Add(FrameworkOption);
        Options.Add(TypeOnlyOption);
        Options.Add(BreakingOption);
        Options.Add(OutputOption);

        Action = new DiffCommandAction(this);
    }
}
