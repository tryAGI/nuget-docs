using System.CommandLine;
using System.CommandLine.Invocation;

namespace NugetDocs.Cli.Commands;

internal static class CommonOptions
{
    /// <summary>
    /// Returns true if output should be JSON (either --output json or --json was specified).
    /// </summary>
    public static bool IsJsonOutput(ParseResult parseResult, Option<string?> outputOption, Option<bool> jsonOption)
    {
        return parseResult.GetValue(jsonOption)
            || string.Equals(parseResult.GetValue(outputOption), "json", StringComparison.OrdinalIgnoreCase);
    }

    public static Argument<string> Package => new(
        name: "package")
    {
        Description = "NuGet package name",
    };

    public static Option<string?> Version => new(
        name: "--version",
        aliases: ["-v"])
    {
        Description = "Package version (default: latest stable)",
        DefaultValueFactory = _ => null,
    };

    public static Option<string?> Framework => new(
        name: "--framework",
        aliases: ["-f"])
    {
        Description = "Target framework moniker (default: auto-select best)",
        DefaultValueFactory = _ => null,
    };

    public static Option<string?> Output => new(
        name: "--output",
        aliases: ["-o"])
    {
        Description = "Output format: text (default) or json",
        DefaultValueFactory = _ => null,
    };

    public static Option<bool> Json => new(
        name: "--json",
        aliases: ["-j"])
    {
        Description = "Shorthand for --output json",
        DefaultValueFactory = _ => false,
    };

    public static Option<string?> Format => new(
        name: "--format")
    {
        Description = "Output format: grouped (default), table, or csv",
        DefaultValueFactory = _ => null,
    };

    /// <summary>
    /// Default maximum number of rows emitted by <c>list</c> and <c>search</c> before truncation.
    /// High enough to leave a typical package's full listing intact (Newtonsoft.Json lists 144 public
    /// types, Microsoft.Extensions.AI.Abstractions 159) while capping pathological ones
    /// (AWSSDK.EC2 lists 5,566) so an agent's context window is not exhausted by a single command.
    /// </summary>
    public const int DefaultResultLimit = 200;

    public static Option<int> Limit => new(
        name: "--limit",
        aliases: ["-l"])
    {
        Description = $"Maximum number of results to show (default: {DefaultResultLimit}, 0 = all)",
        DefaultValueFactory = _ => DefaultResultLimit,
    };

    /// <summary>
    /// Applies a result limit, returning the trimmed list. A limit of 0 or less means "no limit".
    /// </summary>
    public static IReadOnlyList<T> ApplyLimit<T>(IReadOnlyList<T> items, int limit)
    {
        return limit > 0 && items.Count > limit ? items.Take(limit).ToList() : items;
    }

    /// <summary>
    /// Writes the footer shown when a limit hid some results. No-op when nothing was truncated.
    /// </summary>
    public static void WriteTruncationFooter(int total, int limit, string narrowHint, bool leadingBlankLine = true)
    {
        if (limit <= 0 || total <= limit)
        {
            return;
        }

        if (leadingBlankLine)
        {
            Console.WriteLine();
        }
        Console.WriteLine($"  ... and {total - limit} more (use --limit 0 to show all, or {narrowHint})");
    }

    /// <summary>
    /// Default maximum number of source lines emitted by <c>show</c> before truncation.
    /// Leaves typical types whole (Newtonsoft.Json's JsonConvert decompiles to 972 lines) while
    /// capping pathological ones (AWSSDK.EC2's AmazonEC2Client is 24,550 lines / 1.6 MB).
    /// </summary>
    public const int DefaultMaxLines = 1000;

    public static Option<int> MaxLines => new(
        name: "--max-lines")
    {
        Description = $"Maximum lines of source to print (default: {DefaultMaxLines}, 0 = all)",
        DefaultValueFactory = _ => DefaultMaxLines,
    };

    /// <summary>
    /// Trims source text to at most <paramref name="maxLines"/> lines. A limit of 0 or less means
    /// "no limit". <paramref name="totalLines"/> receives the untrimmed line count.
    /// </summary>
    public static string ApplyLineLimit(string source, int maxLines, out int totalLines)
    {
        var lines = source.Split('\n');

        // A trailing newline produces a final empty element that is not a real line.
        totalLines = lines.Length > 0 && lines[^1].Length == 0 ? lines.Length - 1 : lines.Length;

        if (maxLines <= 0 || totalLines <= maxLines)
        {
            return source;
        }

        return string.Join('\n', lines.Take(maxLines)) + '\n';
    }

    /// <summary>
    /// Renders items grouped by kind: a pluralized heading, two-space indented lines, and a blank
    /// line after each group. Shared by <c>list</c> (types) and <c>show --signatures</c> (members).
    /// Writing goes through <paramref name="write"/> so callers can target the console or a buffer.
    /// </summary>
    public static void WriteGroupedByKind<T>(
        IEnumerable<T> items,
        Func<T, string> kind,
        Func<T, string> line,
        Action<string> write,
        Func<string, int>? order = null)
    {
        var groups = items.GroupBy(kind);
        var ordered = order is not null
            ? groups.OrderBy(g => order(g.Key))
            : groups.OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var group in ordered)
        {
            write($"{Pluralize(group.Key)}:");

            foreach (var item in group)
            {
                write($"  {line(item)}");
            }

            write("");
        }
    }

    /// <summary>
    /// Pluralizes a type or member kind for a group heading: Property -> Properties,
    /// Class -> Classes, Method -> Methods.
    /// </summary>
    public static string Pluralize(string kind)
    {
        // Consonant + y -> ies (Property -> Properties), but not vowel + y (Key -> Keys).
        if (kind.Length > 1 && kind.EndsWith('y') && !"aeiou".Contains(char.ToLowerInvariant(kind[^2]), StringComparison.Ordinal))
        {
            return string.Concat(kind.AsSpan(0, kind.Length - 1), "ies");
        }

        var needsEs = kind.EndsWith('s') || kind.EndsWith('x') || kind.EndsWith('z') ||
            kind.EndsWith("ch", StringComparison.Ordinal) || kind.EndsWith("sh", StringComparison.Ordinal);

        return needsEs ? $"{kind}es" : $"{kind}s";
    }

    /// <summary>
    /// Escape a value for CSV output (RFC 4180).
    /// </summary>
    public static string CsvEscape(string value)
    {
        if (value.Contains(',', StringComparison.Ordinal) ||
            value.Contains('"', StringComparison.Ordinal) ||
            value.Contains('\n', StringComparison.Ordinal))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }
        return value;
    }
}
