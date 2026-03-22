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
    public Option<string?> OutputOption { get; } = CommonOptions.Output;

    public DiffCommand() : base("diff", "Compare public API surface between two versions of a package")
    {
        Arguments.Add(PackageArgument);
        Options.Add(FromOption);
        Options.Add(ToOption);
        Options.Add(FrameworkOption);
        Options.Add(OutputOption);

        Action = new DiffCommandAction(this);
    }
}
