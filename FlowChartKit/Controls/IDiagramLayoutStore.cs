using FlowChartKit.Data;
using FlowChartKit.ViewModels;

namespace FlowChartKit.Controls;

/// <summary>
/// 布局持久化存储：负责 <see cref="DiagramData"/> 的读写。
/// 默认实现为 <see cref="JsonDiagramLayoutStore"/>（本地 JSON 文件）；
/// 宿主可提供自定义实现（数据库、网络、嵌入式资源等）并赋给
/// <see cref="DiagramCanvasControl.LayoutStore"/>。
/// </summary>
public interface IDiagramLayoutStore
{
    /// <summary>读取布局；路径不存在或内容无效时返回 null。</summary>
    DiagramData? Load(string path);

    /// <summary>写入布局；失败时抛异常，由调用方决定如何提示。</summary>
    void Save(DiagramData data, string path);

    /// <summary>异步读取；默认委托给同步 <see cref="Load"/>，自定义实现可覆写为真正的异步 IO。</summary>
    Task<DiagramData?> LoadAsync(string path, CancellationToken ct = default)
        => Task.FromResult(Load(path));

    /// <summary>异步写入；默认委托给同步 <see cref="Save"/>，自定义实现可覆写为真正的异步 IO。</summary>
    Task SaveAsync(DiagramData data, string path, CancellationToken ct = default)
    {
        Save(data, path);
        return Task.CompletedTask;
    }
}

/// <summary>默认的 JSON 文件存储，内部走 <see cref="DiagramSerializer"/>。</summary>
public sealed class JsonDiagramLayoutStore : IDiagramLayoutStore
{
    public DiagramData? Load(string path) => DiagramSerializer.Load(path);

    public void Save(DiagramData data, string path) => DiagramSerializer.Save(data, path);

    public Task<DiagramData?> LoadAsync(string path, CancellationToken ct = default)
        => DiagramSerializer.LoadAsync(path, ct);

    public Task SaveAsync(DiagramData data, string path, CancellationToken ct = default)
        => DiagramSerializer.SaveAsync(data, path, ct);
}

/// <summary>
/// 示例布局提供者：当无存档文件（或存档解析失败）以及用户点击"恢复示例"时，
/// 控件用此接口生成初始流路图。默认实现为 <see cref="DefaultDemoLayoutProvider"/>，
/// 内部调用核心库内置的 <see cref="DemoLayout"/>；宿主可替换为空实现（禁用内置示例）
/// 或自定义布局，从而无需在宿主侧维护示例数据、完全解耦核心库的 DemoLayout 技术债。
/// </summary>
public interface IDemoLayoutProvider
{
    /// <summary>清空图并填充示例内容。</summary>
    void Build(DiagramViewModel diagram);
}

/// <summary>默认示例布局：委托给核心库内置的 <see cref="DemoLayout.Build"/>。</summary>
internal sealed class DefaultDemoLayoutProvider : IDemoLayoutProvider
{
    public void Build(DiagramViewModel diagram) => DemoLayout.Build(diagram);
}

/// <summary>空实现示例布局提供者：不填充任何内容，让图保持空白（BuildDemo 已先 Clear）。</summary>
public sealed class EmptyDemoLayoutProvider : IDemoLayoutProvider
{
    public void Build(DiagramViewModel diagram) { }
}
