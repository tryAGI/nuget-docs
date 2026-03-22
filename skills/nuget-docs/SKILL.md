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

## Commands

### List all public types

```bash
nuget-docs list <Package> [--version <ver>] [--framework <tfm>]
```

Shows all public types grouped by kind (Interfaces, Classes, Structs, Enums, Delegates) with one-line XML doc summaries.

### Show a specific type

```bash
nuget-docs show <Package> <TypeName> [--version <ver>] [--framework <tfm>]
```

Decompiles the full type to C# source with `///` XML documentation comments. **Short names work** — `IChatClient` automatically resolves to `Microsoft.Extensions.AI.IChatClient`.

### Search types and members

```bash
nuget-docs search <Package> <pattern> [--version <ver>] [--framework <tfm>]
```

Searches types and members using glob patterns (`*` and `?` wildcards). Results show `[Kind.MemberKind]` labels.

### Package metadata

```bash
nuget-docs info <Package> [--version <ver>]
```

Shows package ID, version, authors, description, license, frameworks, and dependencies.

## Efficient Usage Patterns

1. **Start broad**: `nuget-docs list <pkg>` to see all public types
2. **Narrow down**: `nuget-docs search <pkg> "Chat*"` to find types/members matching a pattern
3. **Deep dive**: `nuget-docs show <pkg> <TypeName>` for full type details

## Tips for AI Agents

- **Short names resolve automatically**: `IChatClient` → `Microsoft.Extensions.AI.IChatClient`
- **Packages auto-download**: No need to pre-install — packages are fetched from NuGet if not cached
- **Framework auto-selection**: Picks the best TFM (prefers net10.0 > net9.0 > net8.0 > netstandard2.1 > netstandard2.0)
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

### Version-specific inspection

```bash
# Check a specific version
nuget-docs show Microsoft.Extensions.AI.Abstractions ChatOptions --version 10.4.0
```
