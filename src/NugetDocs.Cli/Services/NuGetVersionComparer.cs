namespace NugetDocs.Cli.Services;

/// <summary>
/// Compares NuGet version strings using semantic versioning rules.
/// Handles major.minor.patch[-prerelease] format.
/// </summary>
internal sealed class NuGetVersionComparer : IComparer<string?>
{
    public static NuGetVersionComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        if (x is null && y is null) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        var (xParts, xPre) = Split(x);
        var (yParts, yPre) = Split(y);

        // Compare numeric parts
        var maxLen = Math.Max(xParts.Length, yParts.Length);
        for (var i = 0; i < maxLen; i++)
        {
            var xVal = i < xParts.Length ? xParts[i] : 0;
            var yVal = i < yParts.Length ? yParts[i] : 0;
            var cmp = xVal.CompareTo(yVal);
            if (cmp != 0) return cmp;
        }

        // Equal numeric parts — compare prerelease
        // No prerelease > has prerelease (stable wins)
        if (xPre is null && yPre is null) return 0;
        if (xPre is null) return 1;
        if (yPre is null) return -1;

        return string.Compare(xPre, yPre, StringComparison.OrdinalIgnoreCase);
    }

    private static (int[] Parts, string? Prerelease) Split(string version)
    {
        var dashIdx = version.IndexOf('-', StringComparison.Ordinal);
        var versionPart = dashIdx >= 0 ? version[..dashIdx] : version;
        var prerelease = dashIdx >= 0 ? version[(dashIdx + 1)..] : null;

        var segments = versionPart.Split('.');
        var parts = new int[segments.Length];
        for (var i = 0; i < segments.Length; i++)
        {
            _ = int.TryParse(segments[i], out parts[i]);
        }

        return (parts, prerelease);
    }
}
