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
        var output = parseResult.GetValue(command.OutputOption);

        try
        {
            var resolved = await PackageResolver.ResolveAsync(
                package ?? "", version, framework, cancellationToken).ConfigureAwait(false);

            using var inspector = new TypeInspector(resolved.DllPath, resolved.XmlDocPath);

            // --assembly mode: show assembly-level attributes
            if (showAssembly)
            {
                var attrs = inspector.GetAssemblyAttributes();

                if (string.Equals(output, "json", StringComparison.OrdinalIgnoreCase))
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

            var source = inspector.DecompileType(typeName, publicOnly: !showAll);

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

            if (string.Equals(output, "json", StringComparison.OrdinalIgnoreCase))
            {
                var resolvedName = inspector.ResolveTypeName(typeName);
                var json = new
                {
                    package = resolved.PackageId,
                    version = resolved.Version,
                    framework = resolved.Framework,
                    typeName = resolvedName,
                    member = memberName,
                    source,
                };
                Console.WriteLine(JsonSerializer.Serialize(json, JsonOptions.Indented));
            }
            else
            {
                Console.WriteLine($"// Package: {resolved.PackageId} {resolved.Version} ({resolved.Framework})");
                Console.WriteLine();
                Console.Write(source);
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
