using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using NugetDocs.Cli.Services;

namespace NugetDocs.Cli.Commands;

internal sealed class SearchCommandAction(SearchCommand command) : AsynchronousCommandLineAction
{
    public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        var package = parseResult.GetValue(command.PackageArgument)!;
        var pattern = parseResult.GetValue(command.PatternArgument)!;
        var version = parseResult.GetValue(command.VersionOption);
        var framework = parseResult.GetValue(command.FrameworkOption);
        var showAll = parseResult.GetValue(command.AllOption);
        var namespaceFilter = parseResult.GetValue(command.NamespaceOption);
        var output = parseResult.GetValue(command.OutputOption);

        try
        {
            var resolved = await PackageResolver.ResolveAsync(
                package, version, framework, cancellationToken).ConfigureAwait(false);

            using var inspector = new TypeInspector(resolved.DllPath, resolved.XmlDocPath);
            var allResults = inspector.SearchTypes(pattern, publicOnly: !showAll);

            var results = namespaceFilter is not null
                ? allResults.Where(r => r.FullName.StartsWith(namespaceFilter, StringComparison.OrdinalIgnoreCase)).ToList()
                : allResults;

            if (string.Equals(output, "json", StringComparison.OrdinalIgnoreCase))
            {
                var json = new
                {
                    package = resolved.PackageId,
                    version = resolved.Version,
                    framework = resolved.Framework,
                    pattern,
                    count = results.Count,
                    results = results.Select(r => new
                    {
                        kind = r.Kind,
                        memberKind = r.MemberKind,
                        name = r.Name,
                        fullName = r.FullName,
                    }),
                };
                Console.WriteLine(JsonSerializer.Serialize(json, JsonOptions.Indented));
            }
            else
            {
                Console.WriteLine($"Package: {resolved.PackageId} {resolved.Version} ({resolved.Framework})");
                Console.WriteLine($"Pattern: {pattern}");
                Console.WriteLine($"Results: {results.Count}");
                Console.WriteLine();

                foreach (var result in results)
                {
                    var kindLabel = result.MemberKind is not null
                        ? $"{result.Kind}.{result.MemberKind}"
                        : result.Kind;

                    Console.WriteLine($"  [{kindLabel}] {result.FullName}");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
