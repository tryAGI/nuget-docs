using System.CommandLine;

namespace NugetDocs.Cli.Commands;

internal sealed class DepsCommand : Command
{
    public Argument<string> PackageArgument { get; } = CommonOptions.Package;
    public Option<string?> VersionOption { get; } = CommonOptions.Version;
    public Option<string?> FrameworkOption { get; } = CommonOptions.Framework;
    public Option<int> DepthOption { get; } = new("--depth", "-d")
    {
        Description = "Maximum depth for transitive dependency resolution (default: 1 = direct only)",
        DefaultValueFactory = _ => 1,
    };
    public Option<string?> OutputOption { get; } = CommonOptions.Output;
    public Option<bool> JsonOption { get; } = CommonOptions.Json;

    public DepsCommand() : base("deps", "Show dependency tree of a package")
    {
        Arguments.Add(PackageArgument);
        Options.Add(VersionOption);
        Options.Add(FrameworkOption);
        Options.Add(DepthOption);
        Options.Add(OutputOption);
        Options.Add(JsonOption);

        Action = new DepsCommandAction(this);
    }
}
