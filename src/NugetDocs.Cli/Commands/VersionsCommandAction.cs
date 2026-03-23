using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using NugetDocs.Cli.Services;

namespace NugetDocs.Cli.Commands;

internal sealed class VersionsCommandAction(VersionsCommand command) : AsynchronousCommandLineAction
{
    public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        var package = parseResult.GetValue(command.PackageArgument)!;
        var stableOnly = parseResult.GetValue(command.StableOption);
        var latest = parseResult.GetValue(command.LatestOption);
        var since = parseResult.GetValue(command.SinceOption);
        var limit = parseResult.GetValue(command.LimitOption);
        var output = parseResult.GetValue(command.OutputOption);

        try
        {
            using var http = new HttpClient();
#pragma warning disable CA1308 // NuGet API requires lowercase package names
            var url = $"https://api.nuget.org/v3-flatcontainer/{package.ToLowerInvariant()}/index.json";
#pragma warning restore CA1308
            var response = await http.GetFromJsonAsync<VersionIndex>(url, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Could not resolve versions for package '{package}'.");

            var versions = response.Versions ?? [];

            if (stableOnly)
            {
                versions = versions.Where(v => !IsPrerelease(v)).ToList();
            }

            if (since is not null)
            {
#pragma warning disable CA1308 // NuGet API requires lowercase package names
                var resolvedSince = PackageResolver.IsVersionKeyword(since)
                    ? await PackageResolver.ResolveVersionKeywordAsync(
                        package.ToLowerInvariant(), since, cancellationToken).ConfigureAwait(false)
                    : since;
#pragma warning restore CA1308

                var sinceIndex = versions.IndexOf(resolvedSince);
                if (sinceIndex >= 0)
                {
                    versions = versions.Skip(sinceIndex + 1).ToList();
                    since = resolvedSince; // Use resolved version in output
                }
                else
                {
                    Console.Error.WriteLine($"Warning: Version '{resolvedSince}' not found in package history. Showing all versions.");
                }
            }

            // Show newest first
            versions.Reverse();

            if (latest)
            {
                var latestStable = versions.FirstOrDefault(v => !IsPrerelease(v));
                var latestPrerelease = versions.FirstOrDefault(IsPrerelease);
                versions = new[] { latestStable, latestPrerelease }
                    .Where(v => v is not null)
                    .Cast<string>()
                    .ToList();
            }

            var total = versions.Count;
            if (!latest && limit > 0 && versions.Count > limit)
            {
                versions = versions.Take(limit).ToList();
            }

            if (string.Equals(output, "json", StringComparison.OrdinalIgnoreCase))
            {
                var json = latest
                    ? new
                    {
                        package,
                        total,
                        stableOnly,
                        latestStable = versions.FirstOrDefault(v => !IsPrerelease(v)),
                        latestPrerelease = versions.FirstOrDefault(IsPrerelease),
                        versions,
                    }
                    : (object)new
                    {
                        package,
                        total,
                        stableOnly,
                        versions,
                    };
                Console.WriteLine(JsonSerializer.Serialize(json, JsonOptions.Indented));
            }
            else
            {
                var parts = new List<string>();
                if (latest) parts.Add("latest");
                if (stableOnly) parts.Add("stable only");
                if (since is not null) parts.Add($"since {since}");
                var filter = parts.Count > 0 ? $" ({string.Join(", ", parts)})" : "";
                Console.WriteLine($"// Versions: {package}{filter}");
                if (!latest)
                {
                    Console.WriteLine($"// Total: {total}");
                }
                Console.WriteLine();

                if (latest)
                {
                    var latestStable = versions.FirstOrDefault(v => !IsPrerelease(v));
                    var latestPrerelease = versions.FirstOrDefault(IsPrerelease);
                    if (latestStable is not null)
                    {
                        Console.WriteLine($"  {latestStable}  (stable)");
                    }
                    if (latestPrerelease is not null)
                    {
                        Console.WriteLine($"  {latestPrerelease}  (prerelease)");
                    }
                }
                else
                {
                    foreach (var v in versions)
                    {
                        Console.WriteLine($"  {v}");
                    }
                }

                if (limit > 0 && total > limit)
                {
                    Console.WriteLine();
                    Console.WriteLine($"  ... and {total - limit} more (use --limit 0 to show all)");
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

    private static bool IsPrerelease(string version) => version.Contains('-', StringComparison.Ordinal);

#pragma warning disable CA1812 // Instantiated via JSON deserialization
    private sealed class VersionIndex
#pragma warning restore CA1812
    {
        [JsonPropertyName("versions")]
        public List<string>? Versions { get; set; }
    }
}
