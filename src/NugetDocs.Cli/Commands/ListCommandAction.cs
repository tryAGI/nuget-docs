using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using NugetDocs.Cli.Services;

namespace NugetDocs.Cli.Commands;

internal sealed class ListCommandAction(ListCommand command) : AsynchronousCommandLineAction
{
    public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        var package = parseResult.GetValue(command.PackageArgument)!;
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
            var xmlDocs = XmlDocReader.TryLoad(resolved.XmlDocPath);
            var allTypes = inspector.GetTypes(publicOnly: !showAll);

            var types = namespaceFilter is not null
                ? allTypes.Where(t => t.Namespace.StartsWith(namespaceFilter, StringComparison.OrdinalIgnoreCase)).ToList()
                : allTypes;

            if (string.Equals(output, "json", StringComparison.OrdinalIgnoreCase))
            {
                var json = new
                {
                    package = resolved.PackageId,
                    version = resolved.Version,
                    framework = resolved.Framework,
                    types = types.Select(t => new
                    {
                        kind = t.Kind,
                        name = t.GenericParameterCount > 0
                            ? $"{t.Name}<{new string(',', t.GenericParameterCount - 1)}>"
                            : t.Name,
                        fullName = t.FullName,
                        @namespace = t.Namespace,
                        summary = xmlDocs?.GetTypeSummary(t.FullName),
                    }),
                };
                Console.WriteLine(JsonSerializer.Serialize(json, JsonOptions.Indented));
            }
            else
            {
                Console.WriteLine($"Package: {resolved.PackageId} {resolved.Version} ({resolved.Framework})");
                Console.WriteLine();

                var grouped = types.GroupBy(t => t.Kind).OrderBy(g => GetKindOrder(g.Key));

                foreach (var group in grouped)
                {
                    var pluralKey = group.Key.EndsWith('s') ? $"{group.Key}es" : $"{group.Key}s";
                    Console.WriteLine($"{pluralKey}:");

                    foreach (var type in group)
                    {
                        var displayName = type.GenericParameterCount > 0
                            ? $"{type.Name}<{new string(',', type.GenericParameterCount - 1)}>"
                            : type.Name;

                        var summary = xmlDocs?.GetTypeSummary(type.FullName);
                        var line = summary is not null
                            ? $"  {displayName} — {summary}"
                            : $"  {displayName}";

                        Console.WriteLine(line);
                    }

                    Console.WriteLine();
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

    private static int GetKindOrder(string kind) => kind switch
    {
        "Interface" => 0,
        "Class" => 1,
        "Struct" => 2,
        "Enum" => 3,
        "Delegate" => 4,
        _ => 5,
    };
}
