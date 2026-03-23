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
nuget-docs list <Package> [--version <ver>] [--framework <tfm>] [--all] [--namespace <prefix>] [--json] [--output json]
```

Shows all public types grouped by kind (Interfaces, Classes, Structs, Enums, Delegates) with one-line XML doc summaries. Use `--all` (`-a`) to include internal types. Use `--namespace` (`-n`) to filter by namespace prefix.

### Show a specific type

```bash
nuget-docs show <Package> <TypeName> [--version <ver>] [--framework <tfm>] [--all] [--member <name>] [--assembly] [--namespace <prefix>] [--json] [--output json]
```

Decompiles the full type to C# source with `///` XML documentation comments. **Short names work** — `IChatClient` automatically resolves to `Microsoft.Extensions.AI.IChatClient`. By default shows only public/protected members; use `--all` (`-a`) to include private and internal members. Use `--member` (`-m`) to show only a specific member. Use `--assembly` to show assembly-level attributes instead of a type. Use `--namespace` (`-n`) with `--assembly` to filter attributes by their type's namespace prefix.

### Search types and members

```bash
nuget-docs search <Package> <pattern> [--version <ver>] [--framework <tfm>] [--all] [--namespace <prefix>] [--json] [--output json]
```

Searches types and members using glob patterns (`*` and `?` wildcards). Results show `[Kind.MemberKind]` labels. By default searches only public/protected members; use `--all` (`-a`) to include private and internal. Use `--namespace` (`-n`) to filter by namespace prefix.

### Compare API between versions

```bash
nuget-docs diff <Package> --from <ver> --to <ver> [--framework <tfm>] [--type-only] [--breaking] [--member-diff] [--no-additive] [--ignore-docs] [--json] [--output json]
```

Compares the public API surface between two versions of a package. Use `latest` as a version value to auto-resolve the latest stable version from NuGet.org (e.g., `--to latest`). Shows added, removed, and changed types with a unified diff (Myers algorithm) including `@@ -line,count +line,count @@` hunk headers. Use `--type-only` (`-t`) for a quick summary without decompiling. Use `--breaking` (`-b`) to show only potentially breaking changes. Use `--member-diff` (`-m`) to show structured member-level changes (added/removed/changed methods/properties) instead of full source diff. Use `--no-additive` to hide purely additive changes and show only removals/modifications for upgrade safety checks (also available as `--include-additive false`). Use `--ignore-docs` to ignore XML doc comment changes in source-level diff. Works with both source-level and member-level diffs. **Exit codes**: 0 = no breaking changes, 1 = error, 2 = breaking changes detected (useful for CI).

### Package metadata

```bash
nuget-docs info <Package> [--version <ver>] [--json] [--output json]
```

Shows package ID, version, authors, description, license, frameworks, and dependencies.

### Dependency tree

```bash
nuget-docs deps <Package> [--version <ver>] [--framework <tfm>] [--depth <n>] [--json] [--output json]
```

Shows the dependency tree of a package with tree-style output. Use `--depth` (`-d`) to control transitive resolution depth (default: 1 = direct only). Use `--depth 2` or higher for transitive dependencies. Shared dependencies are marked with `(already listed)` to avoid confusion.

### List available versions

```bash
nuget-docs versions <Package> [--stable] [--prerelease] [--latest] [--since <ver>] [--count] [--limit <n>] [--json] [--output json]
```

Lists all available versions of a package from NuGet.org, newest first. Use `--stable` (`-s`) to show only stable versions. Use `--prerelease` (`-p`) to show only prerelease versions. Use `--latest` to show only the latest stable and latest prerelease versions. Use `--since` to show only versions newer than the specified version. Use `--count` (`-c`) to output only the count of matching versions (useful for CI). Use `--limit` (`-l`) to control how many to show (default: 20, 0 = all).

## Efficient Usage Patterns

1. **Start broad**: `nuget-docs list <pkg>` to see all public types
2. **Narrow down**: `nuget-docs search <pkg> "Chat*"` to find types/members matching a pattern
3. **Deep dive**: `nuget-docs show <pkg> <TypeName>` for full type details
4. **Compare versions**: `nuget-docs diff <pkg> --from 1.0 --to 2.0` to see what changed
5. **Check dependencies**: `nuget-docs deps <pkg>` to see what a package depends on
6. **Find versions**: `nuget-docs versions <pkg> --stable` to pick a version for `diff` or `show`

## Tips for AI Agents

- **Short names resolve automatically**: `IChatClient` → `Microsoft.Extensions.AI.IChatClient`, `ChatRole.Converter` → nested type
- **Packages auto-download**: No need to pre-install — packages are fetched from NuGet if not cached
- **Framework auto-selection**: Picks the best TFM (prefers net10.0 > net9.0 > net8.0 > netstandard2.1 > netstandard2.0)
- **Public API by default**: `list`, `show`, and `search` strip non-public items — use `--all` to see everything
- **Member focus**: Use `--member Name` with `show` to extract a single method/property (all overloads) instead of the full type
- **Namespace filter**: Use `--namespace Prefix` with `list` or `search` to filter by namespace
- **Assembly attributes**: Use `show <pkg> --assembly` to see `[assembly:]` attributes (TargetFramework, InternalsVisibleTo, etc.)
- **Assembly namespace filter**: Use `--namespace` with `show --assembly` to filter attributes by their type's namespace (e.g., `--namespace System.Runtime.Versioning`)
- **API diff**: Use `diff <pkg> --from <v1> --to <v2>` to compare public API between versions — shows added/removed/changed types with unified diff. Supports `latest`, `latest-stable`, `latest-prerelease` keywords (e.g., `--from latest-stable --to latest-prerelease`)
- **Quick diff**: Use `--type-only` (`-t`) with `diff` for a fast summary without decompiling — shows only added/removed type names
- **Breaking changes**: Use `--breaking` (`-b`) with `diff` to filter to only breaking changes (removed types, member removals/signature changes)
- **Member-level diff**: Use `--member-diff` (`-m`) with `diff` for structured member changes (added/removed/changed methods/properties) instead of full source diff — formats `Nullable<T>` as `T?` and resolves generic type arguments cleanly
- **Skip additive changes**: Use `--no-additive` with `diff` to hide purely additive changes (new types, additive-only type modifications) and show only removals/modifications — works with both source-level and member-level diffs
- **Ignore doc changes**: Use `--ignore-docs` with `diff` to skip XML doc comment changes — reduces noise when only code changes matter
- **CI integration**: `diff` returns exit code 2 when breaking changes are detected (0 = clean, 1 = error)
- **Dependency tree**: Use `deps <pkg>` to see direct dependencies; `--depth 2` for transitive; shared deps show `(already listed)`
- **Version listing**: Use `versions <pkg>` to see all versions; `--stable` for stable only; `--prerelease` for prerelease only; `--latest` for quick lookup of latest stable + prerelease; `--since <ver>` to see only versions released after a specific version (supports `latest`, `latest-stable`, `latest-prerelease` keywords); `--count` for just the number; useful before `diff`
- **JSON output**: Use `--json` (`-j`) or `--output json` (`-o json`) on any command for structured JSON output
- **Output is AI-friendly**: Plain text with `///` XML doc comments — compact and informative
- **For large packages**: Use `search` before `show` to narrow down
- **Version pinning**: Use `--version` to inspect a specific version. Supports `latest`, `latest-stable`, and `latest-prerelease` keywords on any command

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

# See what it depends on
nuget-docs deps Humanizer

# Full transitive dependency tree
nuget-docs deps Microsoft.Extensions.AI --depth 3

# Check available versions (stable only)
nuget-docs versions Humanizer --stable

# Show only prerelease versions
nuget-docs versions Humanizer --prerelease

# Quick lookup: latest stable + prerelease
nuget-docs versions Humanizer --latest

# See what's been released since a specific version
nuget-docs versions Newtonsoft.Json --since 13.0.1

# Combine: stable versions since a specific release
nuget-docs versions Newtonsoft.Json --since 13.0.1 --stable

# Use version keywords: what's newer than the latest stable?
nuget-docs versions Newtonsoft.Json --since latest-stable

# Just count matching versions (useful for CI scripts)
nuget-docs versions Newtonsoft.Json --count --since 13.0.1
nuget-docs versions Newtonsoft.Json --count --stable
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
# See what changed between versions (full unified diff with hunk headers)
nuget-docs diff Microsoft.Extensions.AI.Abstractions --from 10.3.0 --to 10.4.0

# Use "latest" to auto-resolve the latest stable version
nuget-docs diff Microsoft.Extensions.AI.Abstractions --from 10.3.0 --to latest

# Compare latest stable vs latest prerelease
nuget-docs diff Microsoft.Extensions.AI.Abstractions --from latest-stable --to latest-prerelease

# Quick overview — just added/removed types, no decompilation
nuget-docs diff Microsoft.Extensions.AI.Abstractions --from 10.4.0 --to 10.4.1 --type-only

# Show only breaking changes (removed types, removed/changed members)
nuget-docs diff Microsoft.Extensions.AI.Abstractions --from 10.3.0 --to 10.4.0 --breaking

# Member-level changes (added/removed/changed methods/properties)
nuget-docs diff Microsoft.Extensions.AI.Abstractions --from 10.4.0 --to 10.4.1 --member-diff

# Show only removals/modifications (skip purely additive changes)
nuget-docs diff Microsoft.Extensions.AI.Abstractions --from 10.4.0 --to 10.4.1 --no-additive

# Ignore doc comment changes — show only code changes
nuget-docs diff Microsoft.Extensions.AI.Abstractions --from 10.4.0 --to 10.4.1 --ignore-docs

# CI usage — exit code 2 means breaking changes detected
nuget-docs diff MyPackage --from 1.0.0 --to 2.0.0 --type-only || echo "Breaking changes!"

# Get structured diff output
nuget-docs diff Newtonsoft.Json --from 13.0.3 --to 13.0.4 --output json
```

### Version-specific inspection

```bash
# Check a specific version
nuget-docs show Microsoft.Extensions.AI.Abstractions ChatOptions --version 10.4.0

# Use "latest" keyword (works on any command)
nuget-docs list Microsoft.Extensions.AI.Abstractions --version latest
nuget-docs show Microsoft.Extensions.AI.Abstractions IChatClient --version latest-prerelease
```
