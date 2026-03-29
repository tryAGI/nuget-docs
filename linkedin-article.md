# Stop Your AI Agent From Hallucinating NuGet APIs

If you're using AI coding agents (Claude Code, Cursor, Windsurf, GitHub Copilot) with .NET projects, you've probably hit this problem:

You ask: *"Implement IChatClient from Microsoft.Extensions.AI"*

The agent confidently writes code with method signatures that don't exist. Or uses an API from version 9.0 when you're on 10.4. Or misses required parameters.

**The problem isn't the AI model — it's the context.** Your agent simply doesn't have access to the actual package API.

## The Solution: nuget-docs

I built a .NET global tool that gives any AI agent (or developer) instant access to the real public API surface of any NuGet package:

```
dotnet tool install -g nuget-docs
```

### What Can It Do?

**See all public types at a glance:**

```
nuget-docs list Microsoft.Extensions.AI.Abstractions
```

```
Interfaces:
  IChatClient — Represents a chat client
  IEmbeddingGenerator<TInput, TEmbedding> — Represents an embedding generator

Classes:
  ChatMessage — Represents a chat message
  ChatOptions — Options for chat completion requests
  ...
```

**Get full decompiled source with XML docs:**

```
nuget-docs show Microsoft.Extensions.AI.Abstractions IChatClient
```

Returns the real C# interface with `///` documentation comments — exactly what the AI needs to implement it correctly.

**Search across types and members:**

```
nuget-docs search Microsoft.Extensions.AI.Abstractions "Chat*"
```

**Compare API between versions (catch breaking changes):**

```
nuget-docs diff Microsoft.Extensions.AI.Abstractions --from 10.3.0 --to 10.4.0
```

**Check dependencies and metadata:**

```
nuget-docs deps Microsoft.Extensions.AI --depth 2
nuget-docs info Newtonsoft.Json
nuget-docs versions Humanizer --stable
```

## The AI Agent Integration

The real power comes when you install it as an AI skill:

```
npx skills add tryAGI/nuget-docs -g
```

This works with **Claude Code, Cursor, Windsurf**, and other AI coding agents. Once installed, your agent automatically knows to use `nuget-docs` when you ask questions about NuGet packages.

Instead of guessing, it runs the actual commands and gets real API information. No hallucinations. No outdated signatures. Just the truth from the package itself.

## Why This Matters for .NET Developers

The .NET ecosystem has 400,000+ packages on NuGet.org. AI models are trained on a snapshot of the internet — they can't know every version of every package. But with `nuget-docs`, they don't need to. They can look it up in real time.

This is especially valuable when:

- Working with **Microsoft.Extensions.AI** (the new unified AI abstraction layer) — it's evolving fast
- Implementing against **unfamiliar libraries** where you need to understand the API contract
- **Upgrading packages** and need to know what changed between versions
- Working with **internal/private NuGet feeds** where the AI has zero training data

## Open Source

The tool is MIT licensed and open source. Contributions welcome.

- **GitHub:** https://github.com/tryAGI/nuget-docs
- **NuGet:** https://www.nuget.org/packages/nuget-docs
- **Skill:** `npx skills add tryAGI/nuget-docs -g`

If you're building with .NET and using AI agents — give it a try and let me know what you think.
