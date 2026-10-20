using System.Collections.Concurrent;
using System.Windows.Media;

namespace FlowChartKit.Rendering;

/// <summary>
/// 部件外观渲染器：直接向 <see cref="DrawingContext"/> 绘制矢量图形。
/// 实现必须是无状态的（同一实例会被多个部件控件复用），
/// 内部使用的 Brush/Pen/Geometry 建议冻结（Freeze）后复用。
/// </summary>
public interface INodeRenderer
{
    /// <summary>在基准尺寸（未缩放）坐标系内绘制部件外观；缩放由调用方通过 Transform 施加。</summary>
    void Render(DrawingContext dc);
}

/// <summary>
/// 部件渲染器注册表：按 <see cref="NodeKind"/> 注册/覆盖外观。
/// 未注册的类型回退到 <see cref="NodeRenderer"/> 的内置绘制。
/// 宿主可在启动时调用 <see cref="Register"/> 替换任意类型的外观，或为新增枚举值提供外观。
/// 注册/注销可从任意线程调用（内部使用并发字典）；
/// 渲染器实例的 <see cref="INodeRenderer.Render"/> 只会在 UI 线程被调用，实现无需考虑并发。
/// </summary>
public static class NodeRendererRegistry
{
    private static readonly ConcurrentDictionary<NodeKind, INodeRenderer> s_renderers = new();

    /// <summary>注册（或覆盖）某部件类型的渲染器。</summary>
    public static void Register(NodeKind kind, INodeRenderer renderer)
        => s_renderers[kind] = renderer ?? throw new ArgumentNullException(nameof(renderer));

    /// <summary>移除注册，使该类型回退到内置外观。返回是否真的移除了。</summary>
    public static bool Unregister(NodeKind kind) => s_renderers.TryRemove(kind, out _);

    /// <summary>按注册表解析渲染器；未注册时返回 null。</summary>
    public static INodeRenderer? Resolve(NodeKind kind)
        => s_renderers.TryGetValue(kind, out var r) ? r : null;
}
