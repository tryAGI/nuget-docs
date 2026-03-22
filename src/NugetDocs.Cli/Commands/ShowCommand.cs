using System.CommandLine;

namespace NugetDocs.Cli.Commands;

internal sealed class ShowCommand : Command
{
    public Argument<string> PackageArgument { get; } = CommonOptions.Package;
    public Argument<string> TypeArgument { get; } = new("type")
    {
        Description = "Type name (short name like IChatClient or full name)",
    };
    public Option<string?> VersionOption { get; } = CommonOptions.Version;
    public Option<string?> FrameworkOption { get; } = CommonOptions.Framework;
    public Option<bool> AllOption { get; } = new("--all", "-a")
    {
        Description = "Show all members including private and internal (default: public only)",
        DefaultValueFactory = _ => false,
    };
    public Option<string?> OutputOption { get; } = CommonOptions.Output;

    public ShowCommand() : base("show", "Show decompiled source for a specific type with XML documentation")
    {
        Arguments.Add(PackageArgument);
        Arguments.Add(TypeArgument);
        Options.Add(VersionOption);
        Options.Add(FrameworkOption);
        Options.Add(AllOption);
        Options.Add(OutputOption);

        Action = new ShowCommandAction(this);
    }
}
