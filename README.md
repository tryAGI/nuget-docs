# nuget-docs

Inspect public API documentation from any NuGet package — decompile types with XML docs, compare API between versions, resolve dependencies. Built for AI agents and CLI workflows.

## Claude Code Skill (Recommended)

Install as a [Claude Code skill](https://skills.sh) so your AI agent automatically knows when and how to inspect NuGet packages:

```bash
npx skills add tryAGI/nuget-docs -g
```

This teaches Claude Code to run `nuget-docs` commands when you ask questions like *"what methods does IChatClient have?"* or *"show me the ChatOptions class"*.

## CLI Installation

```bash
dotnet tool install -g nuget-docs
```

## Quick Start

```bash
# See all public types in a package
nuget-docs list Microsoft.Extensions.AI.Abstractions

# Decompile a type with full XML docs (short names work)
nuget-docs show Microsoft.Extensions.AI.Abstractions IChatClient

# Search for types/members
nuget-docs search Microsoft.Extensions.AI.Abstractions "Chat*"

# Compare API between versions
nuget-docs diff Microsoft.Extensions.AI.Abstractions --from 10.3.0 --to latest
```

## Commands

### `list` — List all public types

```bash
nuget-docs list Microsoft.Extensions.AI.Abstractions
nuget-docs list Newtonsoft.Json --namespace Newtonsoft.Json.Linq
nuget-docs list Humanizer --all  # include internal types
```

### `show` — Decompile a type with XML docs

```bash
nuget-docs show Microsoft.Extensions.AI.Abstractions IChatClient
nuget-docs show Newtonsoft.Json JsonConvert --member SerializeObject
nuget-docs show Newtonsoft.Json --assembly  # assembly-level attributes
```

Short names work — `IChatClient` resolves to `Microsoft.Extensions.AI.IChatClient`. Use `--member` to show a specific member (all overloads). Use `--assembly` to inspect assembly attributes.

### `search` — Search types and members

```bash
nuget-docs search Microsoft.Extensions.AI.Abstractions "Chat*"
nuget-docs search Newtonsoft.Json "*Token*" --namespace Newtonsoft.Json.Linq
```

Uses glob patterns (`*` and `?` wildcards). Results show `[Kind.MemberKind]` labels.

### `diff` — Compare API between versions

```bash
# Full unified diff with hunk headers
nuget-docs diff Microsoft.Extensions.AI.Abstractions --from 10.3.0 --to 10.4.0

# Use "latest" to auto-resolve the latest version
nuget-docs diff Microsoft.Extensions.AI.Abstractions --from 10.3.0 --to latest

# Compare latest stable vs latest prerelease
nuget-docs diff Microsoft.Extensions.AI.Abstractions --from latest-stable --to latest-prerelease

# Quick summary — added/removed types only
nuget-docs diff Microsoft.Extensions.AI.Abstractions --from 10.4.0 --to 10.4.1 --type-only

# Breaking changes only
nuget-docs diff Microsoft.Extensions.AI.Abstractions --from 10.3.0 --to 10.4.0 --breaking

# Member-level changes (methods/properties)
nuget-docs diff Microsoft.Extensions.AI.Abstractions --from 10.4.0 --to 10.4.1 --member-diff

# Skip additive changes — show only removals/modifications
nuget-docs diff Microsoft.Extensions.AI.Abstractions --from 10.4.0 --to 10.4.1 --no-additive

# Ignore XML doc comment changes
nuget-docs diff Microsoft.Extensions.AI.Abstractions --from 10.4.0 --to 10.4.1 --ignore-docs
```

Exit codes: `0` = no breaking changes, `1` = error, `2` = breaking changes detected (useful for CI).

### `info` — Package metadata

```bash
nuget-docs info Newtonsoft.Json
```

Shows package ID, version, authors, description, license, frameworks, and dependencies.

### `deps` — Dependency tree

```bash
nuget-docs deps Microsoft.Extensions.AI
nuget-docs deps Microsoft.Extensions.AI --depth 3  # transitive
```

Tree-style output with `├──`/`└──` connectors. Shared dependencies marked `(already listed)`.

### `versions` — List available versions

```bash
nuget-docs versions Humanizer
nuget-docs versions Humanizer --stable       # exclude prereleases
nuget-docs versions Humanizer --latest       # latest stable + prerelease
nuget-docs versions Newtonsoft.Json --since 13.0.1           # versions after 13.0.1
nuget-docs versions Newtonsoft.Json --since 13.0.1 --stable  # stable only, after 13.0.1
nuget-docs versions Newtonsoft.Json --count --since 13.0.1   # just the count (for CI)
nuget-docs versions Humanizer --limit 50     # show more (default: 20, 0 = all)
```

### Common options

| Option | Short | Description |
|--------|-------|-------------|
| `--version <ver>` | `-v` | Pin to a specific package version (`latest`, `latest-stable`, `latest-prerelease`) |
| `--framework <tfm>` | `-f` | Target framework (auto-detected by default) |
| `--all` | `-a` | Include internal/private members |
| `--namespace <prefix>` | `-n` | Filter by namespace prefix |
| `--output json` | `-o json` | JSON output for programmatic use |

## Features

- Inspects any public NuGet package — auto-downloads if not cached
- Full C# decompilation with `///` XML documentation comments
- Short type name resolution (`IChatClient` → `Microsoft.Extensions.AI.IChatClient`)
- Nested type support (`ChatRole.Converter` → `ChatRole+Converter`)
- API diff with unified diff, member-level changes, and breaking change detection
- Version keywords: `latest`, `latest-stable`, `latest-prerelease` work on any `--version`, `--from`, `--to`
- Dependency tree with transitive resolution and deduplication
- Version listing with stable/latest filters
- Framework-aware: picks best matching TFM (net10.0 > net9.0 > ... > netstandard2.0)
- AI-optimized plain text output
- JSON output on all commands
- Tab completion via `dotnet-suggest`
- Zero configuration — works out of the box

## Shell Completion

Install the `dotnet-suggest` global tool and enable tab completion for your shell:

```bash
dotnet tool install -g dotnet-suggest
```

**bash** (add to `~/.bashrc`):
```bash
source <(dotnet-suggest script bash)
```

**zsh** (add to `~/.zshrc`):
```bash
source <(dotnet-suggest script zsh)
```

**fish** (add to `~/.config/fish/config.fish`):
```fish
dotnet-suggest script fish | source
```

## License

MIT
