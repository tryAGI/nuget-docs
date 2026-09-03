using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using System.Xml.Linq;
using NugetDocs.Cli.Services;

namespace NugetDocs.Cli.Commands;

internal sealed class DepsCommandAction(DepsCommand command) : AsynchronousCommandLineAction
{
    public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        var package = parseResult.GetValue(command.PackageArgument)!;
        var version = parseResult.GetValue(command.VersionOption);
        var framework = parseResult.GetValue(command.FrameworkOption);
        var maxDepth = parseResult.GetValue(command.DepthOption);
        var limit = parseResult.GetValue(command.LimitOption);
        var format = parseResult.GetValue(command.FormatOption);
        var jsonOutput = CommonOptions.IsJsonOutput(parseResult, command.OutputOption, command.JsonOption);

        try
        {
            var resolved = await PackageResolver.ResolveMetadataOnlyAsync(
                package, version, cancellationToken).ConfigureAwait(false);

            var fullTree = await BuildDependencyTreeAsync(
                package, resolved.Version, framework, maxDepth, cancellationToken).ConfigureAwait(false);

            // A wide --depth can fan out without bound (EF Core SqlServer reaches 135 nodes at
            // depth 4), so cap the node count the same way list/search cap rows.
            var total = CountNodes(fullTree.Dependencies);
            var remaining = limit > 0 ? limit : int.MaxValue;
            var tree = limit > 0 && total > limit
                ? fullTree with { Dependencies = TrimTree(fullTree.Dependencies, ref remaining) }
                : fullTree;
            const string NarrowHint = "a lower --depth to narrow";

            if (jsonOutput)
            {
                // Keeps the existing PascalCase DepNode shape, with the pre-limit count alongside.
                Console.WriteLine(JsonSerializer.Serialize(
                    new
                    {
                        tree.Id,
                        tree.Version,
                        tree.Dependencies,
                        tree.Deduplicated,
                        Total = total,
                        Truncated = limit > 0 && total > limit,
                    },
                    JsonOptions.Indented));
            }
            else if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Depth,Package,Version,Deduplicated");
                FlattenTree(tree.Dependencies, 0, (node, depth) =>
                {
                    var dedup = node.Deduplicated ? "true" : "false";
                    Console.WriteLine($"{depth},{CommonOptions.CsvEscape(node.Id)},{CommonOptions.CsvEscape(node.Version)},{dedup}");
                });
            }
            else if (string.Equals(format, "table", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Dependencies: {package} {resolved.Version} ({resolved.Framework ?? "any"})");
                Console.WriteLine();

                if (tree.Dependencies.Count == 0)
                {
                    Console.WriteLine("No dependencies.");
                }
                else
                {
                    var rows = new List<(int Depth, string Id, string Version, bool Deduplicated)>();
                    FlattenTree(tree.Dependencies, 0, (node, depth) =>
                    {
                        rows.Add((depth, node.Id, node.Version, node.Deduplicated));
                    });

                    var colDepth = "Depth".Length;
                    var colId = Math.Max("Package".Length, rows.Max(r => r.Id.Length));
                    var colVer = Math.Max("Version".Length, rows.Max(r => r.Version.Length));

                    Console.WriteLine($"  {"Depth".PadRight(colDepth)}  {"Package".PadRight(colId)}  {"Version".PadRight(colVer)}  Note");
                    Console.WriteLine($"  {new string('-', colDepth)}  {new string('-', colId)}  {new string('-', colVer)}  ----");

                    foreach (var row in rows)
                    {
                        var note = row.Deduplicated ? "(already listed)" : "";
                        Console.WriteLine($"  {row.Depth.ToString(System.Globalization.CultureInfo.InvariantCulture).PadRight(colDepth)}  {row.Id.PadRight(colId)}  {row.Version.PadRight(colVer)}  {note}");
                    }

                    CommonOptions.WriteTruncationFooter(total, limit, NarrowHint);
                }
            }
            else
            {
                Console.WriteLine($"// Dependencies: {package} {resolved.Version} ({resolved.Framework ?? "any"})");
                Console.WriteLine();

                if (tree.Dependencies.Count == 0)
                {
                    Console.WriteLine("No dependencies.");
                }
                else
                {
                    PrintTree(tree.Dependencies, "", true);

                    CommonOptions.WriteTruncationFooter(total, limit, NarrowHint);
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

    private static async Task<DepNode> BuildDependencyTreeAsync(
        string packageName,
        string version,
        string? framework,
        int maxDepth,
        CancellationToken cancellationToken)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return await ResolveNodeAsync(packageName, version, framework, 0, maxDepth, visited, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<DepNode> ResolveNodeAsync(
        string packageName,
        string version,
        string? framework,
        int currentDepth,
        int maxDepth,
        HashSet<string> visited,
        CancellationToken cancellationToken)
    {
        var key = $"{packageName}@{version}".ToUpperInvariant();
        var children = new List<DepNode>();
        var deduplicated = false;

        if (currentDepth < maxDepth)
        {
            if (!visited.Add(key))
            {
                // Already resolved this package — mark as deduplicated
                deduplicated = true;
            }
            else
            {
                try
                {
                    var resolved = await PackageResolver.ResolveMetadataOnlyAsync(
                        packageName, version, cancellationToken).ConfigureAwait(false);

                    var deps = GetDependenciesFromNuspec(resolved.PackageDir, framework);

                    foreach (var dep in deps)
                    {
                        var child = await ResolveNodeAsync(
                            dep.Id, dep.Version, framework,
                            currentDepth + 1, maxDepth, visited, cancellationToken).ConfigureAwait(false);
                        children.Add(child);
                    }
                }
                catch
                {
                    // Can't resolve this dependency — show what we know
                }
            }
        }

        return new DepNode(packageName, version, children, deduplicated);
    }

    private static List<DepEntry> GetDependenciesFromNuspec(string packageDir, string? framework)
    {
        var nuspecFiles = Directory.GetFiles(packageDir, "*.nuspec");
        if (nuspecFiles.Length == 0)
        {
            return [];
        }

        var doc = XDocument.Load(nuspecFiles[0]);
        var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
        var metadata = doc.Root?.Element(ns + "metadata");
        var dependencies = metadata?.Element(ns + "dependencies");

        if (dependencies is null)
        {
            return [];
        }

        // Try to find dependencies for the specific framework
        var groups = dependencies.Elements(ns + "group").ToList();
        if (groups.Count > 0)
        {
            XElement? matchGroup = null;

            if (framework is not null)
            {
                // Exact match first
                matchGroup = groups.FirstOrDefault(g =>
                    string.Equals(g.Attribute("targetFramework")?.Value, framework, StringComparison.OrdinalIgnoreCase));

                // Fallback: best prefix match (e.g., net10.0 matches .NETCoreApp,Version=v10.0)
                matchGroup ??= groups.FirstOrDefault(g =>
                {
                    var tfm = g.Attribute("targetFramework")?.Value ?? "";
                    return tfm.Contains(framework, StringComparison.OrdinalIgnoreCase) ||
                           framework.Contains(tfm, StringComparison.OrdinalIgnoreCase);
                });
            }

            // Fallback: any group
            matchGroup ??= groups.FirstOrDefault();

            if (matchGroup is not null)
            {
                return matchGroup.Elements(ns + "dependency")
                    .Select(d => new DepEntry(
                        d.Attribute("id")?.Value ?? "",
                        CleanVersionRange(d.Attribute("version")?.Value ?? "")))
                    .Where(d => d.Id.Length > 0)
                    .ToList();
            }
        }

        // No groups — flat dependency list
        return dependencies.Elements(ns + "dependency")
            .Select(d => new DepEntry(
                d.Attribute("id")?.Value ?? "",
                CleanVersionRange(d.Attribute("version")?.Value ?? "")))
            .Where(d => d.Id.Length > 0)
            .ToList();
    }

    /// <summary>
    /// Clean NuGet version range to a simple version string.
    /// "[1.0.0, )" → "1.0.0", "(, 2.0.0]" → "2.0.0", "1.0.0" → "1.0.0"
    /// </summary>
    private static string CleanVersionRange(string versionRange)
    {
        var v = versionRange.Trim('[', ']', '(', ')', ' ');
        var parts = v.Split(',');
        // Use the lower bound if it exists, otherwise the upper bound
        var lower = parts[0].Trim();
        if (lower.Length > 0)
        {
            return lower;
        }

        return parts.Length > 1 ? parts[1].Trim() : versionRange;
    }

    /// <summary>
    /// Total node count of a dependency tree, matching what the flat and tree renderers emit.
    /// </summary>
    private static int CountNodes(List<DepNode> nodes)
    {
        var count = 0;
        FlattenTree(nodes, 0, (_, _) => count++);
        return count;
    }

    /// <summary>
    /// Trims a tree to the first <paramref name="remaining"/> nodes in the same depth-first order
    /// the renderers walk, so the printed prefix matches an untrimmed run.
    /// </summary>
    private static List<DepNode> TrimTree(List<DepNode> nodes, ref int remaining)
    {
        var trimmed = new List<DepNode>();

        foreach (var node in nodes)
        {
            if (remaining <= 0)
            {
                break;
            }

            remaining--;
            var children = node.Dependencies.Count > 0
                ? TrimTree(node.Dependencies, ref remaining)
                : node.Dependencies;

            trimmed.Add(node with { Dependencies = children });
        }

        return trimmed;
    }

    private static void FlattenTree(List<DepNode> nodes, int depth, Action<DepNode, int> action)
    {
        foreach (var node in nodes)
        {
            action(node, depth);
            if (node.Dependencies.Count > 0)
            {
                FlattenTree(node.Dependencies, depth + 1, action);
            }
        }
    }

    private static void PrintTree(List<DepNode> nodes, string indent, bool isLast)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var last = i == nodes.Count - 1;
            var connector = last ? "└── " : "├── ";
            var dedup = node.Deduplicated ? " (already listed)" : "";

            Console.WriteLine($"{indent}{connector}{node.Id} {node.Version}{dedup}");

            if (node.Dependencies.Count > 0)
            {
                var childIndent = indent + (last ? "    " : "│   ");
                PrintTree(node.Dependencies, childIndent, last);
            }
        }
    }

    private sealed record DepEntry(string Id, string Version);
    private sealed record DepNode(string Id, string Version, List<DepNode> Dependencies, bool Deduplicated = false);
}
