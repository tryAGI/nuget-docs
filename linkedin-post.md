Your AI coding agent doesn't know your NuGet packages. Now it can.

When you ask Claude Code, Cursor, or Copilot to implement something against a NuGet library — it guesses. It hallucinates method signatures. It uses outdated APIs.

I built nuget-docs — a .NET global tool that gives any AI coding agent instant access to the real public API of any NuGet package.

  dotnet tool install -g nuget-docs

What it does:
→ Decompiles any NuGet package to C# with full XML docs
→ Lists all public types, searches members by pattern
→ Compares API between versions (breaking change detection)
→ Shows dependency trees, package metadata, deprecation info

Install as an AI skill (works with Claude Code, Cursor, Windsurf):

  npx skills add tryAGI/nuget-docs -g

Now when you say "what methods does IChatClient have?" — your agent runs the command and gets the real answer. No guessing.

No more hallucinated APIs. No more wrong method signatures. Just the actual source with /// XML docs.

Open source, MIT licensed: https://github.com/tryAGI/nuget-docs

#dotnet #csharp #ai #nuget #developer #opensource #claudecode #cursor
