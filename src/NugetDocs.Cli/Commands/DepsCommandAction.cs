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
        var jsonOutput = CommonOptions.IsJsonOutput(parseResult, command.OutputOption, command.JsonOption);

        try
        {
            var resolved = await PackageResolver.ResolveAsync(
                package, version, framework, cancellationToken).ConfigureAwait(false);

            var tree = await BuildDependencyTreeAsync(
                package, resolved.Version, resolved.Framework, maxDepth, cancellationToken).ConfigureAwait(false);

            if (jsonOutput)
            {
                Console.WriteLine(JsonSerializer.Serialize(tree, JsonOptions.Indented));
            }
            else
            {
                Console.WriteLine($"// Dependencies: {package} {resolved.Version} ({resolved.Framework})");
                Console.WriteLine();

                if (tree.Dependencies.Count == 0)
                {
                    Console.WriteLine("No dependencies.");
                }
                else
                {
                    PrintTree(tree.Dependencies, "", true);
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
        string framework,
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
        string framework,
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
                    var resolved = await PackageResolver.ResolveAsync(
                        packageName, version, framework, cancellationToken).ConfigureAwait(false);

                    var deps = GetDependenciesFromNuspec(resolved.PackageDir, resolved.Framework);

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

    private static List<DepEntry> GetDependenciesFromNuspec(string packageDir, string framework)
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
            // Exact match first
            var matchGroup = groups.FirstOrDefault(g =>
                string.Equals(g.Attribute("targetFramework")?.Value, framework, StringComparison.OrdinalIgnoreCase));

            // Fallback: best prefix match (e.g., net10.0 matches .NETCoreApp,Version=v10.0)
            matchGroup ??= groups.FirstOrDefault(g =>
            {
                var tfm = g.Attribute("targetFramework")?.Value ?? "";
                return tfm.Contains(framework, StringComparison.OrdinalIgnoreCase) ||
                       framework.Contains(tfm, StringComparison.OrdinalIgnoreCase);
            });

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
