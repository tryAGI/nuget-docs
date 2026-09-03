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
        var limit = parseResult.GetValue(command.LimitOption);
        var format = parseResult.GetValue(command.FormatOption);
        var jsonOutput = CommonOptions.IsJsonOutput(parseResult, command.OutputOption, command.JsonOption);

        try
        {
            var resolved = await PackageResolver.ResolveAsync(
                package, version, framework, cancellationToken).ConfigureAwait(false);

            using var inspector = new TypeInspector(resolved.DllPath!, resolved.XmlDocPath);
            var xmlDocs = XmlDocReader.TryLoad(resolved.XmlDocPath);
            var allTypes = inspector.GetTypes(publicOnly: !showAll);

            var filtered = namespaceFilter is not null
                ? allTypes.Where(t => t.Namespace.StartsWith(namespaceFilter, StringComparison.OrdinalIgnoreCase)).ToList()
                : allTypes;

            var total = filtered.Count;
            var types = CommonOptions.ApplyLimit(filtered, limit);
            const string NarrowHint = "--namespace to narrow";

            if (jsonOutput)
            {
                var json = new
                {
                    package = resolved.PackageId,
                    version = resolved.Version,
                    framework = resolved.Framework,
                    total,
                    truncated = types.Count < total,
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
            else if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Kind,Name,FullName,Namespace,Summary");
                foreach (var type in types)
                {
                    var displayName = type.GenericParameterCount > 0
                        ? $"{type.Name}<{new string(',', type.GenericParameterCount - 1)}>"
                        : type.Name;
                    var summary = xmlDocs?.GetTypeSummary(type.FullName) ?? "";
                    Console.WriteLine($"{type.Kind},{CommonOptions.CsvEscape(displayName)},{CommonOptions.CsvEscape(type.FullName)},{CommonOptions.CsvEscape(type.Namespace)},{CommonOptions.CsvEscape(summary)}");
                }
            }
            else if (string.Equals(format, "table", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Package: {resolved.PackageId} {resolved.Version} ({resolved.Framework})");
                Console.WriteLine();

                var rows = types.Select(t => new
                {
                    Kind = t.Kind,
                    Name = t.GenericParameterCount > 0
                        ? $"{t.Name}<{new string(',', t.GenericParameterCount - 1)}>"
                        : t.Name,
                    Namespace = t.Namespace,
                    Summary = xmlDocs?.GetTypeSummary(t.FullName) ?? "",
                }).ToList();

                var colKind = Math.Max("Kind".Length, rows.Count > 0 ? rows.Max(r => r.Kind.Length) : 0);
                var colName = Math.Max("Name".Length, rows.Count > 0 ? rows.Max(r => r.Name.Length) : 0);
                var colNs = Math.Max("Namespace".Length, rows.Count > 0 ? rows.Max(r => r.Namespace.Length) : 0);

                Console.WriteLine($"  {"Kind".PadRight(colKind)}  {"Name".PadRight(colName)}  {"Namespace".PadRight(colNs)}  Summary");
                Console.WriteLine($"  {new string('-', colKind)}  {new string('-', colName)}  {new string('-', colNs)}  -------");

                foreach (var row in rows)
                {
                    Console.WriteLine($"  {row.Kind.PadRight(colKind)}  {row.Name.PadRight(colName)}  {row.Namespace.PadRight(colNs)}  {row.Summary}");
                }

                CommonOptions.WriteTruncationFooter(total, limit, NarrowHint);
            }
            else
            {
                Console.WriteLine($"Package: {resolved.PackageId} {resolved.Version} ({resolved.Framework})");
                Console.WriteLine();

                CommonOptions.WriteGroupedByKind(
                    types,
                    kind: t => t.Kind,
                    line: t =>
                    {
                        var displayName = t.GenericParameterCount > 0
                            ? $"{t.Name}<{new string(',', t.GenericParameterCount - 1)}>"
                            : t.Name;

                        var summary = xmlDocs?.GetTypeSummary(t.FullName);
                        return summary is not null ? $"{displayName} — {summary}" : displayName;
                    },
                    write: Console.WriteLine,
                    order: GetKindOrder);

                CommonOptions.WriteTruncationFooter(total, limit, NarrowHint, leadingBlankLine: false);
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
