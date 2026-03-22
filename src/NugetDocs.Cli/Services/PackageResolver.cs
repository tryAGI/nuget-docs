using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace NugetDocs.Cli.Services;

/// <summary>
/// Resolves NuGet packages to their DLL and XML doc paths.
/// </summary>
internal sealed class PackageResolver
{
    private static readonly string NuGetCacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".nuget", "packages");

    private static readonly string[] TfmPreference =
    [
        "net10.0", "net9.0", "net8.0", "net7.0", "net6.0",
        "netstandard2.1", "netstandard2.0",
    ];

    public record ResolvedPackage(
        string PackageId,
        string Version,
        string Framework,
        string DllPath,
        string? XmlDocPath,
        string PackageDir);

    /// <summary>
    /// Resolve a package to its DLL path. Downloads if not cached.
    /// </summary>
    public static async Task<ResolvedPackage> ResolveAsync(
        string packageName,
        string? requestedVersion,
        string? requestedFramework,
        CancellationToken cancellationToken = default)
    {
        var packageId = packageName.ToLowerInvariant();

        // 1. Find or download package
        var (packageDir, version) = await FindOrDownloadPackageAsync(
            packageId, packageName, requestedVersion, cancellationToken).ConfigureAwait(false);

        // 2. Select TFM
        var (framework, dllDir) = SelectFramework(packageDir, requestedFramework);

        // 3. Find primary DLL
        var dllPath = FindPrimaryDll(dllDir, packageId);

        // 4. Find XML doc file
        var xmlDocPath = Path.ChangeExtension(dllPath, ".xml");
        if (!File.Exists(xmlDocPath))
        {
            xmlDocPath = null;
        }

        return new ResolvedPackage(
            PackageId: packageName,
            Version: version,
            Framework: framework,
            DllPath: dllPath,
            XmlDocPath: xmlDocPath,
            PackageDir: packageDir);
    }

    private static async Task<(string PackageDir, string Version)> FindOrDownloadPackageAsync(
        string packageId,
        string originalName,
        string? requestedVersion,
        CancellationToken cancellationToken)
    {
        var packageCacheDir = Path.Combine(NuGetCacheDir, packageId);

        if (requestedVersion is not null)
        {
            var versionDir = Path.Combine(packageCacheDir, requestedVersion.ToLowerInvariant());
            if (Directory.Exists(versionDir))
            {
                return (versionDir, requestedVersion);
            }
        }
        else if (Directory.Exists(packageCacheDir))
        {
            // Pick highest stable version
            var version = GetHighestVersion(packageCacheDir);
            if (version is not null)
            {
                return (Path.Combine(packageCacheDir, version), version);
            }
        }

        // Need to resolve/download
        var resolvedVersion = requestedVersion
            ?? await ResolveLatestVersionAsync(packageId, cancellationToken).ConfigureAwait(false);

        // Check cache again with resolved version
        var resolvedDir = Path.Combine(packageCacheDir, resolvedVersion.ToLowerInvariant());
        if (Directory.Exists(resolvedDir))
        {
            return (resolvedDir, resolvedVersion);
        }

        // Download
        await DownloadPackageAsync(originalName, resolvedVersion, cancellationToken).ConfigureAwait(false);

        resolvedDir = Path.Combine(packageCacheDir, resolvedVersion.ToLowerInvariant());
        if (!Directory.Exists(resolvedDir))
        {
            throw new InvalidOperationException(
                $"Package '{originalName}' version '{resolvedVersion}' was not found after download. " +
                $"Expected at: {resolvedDir}");
        }

        return (resolvedDir, resolvedVersion);
    }

    private static string? GetHighestVersion(string packageCacheDir)
    {
        var versions = Directory.GetDirectories(packageCacheDir)
            .Select(d => Path.GetFileName(d))
            .Where(v => v is not null && !v.Contains('-')) // stable only
            .OrderByDescending(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (versions.Count == 0)
        {
            // Fall back to any version (including prerelease)
            versions = Directory.GetDirectories(packageCacheDir)
                .Select(d => Path.GetFileName(d))
                .Where(v => v is not null)
                .OrderByDescending(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return versions.FirstOrDefault();
    }

    private static async Task<string> ResolveLatestVersionAsync(
        string packageId,
        CancellationToken cancellationToken)
    {
        using var http = new HttpClient();
        var url = $"https://api.nuget.org/v3-flatcontainer/{packageId}/index.json";
        var response = await http.GetFromJsonAsync<NuGetVersionIndex>(url, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Could not resolve versions for package '{packageId}'.");

        // Pick latest stable, or latest prerelease if no stable
        var stable = response.Versions?
            .Where(v => !v.Contains('-'))
            .LastOrDefault();

        var version = stable ?? response.Versions?.LastOrDefault()
            ?? throw new InvalidOperationException($"No versions found for package '{packageId}'.");

        return version;
    }

    private static async Task DownloadPackageAsync(
        string packageName,
        string version,
        CancellationToken cancellationToken)
    {
        // Use dotnet nuget to restore the package into the global cache
        var tmpDir = Path.Combine(Path.GetTempPath(), "nuget-docs-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tmpDir);

        try
        {
            // Create a temporary project that references the package
            var csproj = $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net8.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="{packageName}" Version="{version}" />
                  </ItemGroup>
                </Project>
                """;

            await File.WriteAllTextAsync(
                Path.Combine(tmpDir, "tmp.csproj"), csproj, cancellationToken).ConfigureAwait(false);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "restore",
                WorkingDirectory = tmpDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = System.Diagnostics.Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start 'dotnet restore'.");

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"Failed to download package '{packageName}@{version}': {stderr}");
            }
        }
        finally
        {
            try { Directory.Delete(tmpDir, recursive: true); } catch { /* best effort */ }
        }
    }

    private static (string Framework, string DllDir) SelectFramework(
        string packageDir,
        string? requestedFramework)
    {
        // Check both lib/ and ref/ directories
        var searchDirs = new[] { "lib", "ref" };
        var candidates = new List<(string Framework, string Dir)>();

        foreach (var subDir in searchDirs)
        {
            var baseDir = Path.Combine(packageDir, subDir);
            if (!Directory.Exists(baseDir))
            {
                continue;
            }

            foreach (var dir in Directory.GetDirectories(baseDir))
            {
                var tfm = Path.GetFileName(dir);
                if (tfm is not null && Directory.GetFiles(dir, "*.dll").Length > 0)
                {
                    candidates.Add((tfm, dir));
                }
            }
        }

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(
                $"No DLLs found in package at '{packageDir}'. Check lib/ and ref/ directories.");
        }

        if (requestedFramework is not null)
        {
            var match = candidates.FirstOrDefault(c =>
                string.Equals(c.Framework, requestedFramework, StringComparison.OrdinalIgnoreCase));

            if (match != default)
            {
                return match;
            }

            var available = string.Join(", ", candidates.Select(c => c.Framework).Distinct());
            throw new InvalidOperationException(
                $"Framework '{requestedFramework}' not found. Available: {available}");
        }

        // Auto-select by preference
        foreach (var preferred in TfmPreference)
        {
            var match = candidates.FirstOrDefault(c =>
                string.Equals(c.Framework, preferred, StringComparison.OrdinalIgnoreCase));

            if (match != default)
            {
                return match;
            }
        }

        // Fallback to first available
        return candidates[0];
    }

    private static string FindPrimaryDll(string dllDir, string packageId)
    {
        var dlls = Directory.GetFiles(dllDir, "*.dll");

        if (dlls.Length == 0)
        {
            throw new InvalidOperationException($"No DLLs found in '{dllDir}'.");
        }

        if (dlls.Length == 1)
        {
            return dlls[0];
        }

        // Match package name (case-insensitive)
        var primaryDll = dlls.FirstOrDefault(d =>
            string.Equals(
                Path.GetFileNameWithoutExtension(d),
                packageId,
                StringComparison.OrdinalIgnoreCase));

        // Try matching with dots replaced (e.g., Microsoft.Extensions.AI -> Microsoft.Extensions.AI.dll)
        primaryDll ??= dlls.FirstOrDefault(d =>
            Path.GetFileNameWithoutExtension(d)?
                .Equals(packageId.Replace("-", "."), StringComparison.OrdinalIgnoreCase) == true);

        return primaryDll ?? dlls[0];
    }

    private sealed class NuGetVersionIndex
    {
        [JsonPropertyName("versions")]
        public List<string>? Versions { get; set; }
    }
}
