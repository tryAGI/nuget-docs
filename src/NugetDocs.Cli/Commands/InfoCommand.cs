using System.CommandLine;

namespace NugetDocs.Cli.Commands;

internal sealed class InfoCommand : Command
{
    public Argument<string> PackageArgument { get; } = CommonOptions.Package;
    public Option<string?> VersionOption { get; } = CommonOptions.Version;
    public Option<string?> OutputOption { get; } = CommonOptions.Output;

    public InfoCommand() : base("info", "Show package metadata from .nuspec")
    {
        Arguments.Add(PackageArgument);
        Options.Add(VersionOption);
        Options.Add(OutputOption);

        Action = new InfoCommandAction(this);
    }
}
