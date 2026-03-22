using System.CommandLine;
using System.CommandLine.Invocation;
using NugetDocs.Cli.Services;

namespace NugetDocs.Cli.Commands;

internal sealed class ShowCommandAction(ShowCommand command) : AsynchronousCommandLineAction
{
    public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        var package = parseResult.GetValue(command.PackageArgument)!;
        var typeName = parseResult.GetValue(command.TypeArgument)!;
        var version = parseResult.GetValue(command.VersionOption);
        var framework = parseResult.GetValue(command.FrameworkOption);
        var showAll = parseResult.GetValue(command.AllOption);

        try
        {
            var resolved = await PackageResolver.ResolveAsync(
                package, version, framework, cancellationToken).ConfigureAwait(false);

            using var inspector = new TypeInspector(resolved.DllPath, resolved.XmlDocPath);

            Console.WriteLine($"// Package: {resolved.PackageId} {resolved.Version} ({resolved.Framework})");
            Console.WriteLine();

            var source = inspector.DecompileType(typeName, publicOnly: !showAll);
            Console.Write(source);

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
