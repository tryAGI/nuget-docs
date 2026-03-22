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
        var memberDiff = parseResult.GetValue(command.MemberDiffOption);
        var includeAdditive = parseResult.GetValue(command.IncludeAdditiveOption);
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
                    // Both exist — compare (skip if --type-only)
                    try
                    {
                        var reflectionName = fromType!.GenericParameterCount > 0
                            ? $"{fromType.FullName}`{fromType.GenericParameterCount}"
                            : fromType.FullName;

                        if (memberDiff)
                        {
                            // Member-level comparison
                            var fromMembers = fromInspector.GetMemberSignatures(reflectionName);
                            var toMembers = toInspector.GetMemberSignatures(reflectionName);
                            var memberChanges = CompareMemberSignatures(fromMembers, toMembers);

                            if (memberChanges is not null)
                            {
                                var isBreaking = memberChanges.Removed.Count > 0 || memberChanges.Changed.Count > 0;
                                changed.Add(new ChangedType(toType!, "", "", isBreaking, memberChanges));
                            }
                        }
                        else
                        {
                            // Source-level comparison
                            var fromSource = fromInspector.DecompileType(reflectionName);
                            var toSource = toInspector.DecompileType(reflectionName);

                            if (!string.Equals(fromSource, toSource, StringComparison.Ordinal))
                            {
                                var isBreaking = HasBreakingChanges(fromSource, toSource);
                                changed.Add(new ChangedType(toType!, fromSource, toSource, isBreaking, null));
                            }
                        }
                    }
                    catch
                    {
                        changed.Add(new ChangedType(toType!, "(could not decompile)", "(could not decompile)", false, null));
                    }
                }
            }

            // When --breaking is set, filter to only breaking changes
            var filteredChanged = breakingOnly
                ? changed.Where(c => c.IsBreaking).ToList()
                : changed;

            // When --include-additive is false, skip purely additive changes
            var filteredAdded = added;
            if (!includeAdditive)
            {
                filteredAdded = []; // Skip all added types
                filteredChanged = filteredChanged
                    .Where(c => c.Members is null || c.Members.Removed.Count > 0 || c.Members.Changed.Count > 0)
                    .ToList();
            }

            // Determine if there are breaking changes for exit code
            var hasBreaking = removed.Count > 0 || changed.Any(c => c.IsBreaking);

            if (string.Equals(output, "json", StringComparison.OrdinalIgnoreCase))
            {
                OutputJson(package, fromResolved, toResolved, filteredAdded, removed, filteredChanged, typeOnly, breakingOnly, memberDiff);
            }
            else
            {
                OutputText(package, fromResolved, toResolved, filteredAdded, removed, filteredChanged, typeOnly, breakingOnly, memberDiff);
            }

            // Exit code 2 when breaking changes detected
            return hasBreaking ? 2 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Compare member signatures between two versions of a type.
    /// Returns null if no changes detected.
    /// </summary>
    private static MemberChanges? CompareMemberSignatures(
        IReadOnlyList<TypeInspector.MemberSignature> fromMembers,
        IReadOnlyList<TypeInspector.MemberSignature> toMembers)
    {
        // Key by signature for exact match
        var fromByKey = fromMembers.ToDictionary(m => $"{m.Kind}:{m.Signature}");
        var toByKey = toMembers.ToDictionary(m => $"{m.Kind}:{m.Signature}");

        var addedMembers = new List<TypeInspector.MemberSignature>();
        var removedMembers = new List<TypeInspector.MemberSignature>();

        // Check for removed/changed members
        foreach (var (key, member) in fromByKey)
        {
            if (!toByKey.ContainsKey(key))
            {
                removedMembers.Add(member);
            }
        }

        // Check for added members
        foreach (var (key, member) in toByKey)
        {
            if (!fromByKey.ContainsKey(key))
            {
                addedMembers.Add(member);
            }
        }

        if (addedMembers.Count == 0 && removedMembers.Count == 0)
        {
            return null;
        }

        // Detect signature changes: removed + added with same name = changed
        var changedMembers = new List<(TypeInspector.MemberSignature From, TypeInspector.MemberSignature To)>();
        var matchedRemoved = new HashSet<int>();
        var matchedAdded = new HashSet<int>();

        for (var i = 0; i < removedMembers.Count; i++)
        {
            for (var j = 0; j < addedMembers.Count; j++)
            {
                if (matchedAdded.Contains(j)) continue;

                if (removedMembers[i].Name == addedMembers[j].Name &&
                    removedMembers[i].Kind == addedMembers[j].Kind)
                {
                    changedMembers.Add((removedMembers[i], addedMembers[j]));
                    matchedRemoved.Add(i);
                    matchedAdded.Add(j);
                    break;
                }
            }
        }

        // Filter out matched items from added/removed
        var pureRemoved = removedMembers.Where((_, i) => !matchedRemoved.Contains(i)).ToList();
        var pureAdded = addedMembers.Where((_, i) => !matchedAdded.Contains(i)).ToList();

        if (pureAdded.Count == 0 && pureRemoved.Count == 0 && changedMembers.Count == 0)
        {
            return null;
        }

        return new MemberChanges(pureAdded, pureRemoved, changedMembers);
    }

    /// <summary>
    /// Detect if changes between two type sources contain breaking changes.
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

        return edits.Any(e => e.Kind == MyersDiff.EditKind.Delete &&
            IsSignificantLine(e.Line));
    }

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
        bool breakingOnly,
        bool memberDiff)
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
                : changed.Select(c => memberDiff && c.Members is not null
                    ? (object)new
                    {
                        kind = c.Type.Kind,
                        name = c.Type.Name,
                        fullName = c.Type.FullName,
                        isBreaking = c.IsBreaking,
                        addedMembers = c.Members.Added.Select(m => new { m.Kind, m.Name, m.Signature }),
                        removedMembers = c.Members.Removed.Select(m => new { m.Kind, m.Name, m.Signature }),
                        changedMembers = c.Members.Changed.Select(m => new
                        {
                            kind = m.From.Kind,
                            name = m.From.Name,
                            fromSignature = m.From.Signature,
                            toSignature = m.To.Signature,
                        }),
                    }
                    : new
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
        bool breakingOnly,
        bool memberDiff)
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

                    if (memberDiff && c.Members is not null)
                    {
                        OutputMemberDiff(c.Members);
                    }
                    else if (c.FromSource == "(could not decompile)")
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

    private static void OutputMemberDiff(MemberChanges members)
    {
        if (members.Removed.Count > 0)
        {
            foreach (var m in members.Removed)
            {
                Console.WriteLine($"  - [{m.Kind}] {m.Signature}");
            }
        }

        if (members.Added.Count > 0)
        {
            foreach (var m in members.Added)
            {
                Console.WriteLine($"  + [{m.Kind}] {m.Signature}");
            }
        }

        if (members.Changed.Count > 0)
        {
            foreach (var (from, to) in members.Changed)
            {
                Console.WriteLine($"  ~ [{from.Kind}] {from.Name}:");
                Console.WriteLine($"    - {from.Signature}");
                Console.WriteLine($"    + {to.Signature}");
            }
        }
    }

    private record ChangedType(
        TypeInspector.TypeInfo Type,
        string FromSource,
        string ToSource,
        bool IsBreaking,
        MemberChanges? Members);

    private record MemberChanges(
        List<TypeInspector.MemberSignature> Added,
        List<TypeInspector.MemberSignature> Removed,
        List<(TypeInspector.MemberSignature From, TypeInspector.MemberSignature To)> Changed);
}
