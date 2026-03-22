---
name: nuget-docs
description: >
  Inspect public API documentation from any NuGet package. Use when needing to understand
  a .NET library's types, methods, interfaces, or properties — for questions like "what methods
  does IChatClient have", "show me the ChatOptions class", or when implementing against a
  NuGet package API. Requires the nuget-docs dotnet global tool.
---

# NuGet Docs — Package API Inspector

Inspect any NuGet package's public API surface with full C# source and XML documentation comments.

## Prerequisites

The `nuget-docs` .NET global tool must be installed:

```bash
dotnet tool install -g nuget-docs
```

If the command fails with "not found", install it first, then retry.

## When to Use This Skill

Use `nuget-docs` when you need to:

- **Explore an unfamiliar NuGet package** — see all public types at a glance
- **Check method signatures and XML docs** — understand interface contracts before implementing
- **Find types by pattern** — search across types and members with wildcards
- **Inspect package metadata** — check dependencies, frameworks, and licensing
- **Implement against a library** — get full decompiled source with `///` doc comments
- **Compare API between versions** — identify breaking changes, new types, and modifications

## Commands

### List all public types

```bash
nuget-docs list <Package> [--version <ver>] [--framework <tfm>] [--all] [--namespace <prefix>] [--output json]
```

Shows all public types grouped by kind (Interfaces, Classes, Structs, Enums, Delegates) with one-line XML doc summaries. Use `--all` (`-a`) to include internal types. Use `--namespace` (`-n`) to filter by namespace prefix.

### Show a specific type

```bash
nuget-docs show <Package> <TypeName> [--version <ver>] [--framework <tfm>] [--all] [--member <name>] [--assembly] [--namespace <prefix>] [--output json]
```

Decompiles the full type to C# source with `///` XML documentation comments. **Short names work** — `IChatClient` automatically resolves to `Microsoft.Extensions.AI.IChatClient`. By default shows only public/protected members; use `--all` (`-a`) to include private and internal members. Use `--member` (`-m`) to show only a specific member. Use `--assembly` to show assembly-level attributes instead of a type. Use `--namespace` (`-n`) with `--assembly` to filter attributes by their type's namespace prefix.

### Search types and members

```bash
nuget-docs search <Package> <pattern> [--version <ver>] [--framework <tfm>] [--all] [--namespace <prefix>] [--output json]
```

Searches types and members using glob patterns (`*` and `?` wildcards). Results show `[Kind.MemberKind]` labels. By default searches only public/protected members; use `--all` (`-a`) to include private and internal. Use `--namespace` (`-n`) to filter by namespace prefix.

### Compare API between versions

```bash
nuget-docs diff <Package> --from <ver> --to <ver> [--framework <tfm>] [--output json]
```

Compares the public API surface between two versions of a package. Shows added, removed, and changed types with a line-by-line diff of changes. Useful for identifying breaking changes before upgrading.

### Package metadata

```bash
nuget-docs info <Package> [--version <ver>] [--output json]
```

Shows package ID, version, authors, description, license, frameworks, and dependencies.

## Efficient Usage Patterns

1. **Start broad**: `nuget-docs list <pkg>` to see all public types
2. **Narrow down**: `nuget-docs search <pkg> "Chat*"` to find types/members matching a pattern
3. **Deep dive**: `nuget-docs show <pkg> <TypeName>` for full type details
4. **Compare versions**: `nuget-docs diff <pkg> --from 1.0 --to 2.0` to see what changed

## Tips for AI Agents

- **Short names resolve automatically**: `IChatClient` → `Microsoft.Extensions.AI.IChatClient`
- **Packages auto-download**: No need to pre-install — packages are fetched from NuGet if not cached
- **Framework auto-selection**: Picks the best TFM (prefers net10.0 > net9.0 > net8.0 > netstandard2.1 > netstandard2.0)
- **Public API by default**: `list`, `show`, and `search` strip non-public items — use `--all` to see everything
- **Member focus**: Use `--member Name` with `show` to extract a single method/property (all overloads) instead of the full type
- **Namespace filter**: Use `--namespace Prefix` with `list` or `search` to filter by namespace
- **Assembly attributes**: Use `show <pkg> --assembly` to see `[assembly:]` attributes (TargetFramework, InternalsVisibleTo, etc.)
- **Assembly namespace filter**: Use `--namespace` with `show --assembly` to filter attributes by their type's namespace (e.g., `--namespace System.Runtime.Versioning`)
- **API diff**: Use `diff <pkg> --from <v1> --to <v2>` to compare public API between versions — shows added/removed/changed types
- **JSON output**: Use `--output json` (`-o json`) on any command for structured JSON output
- **Output is AI-friendly**: Plain text with `///` XML doc comments — compact and informative
- **For large packages**: Use `search` before `show` to narrow down
- **Version pinning**: Use `--version` to inspect a specific version

## Examples

### Exploring Microsoft.Extensions.AI

```bash
# See all types
nuget-docs list Microsoft.Extensions.AI.Abstractions

# Inspect IChatClient interface
nuget-docs show Microsoft.Extensions.AI.Abstractions IChatClient

# Find all Chat-related types
nuget-docs search Microsoft.Extensions.AI.Abstractions "Chat*"
```

### Checking a library before using it

```bash
# See what's in the package
nuget-docs list Humanizer

# Check metadata and dependencies
nuget-docs info Humanizer
```

### Inspecting a specific member

```bash
# Show just the GetResponseAsync method signature and docs
nuget-docs show Microsoft.Extensions.AI.Abstractions IChatClient --member GetResponseAsync

# Show all overloads of SerializeObject
nuget-docs show Newtonsoft.Json JsonConvert --member SerializeObject
```

### Filtering by namespace

```bash
# Show only types in the Linq namespace
nuget-docs list Newtonsoft.Json --namespace Newtonsoft.Json.Linq

# Search within a namespace
nuget-docs search Newtonsoft.Json "*Token*" --namespace Newtonsoft.Json.Linq
```

### Assembly-level attributes

```bash
# Check target framework, InternalsVisibleTo, etc.
nuget-docs show Newtonsoft.Json --assembly

# Filter assembly attributes by namespace
nuget-docs show Newtonsoft.Json --assembly --namespace System.Runtime.Versioning
```

### Comparing API between versions

```bash
# See what changed between versions
nuget-docs diff Microsoft.Extensions.AI.Abstractions --from 10.3.0 --to 10.4.0

# Check for breaking changes before upgrading
nuget-docs diff Newtonsoft.Json --from 13.0.3 --to 13.0.4

# Get structured diff output
nuget-docs diff Newtonsoft.Json --from 13.0.3 --to 13.0.4 --output json
```

### Version-specific inspection

```bash
# Check a specific version
nuget-docs show Microsoft.Extensions.AI.Abstractions ChatOptions --version 10.4.0
```
