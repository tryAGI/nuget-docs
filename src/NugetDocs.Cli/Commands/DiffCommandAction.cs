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
        var noAdditive = parseResult.GetValue(command.NoAdditiveOption);
        if (noAdditive) includeAdditive = false;
        var ignoreDocs = parseResult.GetValue(command.IgnoreDocsOption);
        var format = parseResult.GetValue(command.FormatOption);
        var jsonOutput = CommonOptions.IsJsonOutput(parseResult, command.OutputOption, command.JsonOption);

        try
        {
            // Resolve both versions ("latest", "latest-stable", "latest-prerelease" handled by PackageResolver)
            var fromResolved = await PackageResolver.ResolveAsync(
                package, fromVersion, framework, cancellationToken).ConfigureAwait(false);
            var toResolved = await PackageResolver.ResolveAsync(
                package, toVersion, framework, cancellationToken).ConfigureAwait(false);

            using var fromInspector = new TypeInspector(fromResolved.DllPath!, fromResolved.XmlDocPath);
            using var toInspector = new TypeInspector(toResolved.DllPath!, toResolved.XmlDocPath);

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
                else if (inFrom && inTo)
                {
                    // A whole type gaining [Obsolete] or [Experimental] never shows up in a
                    // member-signature comparison, and needs no decompilation to spot.
                    var newlyDeprecated =
                        fromType!.ObsoleteMessage is null && toType!.ObsoleteMessage is not null
                            ? toType.ObsoleteMessage
                            : null;
                    var newlyExperimental =
                        fromType.ExperimentalId is null && toType!.ExperimentalId is not null
                            ? toType.ExperimentalId
                            : null;
                    var noLongerExperimental =
                        fromType.ExperimentalId is not null && toType!.ExperimentalId is null
                            ? fromType.ExperimentalId
                            : null;

                    if (typeOnly)
                    {
                        // --type-only does no decompilation, but still reports these.
                        if (newlyDeprecated is not null || newlyExperimental is not null ||
                            noLongerExperimental is not null)
                        {
                            changed.Add(new ChangedType(
                                toType!, "", "", false, null,
                                newlyDeprecated, newlyExperimental, noLongerExperimental));
                        }

                        continue;
                    }

                    try
                    {
                        var reflectionName = fromType.ReflectionName;

                        if (memberDiff)
                        {
                            // Member-level comparison
                            var fromMembers = fromInspector.GetMemberSignatures(reflectionName);
                            var toMembers = toInspector.GetMemberSignatures(reflectionName);
                            var memberChanges = CompareMemberSignatures(fromMembers, toMembers);

                            if (memberChanges is not null || newlyDeprecated is not null ||
                                newlyExperimental is not null || noLongerExperimental is not null)
                            {
                                var isBreaking = memberChanges is not null &&
                                    (memberChanges.Removed.Count > 0 || memberChanges.Changed.Count > 0);
                                changed.Add(new ChangedType(
                                    toType!, "", "", isBreaking, memberChanges,
                                    newlyDeprecated, newlyExperimental, noLongerExperimental));
                            }
                        }
                        else
                        {
                            // Source-level comparison
                            var fromSource = fromInspector.DecompileType(reflectionName);
                            var toSource = toInspector.DecompileType(reflectionName);

                            // When --ignore-docs, strip XML doc comments before comparing
                            var fromCompare = ignoreDocs ? StripDocComments(fromSource) : fromSource;
                            var toCompare = ignoreDocs ? StripDocComments(toSource) : toSource;

                            if (!string.Equals(fromCompare, toCompare, StringComparison.Ordinal))
                            {
                                var isBreaking = HasBreakingChanges(fromCompare, toCompare);
                                // Store original sources for display (with docs), but use stripped for comparison
                                changed.Add(new ChangedType(
                                    toType!, fromSource, toSource, isBreaking, null,
                                    newlyDeprecated, newlyExperimental, noLongerExperimental));
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
                    .Where(c => !IsPurelyAdditive(c, ignoreDocs))
                    .ToList();
            }

            // Determine if there are breaking changes for exit code
            var hasBreaking = removed.Count > 0 || changed.Any(c => c.IsBreaking);

            if (jsonOutput)
            {
                OutputJson(package, fromResolved, toResolved, filteredAdded, removed, filteredChanged, typeOnly, breakingOnly, memberDiff);
            }
            else if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            {
                OutputCsv(filteredAdded, removed, filteredChanged);
            }
            else if (string.Equals(format, "table", StringComparison.OrdinalIgnoreCase))
            {
                OutputTable(package, fromResolved, toResolved, filteredAdded, removed, filteredChanged);
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

        // A member that gains [Obsolete] keeps its signature, so the key-based passes below see no
        // change at all. Catch that transition separately — it is the migration signal.
        var deprecatedMembers = new List<(TypeInspector.MemberSignature From, TypeInspector.MemberSignature To)>();
        var experimentalMembers = new List<(TypeInspector.MemberSignature From, TypeInspector.MemberSignature To)>();
        var stabilizedMembers = new List<(TypeInspector.MemberSignature From, TypeInspector.MemberSignature To)>();
        foreach (var (key, fromMember) in fromByKey)
        {
            if (!toByKey.TryGetValue(key, out var toMember))
            {
                continue;
            }

            if (fromMember.ObsoleteMessage is null && toMember.ObsoleteMessage is not null)
            {
                deprecatedMembers.Add((fromMember, toMember));
            }

            if (fromMember.ExperimentalId is null && toMember.ExperimentalId is not null)
            {
                experimentalMembers.Add((fromMember, toMember));
            }

            // The common direction in practice: an API graduating to stable.
            if (fromMember.ExperimentalId is not null && toMember.ExperimentalId is null)
            {
                stabilizedMembers.Add((fromMember, toMember));
            }
        }

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

        if (addedMembers.Count == 0 && removedMembers.Count == 0 &&
            deprecatedMembers.Count == 0 && experimentalMembers.Count == 0 &&
            stabilizedMembers.Count == 0)
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

        if (pureAdded.Count == 0 && pureRemoved.Count == 0 && changedMembers.Count == 0 &&
            deprecatedMembers.Count == 0 && experimentalMembers.Count == 0 &&
            stabilizedMembers.Count == 0)
        {
            return null;
        }

        return new MemberChanges(
            pureAdded, pureRemoved, changedMembers,
            deprecatedMembers, experimentalMembers, stabilizedMembers);
    }

    /// <summary>
    /// Strip XML doc comment lines (/// ...) from source, collapsing consecutive blank lines.
    /// </summary>
    private static string StripDocComments(string source)
    {
        var lines = source.Split('\n');
        var result = new List<string>(lines.Length);
        var lastWasBlank = false;

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("///", StringComparison.Ordinal))
            {
                continue;
            }

            var isBlank = string.IsNullOrWhiteSpace(line);
            if (isBlank && lastWasBlank) continue;

            result.Add(line);
            lastWasBlank = isBlank;
        }

        return string.Join('\n', result);
    }

    /// <summary>
    /// Check if a changed type has only additive changes (no removals or modifications).
    /// Works for both member-diff and source-level diff modes.
    /// When ignoreDocs is true, doc comment changes are stripped before checking.
    /// </summary>
    private static bool IsPurelyAdditive(ChangedType c, bool ignoreDocs)
    {
        if (c.NewlyDeprecated is not null || c.NewlyExperimental is not null ||
            c.NoLongerExperimental is not null)
        {
            return false;
        }

        // Member-level diff: purely additive if no removals and no changed signatures
        if (c.Members is not null)
        {
            return c.Members.Removed.Count == 0 && c.Members.Changed.Count == 0 &&
                c.Members.Deprecated.Count == 0 && c.Members.NowExperimental.Count == 0 &&
                c.Members.NoLongerExperimental.Count == 0;
        }

        // Source-level diff: purely additive if no significant lines were deleted
        if (c.FromSource == "(could not decompile)")
        {
            return false; // Can't determine, keep it
        }

        var fromSource = ignoreDocs ? StripDocComments(c.FromSource) : c.FromSource;
        var toSource = ignoreDocs ? StripDocComments(c.ToSource) : c.ToSource;
        var fromLines = fromSource.Split('\n');
        var toLines = toSource.Split('\n');
        var edits = MyersDiff.Compute(fromLines, toLines);

        return !edits.Any(e => e.Kind == MyersDiff.EditKind.Delete && IsSignificantLine(e.Line));
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
        if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            return false;
        if (trimmed is "{" or "}" or "")
            return false;

        return true;
    }

    private static List<(string Change, string Kind, string FullName, bool Breaking)> BuildFlatRows(
        List<TypeInspector.TypeInfo> added,
        List<TypeInspector.TypeInfo> removed,
        List<ChangedType> changed)
    {
        var rows = new List<(string Change, string Kind, string FullName, bool Breaking)>();
        foreach (var t in added)
            rows.Add(("Added", t.Kind, t.FullName, false));
        foreach (var t in removed)
            rows.Add(("Removed", t.Kind, t.FullName, true));
        foreach (var c in changed)
            rows.Add(("Changed", c.Type.Kind, c.Type.FullName, c.IsBreaking));
        return rows;
    }

    private static void OutputCsv(
        List<TypeInspector.TypeInfo> added,
        List<TypeInspector.TypeInfo> removed,
        List<ChangedType> changed)
    {
        Console.WriteLine("Change,Kind,FullName,Breaking");
        foreach (var (change, kind, fullName, breaking) in BuildFlatRows(added, removed, changed))
        {
            Console.WriteLine($"{change},{CommonOptions.CsvEscape(kind)},{CommonOptions.CsvEscape(fullName)},{(breaking ? "true" : "false")}");
        }
    }

    private static void OutputTable(
        string package,
        PackageResolver.ResolvedPackage fromResolved,
        PackageResolver.ResolvedPackage toResolved,
        List<TypeInspector.TypeInfo> added,
        List<TypeInspector.TypeInfo> removed,
        List<ChangedType> changed)
    {
        Console.WriteLine($"Diff: {package} {fromResolved.Version} → {toResolved.Version}");
        Console.WriteLine();

        var rows = BuildFlatRows(added, removed, changed);
        if (rows.Count == 0)
        {
            Console.WriteLine("No public API changes detected.");
            return;
        }

        var colChange = Math.Max("Change".Length, rows.Max(r => r.Change.Length));
        var colKind = Math.Max("Kind".Length, rows.Max(r => r.Kind.Length));
        var colName = Math.Max("FullName".Length, rows.Max(r => r.FullName.Length));

        Console.WriteLine($"  {"Change".PadRight(colChange)}  {"Kind".PadRight(colKind)}  {"FullName".PadRight(colName)}  Breaking");
        Console.WriteLine($"  {new string('-', colChange)}  {new string('-', colKind)}  {new string('-', colName)}  --------");

        foreach (var (change, kind, fullName, breaking) in rows)
        {
            var breakStr = breaking ? "yes" : "";
            Console.WriteLine($"  {change.PadRight(colChange)}  {kind.PadRight(colKind)}  {fullName.PadRight(colName)}  {breakStr}");
        }
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
                        newlyDeprecated = c.NewlyDeprecated,
                        newlyExperimental = c.NewlyExperimental,
                        noLongerExperimental = c.NoLongerExperimental,
                        addedMembers = c.Members.Added.Select(m => new { m.Kind, m.Name, m.Signature }),
                        removedMembers = c.Members.Removed.Select(m => new { m.Kind, m.Name, m.Signature }),
                        changedMembers = c.Members.Changed.Select(m => new
                        {
                            kind = m.From.Kind,
                            name = m.From.Name,
                            fromSignature = m.From.Signature,
                            toSignature = m.To.Signature,
                        }),
                        deprecatedMembers = c.Members.Deprecated.Select(m => new
                        {
                            kind = m.To.Kind,
                            name = m.To.Name,
                            signature = m.To.Signature,
                            deprecationMessage = m.To.ObsoleteMessage,
                        }),
                        experimentalMembers = c.Members.NowExperimental.Select(m => new
                        {
                            kind = m.To.Kind,
                            name = m.To.Name,
                            signature = m.To.Signature,
                            experimentalId = m.To.ExperimentalId,
                        }),
                        stabilizedMembers = c.Members.NoLongerExperimental.Select(m => new
                        {
                            kind = m.To.Kind,
                            name = m.To.Name,
                            signature = m.To.Signature,
                            wasExperimentalId = m.From.ExperimentalId,
                        }),
                    }
                    : new
                    {
                        kind = c.Type.Kind,
                        name = c.Type.Name,
                        fullName = c.Type.FullName,
                        isBreaking = c.IsBreaking,
                        newlyDeprecated = c.NewlyDeprecated,
                        newlyExperimental = c.NewlyExperimental,
                        noLongerExperimental = c.NoLongerExperimental,
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
                // --type-only has no detail section, so the reason has to ride on the summary
                // line; otherwise it would repeat what the detail block prints below.
                var stability = FormatTransition(
                    c.NewlyDeprecated, c.NewlyExperimental, c.NoLongerExperimental,
                    withMessage: typeOnly);
                var deprecatedLabel = stability.Length > 0 ? $" {stability}" : "";
                Console.WriteLine($"  ~ [{c.Type.Kind}] {c.Type.FullName}{label}{deprecatedLabel}");
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

                    var typeStability = FormatTransition(
                        c.NewlyDeprecated, c.NewlyExperimental, c.NoLongerExperimental,
                        withMarker: false);
                    if (typeStability.Length > 0)
                    {
                        Console.WriteLine($"  ! type is {typeStability}");
                        Console.WriteLine();
                    }

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

    /// <summary>
    /// Renders a stability transition. Pass <paramref name="withMessage"/> false where a detail
    /// block repeats the reason anyway.
    /// </summary>
    private static string FormatTransition(
        string? newlyDeprecated,
        string? newlyExperimental,
        string? noLongerExperimental,
        bool withMessage = true,
        bool withMarker = true)
    {
        var parts = new List<string>();

        if (newlyDeprecated is not null)
        {
            parts.Add(withMessage && newlyDeprecated.Length > 0
                ? $"now deprecated: {newlyDeprecated}"
                : "now deprecated");
        }

        if (newlyExperimental is not null)
        {
            parts.Add(withMessage && newlyExperimental.Length > 0
                ? $"now experimental: {newlyExperimental}"
                : "now experimental");
        }

        if (noLongerExperimental is not null)
        {
            parts.Add(withMessage && noLongerExperimental.Length > 0
                ? $"no longer experimental (was {noLongerExperimental})"
                : "no longer experimental");
        }

        if (parts.Count == 0)
        {
            return "";
        }

        var text = string.Join("; ", parts);
        return withMarker ? $"{CommonOptions.StabilityMarker} {text}" : text;
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

        var stabilityChanged = members.Deprecated
            .Concat(members.NowExperimental)
            .Concat(members.NoLongerExperimental)
            .Distinct();

        foreach (var (from, to) in stabilityChanged)
        {
            // Report only what actually changed for this member, not its full current state.
            var marker = FormatTransition(
                from.ObsoleteMessage is null ? to.ObsoleteMessage : null,
                from.ExperimentalId is null ? to.ExperimentalId : null,
                to.ExperimentalId is null ? from.ExperimentalId : null,
                withMarker: false);

            Console.WriteLine($"  ! [{to.Kind}] {to.Signature}  // {marker}");
        }
    }

    private sealed record ChangedType(
        TypeInspector.TypeInfo Type,
        string FromSource,
        string ToSource,
        bool IsBreaking,
        MemberChanges? Members,
        string? NewlyDeprecated = null,
        string? NewlyExperimental = null,
        string? NoLongerExperimental = null);

    private sealed record MemberChanges(
        List<TypeInspector.MemberSignature> Added,
        List<TypeInspector.MemberSignature> Removed,
        List<(TypeInspector.MemberSignature From, TypeInspector.MemberSignature To)> Changed,
        List<(TypeInspector.MemberSignature From, TypeInspector.MemberSignature To)> Deprecated,
        List<(TypeInspector.MemberSignature From, TypeInspector.MemberSignature To)> NowExperimental,
        List<(TypeInspector.MemberSignature From, TypeInspector.MemberSignature To)> NoLongerExperimental);
}
