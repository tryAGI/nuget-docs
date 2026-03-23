using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using System.Xml.Linq;
using NugetDocs.Cli.Services;

namespace NugetDocs.Cli.Commands;

internal sealed class InfoCommandAction(InfoCommand command) : AsynchronousCommandLineAction
{
    public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        var package = parseResult.GetValue(command.PackageArgument)!;
        var version = parseResult.GetValue(command.VersionOption);
        var jsonOutput = CommonOptions.IsJsonOutput(parseResult, command.OutputOption, command.JsonOption);

        try
        {
            var resolved = await PackageResolver.ResolveAsync(
                package, version, requestedFramework: null, cancellationToken).ConfigureAwait(false);

            // Find .nuspec file
            var nuspecFiles = Directory.GetFiles(resolved.PackageDir, "*.nuspec");
            if (nuspecFiles.Length == 0)
            {
                Console.Error.WriteLine("No .nuspec file found in package.");
                return 1;
            }

            var doc = XDocument.Load(nuspecFiles[0]);
            var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
            var metadata = doc.Root?.Element(ns + "metadata");

            if (metadata is null)
            {
                Console.Error.WriteLine("Invalid .nuspec: no metadata element.");
                return 1;
            }

            // Collect frameworks
            var libDir = Path.Combine(resolved.PackageDir, "lib");
            var tfms = Directory.Exists(libDir)
                ? Directory.GetDirectories(libDir)
                    .Select(Path.GetFileName)
                    .Where(n => n is not null)
                    .ToList()
                : [];

            // Collect dependencies
            var dependencies = metadata.Element(ns + "dependencies");
            var depData = CollectDependencies(dependencies, ns);

            if (jsonOutput)
            {
                var json = new
                {
                    id = GetValue(metadata, ns, "id"),
                    version = GetValue(metadata, ns, "version"),
                    authors = GetValue(metadata, ns, "authors"),
                    description = GetValue(metadata, ns, "description"),
                    license = GetValue(metadata, ns, "license"),
                    licenseUrl = GetValue(metadata, ns, "licenseUrl"),
                    projectUrl = GetValue(metadata, ns, "projectUrl"),
                    tags = GetValue(metadata, ns, "tags"),
                    frameworks = tfms,
                    dependencies = depData,
                };
                Console.WriteLine(JsonSerializer.Serialize(json, JsonOptions.Indented));
            }
            else
            {
                Console.WriteLine($"Package: {GetValue(metadata, ns, "id")}");
                Console.WriteLine($"Version: {GetValue(metadata, ns, "version")}");
                Console.WriteLine($"Authors: {GetValue(metadata, ns, "authors")}");
                Console.WriteLine($"Description: {GetValue(metadata, ns, "description")}");

                var license = GetValue(metadata, ns, "license");
                var licenseUrl = GetValue(metadata, ns, "licenseUrl");
                if (license is not null)
                {
                    Console.WriteLine($"License: {license}");
                }
                else if (licenseUrl is not null)
                {
                    Console.WriteLine($"License URL: {licenseUrl}");
                }

                var projectUrl = GetValue(metadata, ns, "projectUrl");
                if (projectUrl is not null)
                {
                    Console.WriteLine($"Project URL: {projectUrl}");
                }

                var tags = GetValue(metadata, ns, "tags");
                if (tags is not null)
                {
                    Console.WriteLine($"Tags: {tags}");
                }

                if (tfms.Count > 0)
                {
                    Console.WriteLine($"Frameworks: {string.Join(", ", tfms)}");
                }

                if (depData.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("Dependencies:");

                    foreach (var (tfm, deps) in depData)
                    {
                        Console.WriteLine($"  {tfm}:");
                        foreach (var dep in deps)
                        {
                            Console.WriteLine($"    {dep.Id} {dep.Version}");
                        }
                    }
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

    private static Dictionary<string, List<DepInfo>> CollectDependencies(XElement? dependencies, XNamespace ns)
    {
        var result = new Dictionary<string, List<DepInfo>>();

        if (dependencies is null)
        {
            return result;
        }

        var groups = dependencies.Elements(ns + "group").ToList();
        if (groups.Count > 0)
        {
            foreach (var group in groups)
            {
                var tfm = group.Attribute("targetFramework")?.Value ?? "any";
                var deps = group.Elements(ns + "dependency")
                    .Select(d => new DepInfo(
                        d.Attribute("id")?.Value ?? "",
                        d.Attribute("version")?.Value ?? ""))
                    .ToList();
                if (deps.Count > 0)
                {
                    result[tfm] = deps;
                }
            }
        }
        else
        {
            var deps = dependencies.Elements(ns + "dependency")
                .Select(d => new DepInfo(
                    d.Attribute("id")?.Value ?? "",
                    d.Attribute("version")?.Value ?? ""))
                .ToList();
            if (deps.Count > 0)
            {
                result["any"] = deps;
            }
        }

        return result;
    }

    private static string? GetValue(XElement parent, XNamespace ns, string name)
    {
        var value = parent.Element(ns + name)?.Value?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private sealed record DepInfo(string Id, string Version);
}
