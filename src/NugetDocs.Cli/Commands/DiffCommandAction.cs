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
            var changed = new List<(TypeInspector.TypeInfo Type, string FromSource, string ToSource)>();

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
                        // Use reflection name with backtick for generic types
                        var reflectionName = fromType!.GenericParameterCount > 0
                            ? $"{fromType.FullName}`{fromType.GenericParameterCount}"
                            : fromType.FullName;
                        var fromSource = fromInspector.DecompileType(reflectionName);
                        var toSource = toInspector.DecompileType(reflectionName);

                        if (!string.Equals(fromSource, toSource, StringComparison.Ordinal))
                        {
                            changed.Add((toType!, fromSource, toSource));
                        }
                    }
                    catch
                    {
                        // Some nested/complex types can't be decompiled individually —
                        // mark as changed without source diff
                        changed.Add((toType!, "(could not decompile)", "(could not decompile)"));
                    }
                }
            }

            if (string.Equals(output, "json", StringComparison.OrdinalIgnoreCase))
            {
                var json = new
                {
                    package,
                    from = new { version = fromResolved.Version, framework = fromResolved.Framework },
                    to = new { version = toResolved.Version, framework = toResolved.Framework },
                    summary = new
                    {
                        addedCount = added.Count,
                        removedCount = removed.Count,
                        changedCount = changed.Count,
                    },
                    added = added.Select(t => new { kind = t.Kind, name = t.Name, fullName = t.FullName }),
                    removed = removed.Select(t => new { kind = t.Kind, name = t.Name, fullName = t.FullName }),
                    changed = typeOnly
                        ? null
                        : changed.Select(c => new
                        {
                            kind = c.Type.Kind,
                            name = c.Type.Name,
                            fullName = c.Type.FullName,
                            fromSource = c.FromSource,
                            toSource = c.ToSource,
                        }),
                };
                Console.WriteLine(JsonSerializer.Serialize(json, JsonOptions.Indented));
            }
            else
            {
                Console.WriteLine($"// Diff: {package} {fromResolved.Version} → {toResolved.Version}");
                Console.WriteLine($"// Framework: {fromResolved.Framework} → {toResolved.Framework}");
                Console.WriteLine();

                if (added.Count == 0 && removed.Count == 0 && changed.Count == 0)
                {
                    Console.WriteLine("No public API changes detected.");
                    return 0;
                }

                Console.WriteLine($"Summary: +{added.Count} added, -{removed.Count} removed, ~{changed.Count} changed");
                Console.WriteLine();

                if (added.Count > 0)
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
                    Console.WriteLine("Removed:");
                    foreach (var type in removed)
                    {
                        Console.WriteLine($"  - [{type.Kind}] {type.FullName}");
                    }
                    Console.WriteLine();
                }

                if (changed.Count > 0)
                {
                    Console.WriteLine("Changed:");
                    foreach (var (type, _, _) in changed)
                    {
                        Console.WriteLine($"  ~ [{type.Kind}] {type.FullName}");
                    }
                    Console.WriteLine();

                    // Show detailed diffs (skip if --type-only)
                    if (!typeOnly)
                    {
                        Console.WriteLine("--- Detailed changes ---");
                        Console.WriteLine();

                        foreach (var (type, fromSource, toSource) in changed)
                        {
                            Console.WriteLine($"=== {type.FullName} ===");
                            Console.WriteLine();

                            if (fromSource == "(could not decompile)")
                            {
                                Console.WriteLine("  (could not decompile for comparison)");
                            }
                            else
                            {
                                // Myers diff for proper ordered output
                                var fromLines = fromSource.Split('\n');
                                var toLines = toSource.Split('\n');
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

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
