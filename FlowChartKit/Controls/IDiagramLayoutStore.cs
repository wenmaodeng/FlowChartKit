using FlowChartKit.Data;

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
}

/// <summary>默认的 JSON 文件存储，内部走 <see cref="DiagramSerializer"/>。</summary>
public sealed class JsonDiagramLayoutStore : IDiagramLayoutStore
{
    public DiagramData? Load(string path) => DiagramSerializer.Load(path);

    public void Save(DiagramData data, string path) => DiagramSerializer.Save(data, path);
}
