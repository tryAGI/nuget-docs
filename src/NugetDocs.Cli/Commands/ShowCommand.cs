using System.CommandLine;

namespace NugetDocs.Cli.Commands;

internal sealed class ShowCommand : Command
{
    public Argument<string> PackageArgument { get; } = CommonOptions.Package;
    public Argument<string> TypeArgument { get; } = new("type")
    {
        Description = "Type name (short name like IChatClient or full name)",
        Arity = ArgumentArity.ZeroOrOne,
    };
    public Option<string?> VersionOption { get; } = CommonOptions.Version;
    public Option<string?> FrameworkOption { get; } = CommonOptions.Framework;
    public Option<bool> AllOption { get; } = new("--all", "-a")
    {
        Description = "Show all members including private and internal (default: public only)",
        DefaultValueFactory = _ => false,
    };
    public Option<string?> MemberOption { get; } = new("--member", "-m")
    {
        Description = "Show only a specific member (method, property, etc.) by name",
        DefaultValueFactory = _ => null,
    };
    public Option<bool> AssemblyOption { get; } = new("--assembly")
    {
        Description = "Show assembly-level attributes instead of a type",
        DefaultValueFactory = _ => false,
    };
    public Option<string?> NamespaceOption { get; } = new("--namespace", "-n")
    {
        Description = "Filter by namespace prefix (applies to --assembly attribute types)",
        DefaultValueFactory = _ => null,
    };
    public Option<string?> OutputOption { get; } = CommonOptions.Output;

    public ShowCommand() : base("show", "Show decompiled source for a specific type with XML documentation")
    {
        Arguments.Add(PackageArgument);
        Arguments.Add(TypeArgument);
        Options.Add(VersionOption);
        Options.Add(FrameworkOption);
        Options.Add(AllOption);
        Options.Add(MemberOption);
        Options.Add(AssemblyOption);
        Options.Add(NamespaceOption);
        Options.Add(OutputOption);

        Action = new ShowCommandAction(this);
    }
}
