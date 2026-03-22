using System.CommandLine;

namespace NugetDocs.Cli.Commands;

internal sealed class SearchCommand : Command
{
    public Argument<string> PackageArgument { get; } = CommonOptions.Package;
    public Argument<string> PatternArgument { get; } = new("pattern")
    {
        Description = "Search pattern (supports * and ? wildcards)",
    };
    public Option<string?> VersionOption { get; } = CommonOptions.Version;
    public Option<string?> FrameworkOption { get; } = CommonOptions.Framework;
    public Option<bool> AllOption { get; } = new("--all", "-a")
    {
        Description = "Search all members including private and internal (default: public only)",
        DefaultValueFactory = _ => false,
    };
    public Option<string?> OutputOption { get; } = CommonOptions.Output;

    public SearchCommand() : base("search", "Search types and members by pattern")
    {
        Arguments.Add(PackageArgument);
        Arguments.Add(PatternArgument);
        Options.Add(VersionOption);
        Options.Add(FrameworkOption);
        Options.Add(AllOption);
        Options.Add(OutputOption);

        Action = new SearchCommandAction(this);
    }
}
