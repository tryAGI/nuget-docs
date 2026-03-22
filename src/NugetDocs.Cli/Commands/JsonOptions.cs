using System.Text.Json;

namespace NugetDocs.Cli.Commands;

internal static class JsonOptions
{
    public static JsonSerializerOptions Indented { get; } = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}
