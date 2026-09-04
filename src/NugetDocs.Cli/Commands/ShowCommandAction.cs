using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using NugetDocs.Cli.Services;

namespace NugetDocs.Cli.Commands;

internal sealed class ShowCommandAction(ShowCommand command) : AsynchronousCommandLineAction
{
    public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        var package = parseResult.GetValue(command.PackageArgument);
        var typeName = parseResult.GetValue(command.TypeArgument);
        var version = parseResult.GetValue(command.VersionOption);
        var framework = parseResult.GetValue(command.FrameworkOption);
        var showAll = parseResult.GetValue(command.AllOption);
        var memberName = parseResult.GetValue(command.MemberOption);
        var showAssembly = parseResult.GetValue(command.AssemblyOption);
        var namespaceFilter = parseResult.GetValue(command.NamespaceOption);
        var signaturesOnly = parseResult.GetValue(command.SignaturesOption);
        var maxLines = parseResult.GetValue(command.MaxLinesOption);
        var jsonOutput = CommonOptions.IsJsonOutput(parseResult, command.OutputOption, command.JsonOption);

        try
        {
            var resolved = await PackageResolver.ResolveAsync(
                package ?? "", version, framework, cancellationToken).ConfigureAwait(false);

            using var inspector = new TypeInspector(resolved.DllPath!, resolved.XmlDocPath);

            // --assembly mode: show assembly-level attributes
            if (showAssembly)
            {
                var attrs = inspector.GetAssemblyAttributes(namespaceFilter);

                if (jsonOutput)
                {
                    var json = new
                    {
                        package = resolved.PackageId,
                        version = resolved.Version,
                        framework = resolved.Framework,
                        assemblyAttributes = attrs,
                    };
                    Console.WriteLine(JsonSerializer.Serialize(json, JsonOptions.Indented));
                }
                else
                {
                    Console.WriteLine($"// Package: {resolved.PackageId} {resolved.Version} ({resolved.Framework})");
                    Console.WriteLine($"// Assembly attributes:");
                    Console.WriteLine();
                    foreach (var attr in attrs)
                    {
                        Console.WriteLine(attr);
                    }
                }

                return 0;
            }

            // Normal mode: decompile a type
            if (string.IsNullOrEmpty(typeName))
            {
                Console.Error.WriteLine("Error: Type name is required (or use --assembly).");
                return 1;
            }

            string source;

            if (signaturesOnly)
            {
                // Signature overview: no bodies, so a huge type stays a handful of lines.
                var members = inspector.GetMemberSignatures(typeName);

                if (memberName is not null)
                {
                    members = members
                        .Where(m => string.Equals(m.Name, memberName, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (members.Count == 0)
                    {
                        Console.Error.WriteLine($"Error: Member '{memberName}' not found in type.");
                        return 1;
                    }
                }

                source = FormatSignatures(
                    inspector.ResolveTypeName(typeName),
                    members,
                    inspector.GetTypeObsoleteMessage(typeName));
            }
            else
            {
                source = inspector.DecompileType(typeName, publicOnly: !showAll);

                // If --member is specified, extract just that member
                if (memberName is not null)
                {
                    var memberSource = TypeInspector.ExtractMember(source, memberName);
                    if (memberSource is null)
                    {
                        Console.Error.WriteLine($"Error: Member '{memberName}' not found in type.");
                        return 1;
                    }

                    source = memberSource;
                }
            }

            source = CommonOptions.ApplyLineLimit(source, maxLines, out var totalLines);
            var truncated = maxLines > 0 && totalLines > maxLines;

            if (jsonOutput)
            {
                var resolvedName = inspector.ResolveTypeName(typeName);
                var json = new
                {
                    package = resolved.PackageId,
                    version = resolved.Version,
                    framework = resolved.Framework,
                    typeName = resolvedName,
                    member = memberName,
                    signaturesOnly,
                    totalLines,
                    truncated,
                    source,
                };
                Console.WriteLine(JsonSerializer.Serialize(json, JsonOptions.Indented));
            }
            else
            {
                Console.WriteLine($"// Package: {resolved.PackageId} {resolved.Version} ({resolved.Framework})");
                Console.WriteLine();
                Console.Write(source);

                if (truncated)
                {
                    // Comment syntax: the output is C#, and a truncated type no longer parses.
                    var hint = signaturesOnly
                        ? "use --max-lines 0 for all members, or --member <name> for one"
                        : "use --max-lines 0 for the full source, --signatures for an overview, or --member <name> for one member";
                    Console.WriteLine();
                    Console.WriteLine($"// ... and {totalLines - maxLines} more lines ({hint})");
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

    /// <summary>
    /// Renders member signatures grouped by kind, mirroring the layout <c>list</c> uses for types.
    /// </summary>
    private static string FormatSignatures(
        string typeName,
        IReadOnlyList<TypeInspector.MemberSignature> members,
        string? typeObsoleteMessage)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append("// Type: ").Append(typeName).AppendLine();

        // A type-level [Obsolete] does not repeat on its members, so surface it in the header.
        if (typeObsoleteMessage is not null)
        {
            builder.Append("// ** deprecated");
            if (typeObsoleteMessage.Length > 0)
            {
                builder.Append(": ").Append(typeObsoleteMessage);
            }
            builder.AppendLine();
        }

        builder.Append("// Members: ").Append(members.Count).AppendLine();
        builder.AppendLine();

        CommonOptions.WriteGroupedByKind(
            members,
            kind: m => m.Kind,
            // The deprecation marker is the highest-value signal in a survey, so it rides inline
            // rather than being dropped with the rest of the attributes.
            line: m => m.ObsoleteMessage is null
                ? $"{m.Signature};"
                : $"{m.Signature};  // ** deprecated{(m.ObsoleteMessage.Length > 0 ? $": {m.ObsoleteMessage}" : "")}",
            write: line => builder.AppendLine(line));

        return builder.ToString();
    }
}
