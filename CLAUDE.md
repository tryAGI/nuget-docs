# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**nuget-docs** is a .NET CLI global tool that inspects public API documentation from any NuGet package. It decompiles types with XML docs using ICSharpCode.Decompiler (ILSpy), compares API surfaces between versions, resolves dependencies, and provides multiple output formats. Distributed via NuGet as `nuget-docs`.

## Build Commands

```bash
# Build everything
dotnet build nuget-docs.slnx

# Run integration tests (135 tests, hits NuGet.org)
dotnet test src/NugetDocs.IntegrationTests/

# Run a single test
dotnet test src/NugetDocs.IntegrationTests/ --filter "FullyQualifiedName~List_ReturnsPublicTypes"

# Pack and install locally for manual testing
dotnet pack src/NugetDocs.Cli/NugetDocs.Cli.csproj -o ./nupkg
dotnet tool install --global --add-source ./nupkg nuget-docs --prerelease

# If same version number (stale cache), force reinstall:
dotnet tool uninstall -g nuget-docs
rm -rf ~/.dotnet/tools/.store/nuget-docs/ ~/.nuget/packages/nuget-docs/
dotnet tool install --global --add-source ./nupkg nuget-docs --prerelease
```

## Architecture

### Command Pattern

Every CLI command follows the **Command + CommandAction** split using `System.CommandLine`:

- `FooCommand.cs` — Defines arguments, options, description. All `internal sealed`.
- `FooCommandAction.cs` — Implements `AsynchronousCommandLineAction.InvokeAsync()`. Extracts parsed values, calls services, formats output.

`Program.cs` wires all 7 commands into a `RootCommand` and invokes via `rootCommand.Parse(args).InvokeAsync()`.

### Shared Options (CommonOptions.cs)

Centralized factory properties for reusable CLI options: `Package`, `Version`, `Framework`, `Output`, `Json`, `Format`. Also contains `CsvEscape()` (RFC 4180) and `IsJsonOutput()` helper.

### Service Layer

| Service | Purpose |
|---------|---------|
| `PackageResolver` | Downloads packages from NuGet, resolves version keywords (`latest`, `latest-stable`, `latest-prerelease`), selects best TFM, finds DLL + XML doc paths |
| `TypeInspector` | ILSpy wrapper — type listing, decompilation, search, member extraction, assembly attributes. Strips compiler-generated noise (nullable attrs, display classes, backing fields) |
| `XmlDocReader` | Parses .NET XML doc files, returns type summaries keyed by `T:{FullName}` |
| `NuGetMetadataService` | Queries NuGet V3 Registration API for deprecation/vulnerability info. Caches per package in `ConcurrentDictionary` |
| `MyersDiff` | Myers linear-space diff algorithm for unified source diffs |
| `NuGetVersionComparer` | Semantic version comparison (`IComparer<string?>`) |

### Output Formats

All commands support `--json` / `--output json`. Commands with tabular data (`list`, `search`, `versions`, `deps`) also support `--format table|csv`. The default grouped format is plain text optimized for AI consumption.

### Result Limits

`list`, `search` and `deps` truncate at `CommonOptions.DefaultResultLimit` (200) rows; `versions` at 20; `show` at `CommonOptions.DefaultMaxLines` (1000) source lines via `--max-lines`. `--limit 0` / `--max-lines 0` disables the cap. The shared helpers live in `CommonOptions`: `ApplyLimit` trims a collection, `ApplyLineLimit` trims source text and reports the untrimmed line count, `WriteTruncationFooter` writes the `... and N more (use --limit 0 to show all, or <hint>)` line.

`deps` cannot use `ApplyLimit` — it trims a tree, so `DepsCommandAction.TrimTree` walks depth-first with a shared budget so the printed prefix matches an untrimmed run, and `CountNodes` supplies the pre-limit total. `show` writes its footer as a `//` comment because the output is C# and a truncated type no longer parses.

Rules when adding a limit to a new command:
- Capture `total` **before** trimming — text/table headers and the JSON `total`/`truncated` fields report the pre-limit figure.
- Apply the limit to every output mode, JSON and CSV included, so consumers never see a different row count than the text mode.
- Never write the footer in CSV mode — CSV must stay machine-parseable.
- Any test that compares "more content" by output **character length** breaks once a cap applies — internal members carry no XML docs, so a capped larger listing can be shorter text. Compare row counts, or pass `--limit 0` / `--max-lines 0` on both sides.

### Deprecation

`TypeInspector.GetObsoleteMessage(IEntity)` reads `System.ObsoleteAttribute` off a type or member;
`TypeInfo`, `SearchResult` and `MemberSignature` all carry a nullable `ObsoleteMessage` (null = not
deprecated, empty string = deprecated without a message — so test for `is not null`, never for
truthiness). `SearchResult` deliberately lets a member inherit its declaring type's deprecation;
`GetMemberSignatures` does not, because `diff` needs the member's own state.

`diff` detects two separate transitions, since adding `[Obsolete]` leaves the signature identical
and the key-based member comparison sees nothing: `ChangedType.NewlyDeprecated` for a type, and
`MemberChanges.Deprecated` for members. Neither is treated as breaking (deprecated code compiles),
but `IsPurelyAdditive` returns false for them so `--no-additive` keeps them.

### Grouped Output

`CommonOptions.WriteGroupedByKind` renders the "pluralized heading + two-space indented lines +
trailing blank line" layout shared by `list` (types, ordered by `GetKindOrder`) and
`show --signatures` (members, alphabetical). It writes through an `Action<string>` so `list` can
target `Console.WriteLine` while `show` buffers into a `StringBuilder` for the line cap.
`CommonOptions.Pluralize` handles the kind headings (Property -> Properties, Class -> Classes).

`show --signatures` reads the ILSpy type system without calling `DecompileType`, so it is ~35x
faster than a full decompile (0.11s vs 3.9s on AWSSDK.EC2's AmazonEC2Client), not merely smaller.
The `--max-lines` cap does not share that saving: the type is decompiled first and trimmed after.

### Test Infrastructure

Tests use `CliTestHelper.RunAsync(params string[] args)` which constructs the full `RootCommand` in-memory, redirects `Console.Out`/`Console.Error` to `StringWriter`, and returns `(exitCode, stdout, stderr)`. This tests the real CLI pipeline without process spawning.

## Key Conventions

- All commands and services are `internal sealed` — test project accesses via `InternalsVisibleTo`
- `ConfigureAwait(false)` on all async calls
- Error pattern: try-catch in CommandAction, write to `Console.Error`, return exit code 1
- `diff` exit codes: 0 = no breaking changes, 1 = error, 2 = breaking changes detected
- NuGet API calls use `#pragma warning disable CA1308` for required lowercase
- `dotnet pack` defaults to Release config; use `-c Release` for build or just `dotnet pack`

## CI/CD

Uses shared `HavenDV/workflows/.github/workflows/dotnet_build-test-publish.yml@main`. Publishes to NuGet.org on every push to `main`. Version tags (`v*`) also create GitHub Releases. Versioning via MinVer with `v` tag prefix.
