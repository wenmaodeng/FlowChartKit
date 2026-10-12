using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlowChartKit.Data;

namespace FlowChartKit.Data;

public static class DiagramSerializer
{
    public static string DefaultFilePath { get; } =
        Path.Combine(AppContext.BaseDirectory, "flowchart.json");

    private static JsonSerializerOptions BuildOptions() => new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string ToJson(DiagramData data)
        => JsonSerializer.Serialize(data, BuildOptions());

    public static DiagramData? FromJson(string json)
        => string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<DiagramData>(json, BuildOptions());

    public static void Save(DiagramData data, string path)
        => File.WriteAllText(path, ToJson(data));

    public static DiagramData? Load(string path)
        => File.Exists(path) ? FromJson(File.ReadAllText(path)) : null;
}
