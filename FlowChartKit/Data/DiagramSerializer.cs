using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlowChartKit.Data;

namespace FlowChartKit.Data;

public static class DiagramSerializer
{
    public static string DefaultFilePath { get; } =
        Path.Combine(AppContext.BaseDirectory, "flowchart.json");

    // JsonSerializerOptions 构造昂贵且只读使用线程安全：单例复用，避免每次序列化重建
    private static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string ToJson(DiagramData data)
        => JsonSerializer.Serialize(data, s_options);

    public static DiagramData? FromJson(string json)
        => string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<DiagramData>(json, s_options);

    public static void Save(DiagramData data, string path)
        => File.WriteAllText(path, ToJson(data));

    public static DiagramData? Load(string path)
    {
        if (!File.Exists(path)) return null;
        try { return FromJson(File.ReadAllText(path)); }
        catch { return null; }
    }

    /// <summary>异步写入，避免大布局阻塞 UI 线程。</summary>
    public static async Task SaveAsync(DiagramData data, string path, CancellationToken ct = default)
        => await File.WriteAllTextAsync(path, ToJson(data), ct).ConfigureAwait(false);

    /// <summary>异步读取；路径不存在时返回 null。</summary>
    public static async Task<DiagramData?> LoadAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            return FromJson(json);
        }
        catch { return null; }
    }
}
