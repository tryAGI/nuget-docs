using System.CommandLine;

namespace NugetDocs.Cli.Commands;

internal sealed class ListCommand : Command
{
    public Argument<string> PackageArgument { get; } = CommonOptions.Package;
    public Option<string?> VersionOption { get; } = CommonOptions.Version;
    public Option<string?> FrameworkOption { get; } = CommonOptions.Framework;
    public Option<string?> OutputOption { get; } = CommonOptions.Output;

    public ListCommand() : base("list", "List all public types in a NuGet package")
    {
        Arguments.Add(PackageArgument);
        Options.Add(VersionOption);
        Options.Add(FrameworkOption);
        Options.Add(OutputOption);

        Action = new ListCommandAction(this);
    }
}
