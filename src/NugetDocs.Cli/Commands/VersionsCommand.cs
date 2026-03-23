using System.CommandLine;

namespace NugetDocs.Cli.Commands;

internal sealed class VersionsCommand : Command
{
    public Argument<string> PackageArgument { get; } = CommonOptions.Package;
    public Option<bool> StableOption { get; } = new("--stable", "-s")
    {
        Description = "Show only stable versions (exclude prereleases)",
        DefaultValueFactory = _ => false,
    };
    public Option<bool> PrereleaseOption { get; } = new("--prerelease", "-p")
    {
        Description = "Show only prerelease versions",
        DefaultValueFactory = _ => false,
    };
    public Option<bool> LatestOption { get; } = new("--latest")
    {
        Description = "Show only the latest stable and latest prerelease versions",
        DefaultValueFactory = _ => false,
    };
    public Option<int> LimitOption { get; } = new("--limit", "-l")
    {
        Description = "Maximum number of versions to show (default: 20, 0 = all)",
        DefaultValueFactory = _ => 20,
    };
    public Option<string?> SinceOption { get; } = new("--since")
    {
        Description = "Show only versions newer than the specified version",
        DefaultValueFactory = _ => null,
    };
    public Option<bool> CountOption { get; } = new("--count", "-c")
    {
        Description = "Output only the count of matching versions",
        DefaultValueFactory = _ => false,
    };
    public Option<string?> OutputOption { get; } = CommonOptions.Output;
    public Option<bool> JsonOption { get; } = CommonOptions.Json;

    public VersionsCommand() : base("versions", "List all available versions of a package from NuGet.org")
    {
        Arguments.Add(PackageArgument);
        Options.Add(StableOption);
        Options.Add(PrereleaseOption);
        Options.Add(LatestOption);
        Options.Add(SinceOption);
        Options.Add(LimitOption);
        Options.Add(CountOption);
        Options.Add(OutputOption);
        Options.Add(JsonOption);

        Action = new VersionsCommandAction(this);
    }
}
