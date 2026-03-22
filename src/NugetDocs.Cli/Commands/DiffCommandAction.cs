using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using NugetDocs.Cli.Services;

namespace NugetDocs.Cli.Commands;

internal sealed class DiffCommandAction(DiffCommand command) : AsynchronousCommandLineAction
{
    public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        var package = parseResult.GetValue(command.PackageArgument)!;
        var fromVersion = parseResult.GetValue(command.FromOption)!;
        var toVersion = parseResult.GetValue(command.ToOption)!;
        var framework = parseResult.GetValue(command.FrameworkOption);
        var typeOnly = parseResult.GetValue(command.TypeOnlyOption);
        var breakingOnly = parseResult.GetValue(command.BreakingOption);
        var output = parseResult.GetValue(command.OutputOption);

        try
        {
            // Resolve both versions
            var fromResolved = await PackageResolver.ResolveAsync(
                package, fromVersion, framework, cancellationToken).ConfigureAwait(false);
            var toResolved = await PackageResolver.ResolveAsync(
                package, toVersion, framework, cancellationToken).ConfigureAwait(false);

            using var fromInspector = new TypeInspector(fromResolved.DllPath, fromResolved.XmlDocPath);
            using var toInspector = new TypeInspector(toResolved.DllPath, toResolved.XmlDocPath);

            // Use unique key that includes generic arity to avoid collisions
            // (e.g., JsonConverter and JsonConverter<T> both have FullName "Newtonsoft.Json.JsonConverter")
            static string TypeKey(TypeInspector.TypeInfo t) =>
                t.GenericParameterCount > 0 ? $"{t.FullName}`{t.GenericParameterCount}" : t.FullName;

            var fromTypes = fromInspector.GetTypes().ToDictionary(TypeKey);
            var toTypes = toInspector.GetTypes().ToDictionary(TypeKey);

            var allTypeKeys = fromTypes.Keys.Union(toTypes.Keys).OrderBy(n => n).ToList();

            var added = new List<TypeInspector.TypeInfo>();
            var removed = new List<TypeInspector.TypeInfo>();
            var changed = new List<ChangedType>();

            foreach (var typeKey in allTypeKeys)
            {
                var inFrom = fromTypes.TryGetValue(typeKey, out var fromType);
                var inTo = toTypes.TryGetValue(typeKey, out var toType);

                if (!inFrom && inTo)
                {
                    added.Add(toType!);
                }
                else if (inFrom && !inTo)
                {
                    removed.Add(fromType!);
                }
                else if (inFrom && inTo && !typeOnly)
                {
                    // Both exist — compare decompiled source (skip if --type-only)
                    try
                    {
                        var reflectionName = fromType!.GenericParameterCount > 0
                            ? $"{fromType.FullName}`{fromType.GenericParameterCount}"
                            : fromType.FullName;
                        var fromSource = fromInspector.DecompileType(reflectionName);
                        var toSource = toInspector.DecompileType(reflectionName);

                        if (!string.Equals(fromSource, toSource, StringComparison.Ordinal))
                        {
                            var isBreaking = HasBreakingChanges(fromSource, toSource);
                            changed.Add(new ChangedType(toType!, fromSource, toSource, isBreaking));
                        }
                    }
                    catch
                    {
                        changed.Add(new ChangedType(toType!, "(could not decompile)", "(could not decompile)", false));
                    }
                }
            }

            // When --breaking is set, filter to only breaking changes
            var filteredChanged = breakingOnly
                ? changed.Where(c => c.IsBreaking).ToList()
                : changed;

            if (string.Equals(output, "json", StringComparison.OrdinalIgnoreCase))
            {
                OutputJson(package, fromResolved, toResolved, added, removed, filteredChanged, typeOnly, breakingOnly);
            }
            else
            {
                OutputText(package, fromResolved, toResolved, added, removed, filteredChanged, typeOnly, breakingOnly);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Detect if changes between two type sources contain breaking changes.
    /// Breaking = lines removed from old source (member removals, signature changes).
    /// </summary>
    private static bool HasBreakingChanges(string fromSource, string toSource)
    {
        if (fromSource == "(could not decompile)")
        {
            return false;
        }

        var fromLines = fromSource.Split('\n');
        var toLines = toSource.Split('\n');
        var edits = MyersDiff.Compute(fromLines, toLines);

        // If any lines were deleted, it's potentially breaking
        return edits.Any(e => e.Kind == MyersDiff.EditKind.Delete &&
            IsSignificantLine(e.Line));
    }

    /// <summary>
    /// Check if a line is significant for breaking change detection.
    /// Filters out comments, whitespace, attributes, and using statements.
    /// </summary>
    private static bool IsSignificantLine(string line)
    {
        var trimmed = line.TrimStart();

        if (string.IsNullOrWhiteSpace(trimmed))
            return false;
        if (trimmed.StartsWith("///", StringComparison.Ordinal))
            return false;
        if (trimmed.StartsWith("//", StringComparison.Ordinal))
            return false;
        if (trimmed.StartsWith("using ", StringComparison.Ordinal))
            return false;
        if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
            return false;
        if (trimmed is "{" or "}" or "")
            return false;

        return true;
    }

    private static void OutputJson(
        string package,
        PackageResolver.ResolvedPackage fromResolved,
        PackageResolver.ResolvedPackage toResolved,
        List<TypeInspector.TypeInfo> added,
        List<TypeInspector.TypeInfo> removed,
        List<ChangedType> changed,
        bool typeOnly,
        bool breakingOnly)
    {
        var json = new
        {
            package,
            from = new { version = fromResolved.Version, framework = fromResolved.Framework },
            to = new { version = toResolved.Version, framework = toResolved.Framework },
            breakingOnly,
            summary = new
            {
                addedCount = breakingOnly ? 0 : added.Count,
                removedCount = removed.Count,
                changedCount = changed.Count,
                breakingChangedCount = changed.Count(c => c.IsBreaking),
            },
            added = breakingOnly ? null : added.Select(t => new { kind = t.Kind, name = t.Name, fullName = t.FullName }),
            removed = removed.Select(t => new { kind = t.Kind, name = t.Name, fullName = t.FullName }),
            changed = typeOnly
                ? null
                : changed.Select(c => new
                {
                    kind = c.Type.Kind,
                    name = c.Type.Name,
                    fullName = c.Type.FullName,
                    isBreaking = c.IsBreaking,
                    fromSource = c.FromSource,
                    toSource = c.ToSource,
                }),
        };
        Console.WriteLine(JsonSerializer.Serialize(json, JsonOptions.Indented));
    }

    private static void OutputText(
        string package,
        PackageResolver.ResolvedPackage fromResolved,
        PackageResolver.ResolvedPackage toResolved,
        List<TypeInspector.TypeInfo> added,
        List<TypeInspector.TypeInfo> removed,
        List<ChangedType> changed,
        bool typeOnly,
        bool breakingOnly)
    {
        Console.WriteLine($"// Diff: {package} {fromResolved.Version} → {toResolved.Version}");
        Console.WriteLine($"// Framework: {fromResolved.Framework} → {toResolved.Framework}");
        if (breakingOnly)
        {
            Console.WriteLine("// Filter: breaking changes only");
        }
        Console.WriteLine();

        var showAdded = !breakingOnly && added.Count > 0;
        var hasChanges = showAdded || removed.Count > 0 || changed.Count > 0;

        if (!hasChanges)
        {
            Console.WriteLine(breakingOnly
                ? "No breaking API changes detected."
                : "No public API changes detected.");
            return;
        }

        // Summary line
        if (breakingOnly)
        {
            Console.WriteLine($"Breaking changes: -{removed.Count} removed, ~{changed.Count} changed with removals");
        }
        else
        {
            var breakingCount = changed.Count(c => c.IsBreaking);
            var summaryParts = $"+{added.Count} added, -{removed.Count} removed, ~{changed.Count} changed";
            if (breakingCount > 0)
            {
                summaryParts += $" ({breakingCount} breaking)";
            }
            Console.WriteLine($"Summary: {summaryParts}");
        }
        Console.WriteLine();

        if (showAdded)
        {
            Console.WriteLine("Added:");
            foreach (var type in added)
            {
                Console.WriteLine($"  + [{type.Kind}] {type.FullName}");
            }
            Console.WriteLine();
        }

        if (removed.Count > 0)
        {
            Console.WriteLine(breakingOnly ? "Removed (BREAKING):" : "Removed:");
            foreach (var type in removed)
            {
                Console.WriteLine($"  - [{type.Kind}] {type.FullName}");
            }
            Console.WriteLine();
        }

        if (changed.Count > 0)
        {
            Console.WriteLine(breakingOnly ? "Changed (BREAKING):" : "Changed:");
            foreach (var c in changed)
            {
                var label = c.IsBreaking ? " ⚠" : "";
                Console.WriteLine($"  ~ [{c.Type.Kind}] {c.Type.FullName}{label}");
            }
            Console.WriteLine();

            // Show detailed diffs (skip if --type-only)
            if (!typeOnly)
            {
                Console.WriteLine("--- Detailed changes ---");
                Console.WriteLine();

                foreach (var c in changed)
                {
                    var label = c.IsBreaking ? " (BREAKING)" : "";
                    Console.WriteLine($"=== {c.Type.FullName}{label} ===");
                    Console.WriteLine();

                    if (c.FromSource == "(could not decompile)")
                    {
                        Console.WriteLine("  (could not decompile for comparison)");
                    }
                    else
                    {
                        var fromLines = c.FromSource.Split('\n');
                        var toLines = c.ToSource.Split('\n');
                        var edits = MyersDiff.Compute(fromLines, toLines);
                        var diffLines = MyersDiff.FormatUnified(edits);

                        foreach (var line in diffLines)
                        {
                            Console.WriteLine(line);
                        }
                    }

                    Console.WriteLine();
                }
            }
        }
    }

    private record ChangedType(
        TypeInspector.TypeInfo Type,
        string FromSource,
        string ToSource,
        bool IsBreaking);
}
