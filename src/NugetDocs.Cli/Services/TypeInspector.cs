using System.Text.RegularExpressions;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace NugetDocs.Cli.Services;

/// <summary>
/// ILSpy-based type inspection and decompilation.
/// </summary>
internal sealed partial class TypeInspector : IDisposable
{
    private readonly CSharpDecompiler _decompiler;
    private readonly PEFile _peFile;

    public TypeInspector(string dllPath, string? xmlDocPath)
    {
        _peFile = new PEFile(dllPath);

        // Use default constructor — enables all modern C# features by default
        // (auto-properties, records, pattern matching, async/await, etc.)
        var settings = new DecompilerSettings
        {
            ThrowOnAssemblyResolveErrors = false,
            AlwaysQualifyMemberReferences = false,
            ShowXmlDocumentation = xmlDocPath is not null,
            FileScopedNamespaces = false, // traditional namespaces are easier to parse
        };

        _decompiler = new CSharpDecompiler(_peFile, new UniversalAssemblyResolver(
            dllPath, false, _peFile.DetectTargetFrameworkId()), settings);
    }

    /// <summary>
    /// Get types, filtered to remove compiler-generated noise.
    /// </summary>
    public IReadOnlyList<TypeInfo> GetTypes(bool publicOnly = true)
    {
        var types = new List<TypeInfo>();

        foreach (var type in _decompiler.TypeSystem.MainModule.TypeDefinitions)
        {
            if (!IsNonNoiseType(type))
            {
                continue;
            }

            if (publicOnly && type.Accessibility != Accessibility.Public &&
                type.Accessibility != Accessibility.Protected)
            {
                continue;
            }

            types.Add(new TypeInfo(
                FullName: type.FullName,
                Name: type.Name,
                Namespace: type.Namespace,
                Kind: GetTypeKind(type),
                Accessibility: type.Accessibility.ToString(),
                GenericParameterCount: type.TypeParameterCount,
                ReflectionName: type.FullTypeName.ReflectionName));
        }

        return types.OrderBy(t => t.Namespace).ThenBy(t => t.Name).ToList();
    }

    /// <summary>
    /// Decompile a specific type to C# source with XML doc comments.
    /// </summary>
    public string DecompileType(string typeName, bool publicOnly = true)
    {
        var fullName = ResolveTypeName(typeName);
        var raw = _decompiler.DecompileTypeAsString(new FullTypeName(fullName));
        return CleanDecompiledOutput(raw, publicOnly);
    }

    /// <summary>
    /// Get assembly-level attributes from the assembly.
    /// </summary>
    public IReadOnlyList<string> GetAssemblyAttributes(string? namespaceFilter = null)
    {
        var attrs = new List<string>();

        foreach (var attr in _decompiler.TypeSystem.MainModule.GetAssemblyAttributes())
        {
            var attrType = attr.AttributeType;
            var name = attrType.Name;
            var fullName = attrType.FullName;

            // Skip compiler noise attributes
            if (name is "CompilationRelaxationsAttribute" or "RuntimeCompatibilityAttribute"
                or "DebuggableAttribute" or "CompilerGeneratedAttribute"
                or "NullableContextAttribute" or "NullableAttribute"
                or "RefSafetyRulesAttribute")
            {
                continue;
            }

            // Apply namespace filter on the attribute type's namespace
            if (namespaceFilter is not null)
            {
                var attrNamespace = attrType.Namespace ?? "";
                if (!attrNamespace.StartsWith(namespaceFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            var args = attr.FixedArguments;
            var namedArgs = attr.NamedArguments;

            var parts = new List<string>();
            foreach (var arg in args)
            {
                parts.Add(FormatAttributeArg(arg.Value));
            }
            foreach (var arg in namedArgs)
            {
                parts.Add($"{arg.Name} = {FormatAttributeArg(arg.Value)}");
            }

            var shortName = name.EndsWith("Attribute", StringComparison.Ordinal)
                ? name[..^"Attribute".Length]
                : name;

            var line = parts.Count > 0
                ? $"[assembly: {shortName}({string.Join(", ", parts)})]"
                : $"[assembly: {shortName}]";

            attrs.Add(line);
        }

        return attrs;
    }

    private static string FormatAttributeArg(object? value)
    {
        return value switch
        {
            string s => $"\"{s}\"",
            bool b => b ? "true" : "false",
            null => "null",
            _ => value.ToString() ?? "",
        };
    }

    /// <summary>
    /// Extract all members matching a name from decompiled type source.
    /// Returns all matching declarations (including overloads) with their XML doc comments.
    /// </summary>
    public static string? ExtractMember(string source, string memberName)
    {
        var lines = source.Split('\n');
        var linesList = lines.ToList();
        var i = 0;
        var allMatches = new List<List<string>>();

        while (i < lines.Length)
        {
            var trimmed = lines[i].TrimStart();

            // Look for member declarations that contain the member name
            if (IsMemberDeclarationContaining(trimmed, memberName))
            {
                // Collect preceding XML doc comments and attributes
                var docLines = new List<string>();
                var lookBack = i - 1;
                while (lookBack >= 0)
                {
                    var prevTrimmed = lines[lookBack].TrimStart();
                    if (prevTrimmed.StartsWith("///", StringComparison.Ordinal) ||
                        prevTrimmed.StartsWith("[", StringComparison.Ordinal))
                    {
                        docLines.Insert(0, lines[lookBack]);
                        lookBack--;
                    }
                    else if (string.IsNullOrWhiteSpace(lines[lookBack]))
                    {
                        lookBack--;
                    }
                    else
                    {
                        break;
                    }
                }

                // Collect the member body
                var end = SkipMemberBody(linesList, i);
                var memberLines = new List<string>(docLines);
                for (var j = i; j < end; j++)
                {
                    memberLines.Add(lines[j]);
                }

                allMatches.Add(memberLines);
                i = end;
                continue;
            }

            i++;
        }

        if (allMatches.Count == 0)
        {
            return null;
        }

        return string.Join("\n\n", allMatches.Select(m => string.Join('\n', m)));
    }

    /// <summary>
    /// Check if a line declares a member with the given name.
    /// Only matches the declaration signature (before =>, {, or (), not the body.
    /// </summary>
    private static bool IsMemberDeclarationContaining(string trimmedLine, string memberName)
    {
        // Skip non-declaration lines
        if (string.IsNullOrWhiteSpace(trimmedLine) ||
            trimmedLine.StartsWith("///", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("//", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("[", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("using ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("namespace ", StringComparison.Ordinal) ||
            trimmedLine is "{" or "}" or "")
        {
            return false;
        }

        // Skip statement lines (these are inside method bodies, not declarations)
        if (trimmedLine.StartsWith("return ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("var ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("if ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("if(", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("else", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("for ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("for(", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("foreach ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("foreach(", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("while ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("while(", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("throw ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("switch ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("switch(", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("case ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("break;", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("continue;", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("yield ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("await ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("try", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("catch", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("finally", StringComparison.Ordinal))
        {
            return false;
        }

        // Extract just the declaration part (before =>, {, or ()
        // This prevents matching references in expression bodies
        var declPart = trimmedLine;
        var arrowIdx = declPart.IndexOf("=>", StringComparison.Ordinal);
        var braceIdx = declPart.IndexOf('{');
        var parenIdx = declPart.IndexOf('(');

        var cutoff = declPart.Length;
        if (arrowIdx >= 0 && arrowIdx < cutoff) cutoff = arrowIdx;
        if (braceIdx >= 0 && braceIdx < cutoff) cutoff = braceIdx;
        if (parenIdx >= 0 && parenIdx < cutoff) cutoff = parenIdx;

        declPart = declPart[..cutoff];

        // Check for the member name as a word boundary match in the declaration part
        var nameIndex = declPart.IndexOf(memberName, StringComparison.OrdinalIgnoreCase);
        if (nameIndex < 0)
        {
            return false;
        }

        // Verify it's a word boundary (not a substring of a longer name)
        var afterName = nameIndex + memberName.Length;
        if (afterName < declPart.Length)
        {
            var nextChar = declPart[afterName];
            if (char.IsLetterOrDigit(nextChar) || nextChar == '_')
            {
                return false;
            }
        }

        if (nameIndex > 0)
        {
            var prevChar = declPart[nameIndex - 1];
            if (char.IsLetterOrDigit(prevChar) || prevChar == '_')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Search types and members by pattern (glob-like: * matches any).
    /// </summary>
    public IReadOnlyList<SearchResult> SearchTypes(string pattern, bool publicOnly = true)
    {
        var results = new List<SearchResult>();
        var regex = GlobToRegex(pattern);

        foreach (var type in _decompiler.TypeSystem.MainModule.TypeDefinitions)
        {
            if (!IsPublicApiType(type))
            {
                continue;
            }

            // Match type name
            if (regex.IsMatch(type.Name) || regex.IsMatch(type.FullName))
            {
                results.Add(new SearchResult(
                    Kind: GetTypeKind(type),
                    FullName: type.FullName,
                    Name: type.Name,
                    MemberKind: null));
            }

            // Search members
            foreach (var member in type.Members)
            {
                if (publicOnly && member.Accessibility != Accessibility.Public &&
                    member.Accessibility != Accessibility.Protected)
                {
                    continue;
                }

                if (regex.IsMatch(member.Name))
                {
                    results.Add(new SearchResult(
                        Kind: GetTypeKind(type),
                        FullName: $"{type.FullName}.{member.Name}",
                        Name: member.Name,
                        MemberKind: GetMemberKind(member)));
                }
            }
        }

        return results.OrderBy(r => r.FullName).ToList();
    }

    /// <summary>
    /// Resolve a short type name to its full name.
    /// </summary>
    public string ResolveTypeName(string typeName)
    {
        // If it contains '+' it's already a reflection name for nested types — use as-is
        if (typeName.Contains('+'))
        {
            return typeName;
        }

        // If it looks like a full name already (with or without backtick arity)
        if (typeName.Contains('.'))
        {
            // If it already has backtick notation, use as-is
            if (typeName.Contains('`'))
            {
                return typeName;
            }

            // Try to find exact match — return ReflectionName to handle nested types
            var exactMatch = _decompiler.TypeSystem.MainModule.TypeDefinitions
                .FirstOrDefault(t => string.Equals(t.FullName, typeName, StringComparison.OrdinalIgnoreCase));

            if (exactMatch is not null)
            {
                return exactMatch.FullTypeName.ReflectionName;
            }

            // Try suffix match for short nested names like "ChatRole.Converter"
            var suffixMatches = _decompiler.TypeSystem.MainModule.TypeDefinitions
                .Where(t => IsPublicApiType(t) &&
                    t.FullName.EndsWith("." + typeName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (suffixMatches.Count == 1)
            {
                return suffixMatches[0].FullTypeName.ReflectionName;
            }

            if (suffixMatches.Count > 1)
            {
                var suffixCandidates = suffixMatches.Select(m => $"  {m.FullName}");
                throw new InvalidOperationException(
                    $"Ambiguous type name '{typeName}'. Candidates:\n" +
                    string.Join("\n", suffixCandidates));
            }

            return typeName;
        }

        var matches = _decompiler.TypeSystem.MainModule.TypeDefinitions
            .Where(t => IsPublicApiType(t) &&
                string.Equals(t.Name, typeName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            throw new InvalidOperationException(
                $"Type '{typeName}' not found in this package.");
        }

        if (matches.Count == 1)
        {
            return matches[0].FullTypeName.ReflectionName;
        }

        // For generic arity variants (e.g., IEmbeddingGenerator and IEmbeddingGenerator<,>),
        // prefer the most-generic version (highest type parameter count)
        var allSameBaseName = matches.All(m =>
            string.Equals(m.Name, matches[0].Name, StringComparison.OrdinalIgnoreCase));

        if (allSameBaseName)
        {
            return matches.OrderByDescending(m => m.TypeParameterCount).First().FullTypeName.ReflectionName;
        }

        // Truly ambiguous — show candidates with arity info
        var candidates = matches.Select(m =>
        {
            var arity = m.TypeParameterCount > 0
                ? $"<{new string(',', m.TypeParameterCount - 1)}>"
                : "";
            return $"  {m.FullName}{arity}";
        });

        throw new InvalidOperationException(
            $"Ambiguous type name '{typeName}'. Candidates:\n" +
            string.Join("\n", candidates));
    }

    /// <summary>
    /// Clean decompiled output by removing compiler-generated noise.
    /// </summary>
    private static string CleanDecompiledOutput(string source, bool publicOnly)
    {
        // Remove lines containing compiler-generated attributes
        // These are IL-level artifacts that add noise without value
        var lines = source.Split('\n');
        var cleanedLines = new List<string>(lines.Length);

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();

            // Skip lines that are purely compiler-generated attributes
            if (IsNoiseAttributeLine(trimmed))
            {
                continue;
            }

            // Skip using statements for compiler-infrastructure namespaces
            if (IsNoiseUsingLine(trimmed))
            {
                continue;
            }

            // Clean inline attribute noise from remaining lines
            var cleaned = CleanInlineAttributes(line);
            cleanedLines.Add(cleaned);
        }

        // Strip non-public members if requested
        if (publicOnly)
        {
            cleanedLines = StripNonPublicMembers(cleanedLines);
        }

        // Remove consecutive blank lines (collapsing gaps from removed lines)
        var result = new List<string>(cleanedLines.Count);
        var lastWasBlank = false;

        foreach (var line in cleanedLines)
        {
            var isBlank = string.IsNullOrWhiteSpace(line);

            if (isBlank && lastWasBlank)
            {
                continue;
            }

            result.Add(line);
            lastWasBlank = isBlank;
        }

        return string.Join('\n', result);
    }

    /// <summary>
    /// Remove private, internal, and protected internal members from decompiled output.
    /// Works by detecting member declaration lines and skipping them plus their bodies.
    /// </summary>
    private static List<string> StripNonPublicMembers(List<string> lines)
    {
        var result = new List<string>(lines.Count);
        var i = 0;

        while (i < lines.Count)
        {
            var trimmed = lines[i].TrimStart();

            // Check if this line (or a preceding XML doc comment block) starts a non-public member
            if (IsNonPublicMemberDeclaration(trimmed))
            {
                // Look back and remove any preceding XML doc comments and attributes for this member
                while (result.Count > 0)
                {
                    var lastTrimmed = result[^1].TrimStart();
                    if (lastTrimmed.StartsWith("///", StringComparison.Ordinal) ||
                        lastTrimmed.StartsWith("[", StringComparison.Ordinal) ||
                        string.IsNullOrWhiteSpace(result[^1]))
                    {
                        result.RemoveAt(result.Count - 1);
                    }
                    else
                    {
                        break;
                    }
                }

                // Skip this line and its body (brace-matched)
                i = SkipMemberBody(lines, i);
                continue;
            }

            result.Add(lines[i]);
            i++;
        }

        return result;
    }

    /// <summary>
    /// Returns true if the trimmed line declares a private/internal member.
    /// </summary>
    private static bool IsNonPublicMemberDeclaration(string trimmedLine)
    {
        // Skip blank lines, comments, attributes, namespace/using, opening/closing braces
        if (string.IsNullOrWhiteSpace(trimmedLine) ||
            trimmedLine.StartsWith("///", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("//", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("[", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("using ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("namespace ", StringComparison.Ordinal) ||
            trimmedLine is "{" or "}" or "")
        {
            return false;
        }

        // Detect access modifiers at the start of member declarations
        // private, internal, private protected — strip these
        // public, protected — keep these
        if (trimmedLine.StartsWith("private ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("internal ", StringComparison.Ordinal))
        {
            return true;
        }

        // Private fields: lines like "private readonly string _foo;" or just field with no modifier
        // inside a class body (indented with tab)
        // Note: members with no access modifier default to private in C#

        return false;
    }

    /// <summary>
    /// Skip from current line past the member body (brace-matched).
    /// Returns the index of the next line after the member.
    /// </summary>
    private static int SkipMemberBody(List<string> lines, int startIndex)
    {
        var i = startIndex;
        var braceDepth = 0;
        var foundBrace = false;

        while (i < lines.Count)
        {
            var line = lines[i];

            foreach (var ch in line)
            {
                if (ch == '{')
                {
                    braceDepth++;
                    foundBrace = true;
                }
                else if (ch == '}')
                {
                    braceDepth--;
                }
            }

            i++;

            // If line ends with ; and no braces opened, it's a single-line member (field, etc.)
            if (!foundBrace && line.TrimEnd().EndsWith(';'))
            {
                return i;
            }

            // If we opened and closed all braces, we're done with the body
            if (foundBrace && braceDepth <= 0)
            {
                return i;
            }
        }

        return i;
    }

    /// <summary>
    /// Returns true if the line is a standalone noise attribute that should be removed entirely.
    /// </summary>
    private static bool IsNoiseAttributeLine(string trimmedLine)
    {
        // [NullableContext(N)]
        if (trimmedLine.StartsWith("[NullableContext(", StringComparison.Ordinal))
        {
            return true;
        }

        // [Nullable(N)] or [Nullable(new byte[] { ... })]
        if (trimmedLine.StartsWith("[Nullable(", StringComparison.Ordinal) &&
            !trimmedLine.StartsWith("[NullablePublic", StringComparison.Ordinal))
        {
            return true;
        }

        // [IsReadOnly] — emitted for readonly structs (C# already says "readonly struct")
        if (trimmedLine is "[IsReadOnly]")
        {
            return true;
        }

        // [CompilerGenerated] — on backing fields, accessors
        if (trimmedLine is "[CompilerGenerated]")
        {
            return true;
        }

        // [DebuggerDisplay(...)] / [DebuggerBrowsable(...)] — debugger hints
        if (trimmedLine.StartsWith("[DebuggerDisplay(", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("[DebuggerBrowsable(", StringComparison.Ordinal))
        {
            return true;
        }

        // [Extension] — emitted on static classes containing extension methods
        if (trimmedLine is "[Extension]")
        {
            return true;
        }

        // [StructLayout(...)] with Sequential — default for structs, noise
        if (trimmedLine.StartsWith("[StructLayout(", StringComparison.Ordinal) &&
            trimmedLine.Contains("LayoutKind.Sequential", StringComparison.Ordinal))
        {
            return true;
        }

        // [DefaultMember("Item")] — emitted for indexers (already implied by this[] syntax)
        if (trimmedLine.StartsWith("[DefaultMember(", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns true if the line is a using statement for compiler-infrastructure namespaces.
    /// </summary>
    private static bool IsNoiseUsingLine(string trimmedLine)
    {
        if (!trimmedLine.StartsWith("using ", StringComparison.Ordinal))
        {
            return false;
        }

        return trimmedLine is "using System.Runtime.CompilerServices;"
            or "using System.Diagnostics;"
            or "using System.Diagnostics.CodeAnalysis;"
            or "using System.Runtime.InteropServices;"
            or "using System.ComponentModel;"
            or "using System.Runtime.Versioning;";
    }

    /// <summary>
    /// Clean inline attribute noise from parameter/return/property declarations.
    /// </summary>
    private static string CleanInlineAttributes(string line)
    {
        // Remove [Nullable(N)] and [Nullable(new byte[] {...})] inline
        var cleaned = NullableInlineRegex().Replace(line, "");

        // Remove [NullableContext(N)] inline
        cleaned = NullableContextInlineRegex().Replace(cleaned, "");

        // Remove [param: Nullable(...)] / [return: Nullable(...)] / [param: AllowNull]
        cleaned = ParamReturnNullableRegex().Replace(cleaned, "");

        // Remove [CompilerGenerated] inline (on getters/setters)
        cleaned = CompilerGeneratedInlineRegex().Replace(cleaned, "");

        // Remove [DebuggerBrowsable(...)] inline
        cleaned = DebuggerBrowsableInlineRegex().Replace(cleaned, "");

        // Remove [MaybeNull] / [NotNull] / [NotNullWhen(...)] / [MaybeNullWhen(...)] / [AllowNull] / [DisallowNull]
        // These are nullability contract attributes — already expressed via ? syntax
        cleaned = NullabilityContractInlineRegex().Replace(cleaned, "");

        // Replace <PropertyName>k__BackingField with PropertyName
        // These appear when ILSpy can't fully reconstruct auto-properties
        cleaned = BackingFieldRegex().Replace(cleaned, "$1");

        // Replace base..ctor() and this..ctor() with base() and this()
        cleaned = cleaned
            .Replace("base..ctor()", "base()", StringComparison.Ordinal)
            .Replace("this..ctor(", "this(", StringComparison.Ordinal);

        // Clean up any double spaces left behind
        cleaned = MultiSpaceRegex().Replace(cleaned, " ");

        // Clean up empty attribute brackets [] that might be left
        cleaned = cleaned.Replace("[] ", "", StringComparison.Ordinal);

        return cleaned;
    }

    // Regex patterns for inline attribute removal

    [GeneratedRegex(@"\[Nullable\([^)]*\)\]\s*")]
    private static partial Regex NullableInlineRegex();

    [GeneratedRegex(@"\[NullableContext\(\d+\)\]\s*")]
    private static partial Regex NullableContextInlineRegex();

    [GeneratedRegex(@"\[(param|return|field):\s*(Nullable\([^)]*\)|AllowNull|MaybeNull|NotNull)\]\s*")]
    private static partial Regex ParamReturnNullableRegex();

    [GeneratedRegex(@"\[CompilerGenerated\]\s*")]
    private static partial Regex CompilerGeneratedInlineRegex();

    [GeneratedRegex(@"\[DebuggerBrowsable\([^)]*\)\]\s*")]
    private static partial Regex DebuggerBrowsableInlineRegex();

    [GeneratedRegex(@"\[(MaybeNull|NotNull|NotNullWhen\([^)]*\)|MaybeNullWhen\([^)]*\)|AllowNull|DisallowNull)\]\s*")]
    private static partial Regex NullabilityContractInlineRegex();

    [GeneratedRegex(@"<(\w+)>k__BackingField")]
    private static partial Regex BackingFieldRegex();

    [GeneratedRegex(@"  +")]
    private static partial Regex MultiSpaceRegex();

    private static bool IsPublicApiType(ITypeDefinition type)
    {
        if (type.Accessibility != Accessibility.Public &&
            type.Accessibility != Accessibility.Protected)
        {
            return false;
        }

        return IsNonNoiseType(type);
    }

    private static bool IsNonNoiseType(ITypeDefinition type)
    {
        var name = type.Name;
        var fullName = type.FullName;

        // Filter compiler-generated
        if (name.Contains('<') || name.Contains('>') ||
            name.StartsWith("__", StringComparison.Ordinal) ||
            name == "<Module>" ||
            name == "<PrivateImplementationDetails>")
        {
            return false;
        }

        // Filter internal infrastructure namespaces
        if (fullName.StartsWith("Microsoft.Shared.", StringComparison.Ordinal) ||
            fullName.StartsWith("System.Text.RegularExpressions.Generated.", StringComparison.Ordinal) ||
            fullName.Contains(".DisplayClass") ||
            fullName.Contains(".DebugView"))
        {
            return false;
        }

        // Filter nested compiler-generated types
        if (type.DeclaringType is not null &&
            !IsNonNoiseType(type.DeclaringType.GetDefinition()!))
        {
            return false;
        }

        return true;
    }

    private static string GetTypeKind(ITypeDefinition type)
    {
        if (type.Kind == TypeKind.Interface)
        {
            return "Interface";
        }

        if (type.Kind == TypeKind.Enum)
        {
            return "Enum";
        }

        if (type.Kind == TypeKind.Delegate)
        {
            return "Delegate";
        }

        if (type.Kind == TypeKind.Struct)
        {
            return "Struct";
        }

        return "Class";
    }

    private static string? GetMemberKind(IMember member)
    {
        return member switch
        {
            IMethod m when m.IsConstructor => "Constructor",
            IMethod => "Method",
            IProperty => "Property",
            IField => "Field",
            IEvent => "Event",
            _ => null,
        };
    }

    private static Regex GlobToRegex(string pattern)
    {
        var regexPattern = "^" +
            Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") +
            "$";

        return new Regex(regexPattern, RegexOptions.IgnoreCase);
    }

    public void Dispose()
    {
        _peFile.Dispose();
    }

    public record TypeInfo(
        string FullName,
        string Name,
        string Namespace,
        string Kind,
        string Accessibility,
        int GenericParameterCount,
        string ReflectionName);

    public record SearchResult(
        string Kind,
        string FullName,
        string Name,
        string? MemberKind);

    /// <summary>
    /// Get public member signatures for a type, for member-level diffing.
    /// </summary>
    public IReadOnlyList<MemberSignature> GetMemberSignatures(string typeName)
    {
        var fullName = ResolveTypeName(typeName);
        var ftName = new FullTypeName(fullName);
        var type = _decompiler.TypeSystem.MainModule.TypeDefinitions
            .FirstOrDefault(t => t.FullTypeName == ftName);

        if (type is null)
        {
            return [];
        }

        var members = new List<MemberSignature>();

        foreach (var member in type.Members)
        {
            if (member.Accessibility != Accessibility.Public &&
                member.Accessibility != Accessibility.Protected)
            {
                continue;
            }

            var kind = GetMemberKind(member) ?? "Unknown";
            var signature = FormatMemberSignature(member);

            members.Add(new MemberSignature(
                Name: member.Name,
                Kind: kind,
                Signature: signature));
        }

        return members.OrderBy(m => m.Kind).ThenBy(m => m.Name).ToList();
    }

    private static string FormatMemberSignature(IMember member)
    {
        var access = member.Accessibility.ToString().ToLowerInvariant();
        return member switch
        {
            IMethod m when m.IsConstructor =>
                $"{access} {m.DeclaringType.Name}({FormatParameters(m.Parameters)})",
            IMethod m =>
                $"{access} {FormatType(m.ReturnType)} {m.Name}{FormatTypeParams(m.TypeParameters)}({FormatParameters(m.Parameters)})",
            IProperty p when p.IsIndexer =>
                $"{access} {FormatType(p.ReturnType)} this[{FormatParameters(p.Parameters)}] {{ {PropertyAccessors(p)} }}",
            IProperty p =>
                $"{access} {FormatType(p.ReturnType)} {p.Name} {{ {PropertyAccessors(p)} }}",
            IField f =>
                $"{access} {FormatType(f.ReturnType)} {f.Name}",
            IEvent e =>
                $"{access} event {FormatType(e.ReturnType)} {e.Name}",
            _ => member.Name,
        };
    }

    private static string FormatType(IType type)
    {
        // Handle parameterized generic types (Nullable<T>, List<T>, etc.)
        if (type is ICSharpCode.Decompiler.TypeSystem.ParameterizedType pt)
        {
            var genericDef = pt.GenericType.ReflectionName;

            // Nullable<T> → T?
            if (genericDef == "System.Nullable`1" && pt.TypeArguments.Count == 1)
            {
                return $"{FormatType(pt.TypeArguments[0])}?";
            }

            // Other generics: format as Name<T1, T2>
            var baseName = FormatSimpleTypeName(genericDef);
            // Strip the backtick arity suffix (e.g., "List`1" → "List")
            var backtickIdx = baseName.IndexOf('`');
            if (backtickIdx >= 0)
            {
                baseName = baseName[..backtickIdx];
            }
            var args = string.Join(", ", pt.TypeArguments.Select(FormatType));
            return $"{baseName}<{args}>";
        }

        return FormatSimpleTypeName(type.ReflectionName);
    }

    private static string FormatSimpleTypeName(string name)
    {
        return name switch
        {
            "System.Void" => "void",
            "System.String" => "string",
            "System.Boolean" => "bool",
            "System.Int32" => "int",
            "System.Int64" => "long",
            "System.Double" => "double",
            "System.Single" => "float",
            "System.Decimal" => "decimal",
            "System.Object" => "object",
            "System.Byte" => "byte",
            "System.Char" => "char",
            "System.Int16" => "short",
            "System.UInt16" => "ushort",
            "System.UInt32" => "uint",
            "System.UInt64" => "ulong",
            "System.SByte" => "sbyte",
            "System.IntPtr" => "nint",
            "System.UIntPtr" => "nuint",
            _ => name,
        };
    }

    private static string FormatParameters(IReadOnlyList<IParameter> parameters)
    {
        return string.Join(", ", parameters.Select(p => $"{FormatType(p.Type)} {p.Name}"));
    }

    private static string FormatTypeParams(IReadOnlyList<ITypeParameter> typeParams)
    {
        if (typeParams.Count == 0) return "";
        return $"<{string.Join(", ", typeParams.Select(tp => tp.Name))}>";
    }

    private static string PropertyAccessors(IProperty p)
    {
        var parts = new List<string>();
        if (p.CanGet) parts.Add("get;");
        if (p.CanSet) parts.Add("set;");
        return string.Join(" ", parts);
    }

    public record MemberSignature(
        string Name,
        string Kind,
        string Signature);
}
