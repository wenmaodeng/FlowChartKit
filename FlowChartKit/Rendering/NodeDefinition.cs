using System.Collections.Concurrent;
using System.Windows.Media;

namespace FlowChartKit.Rendering;

/// <summary>端口规格：相对位置（FX/FY 为形状宽高比例）与出口方向。</summary>
public readonly record struct PortSpec(string Id, double FX, double FY, PortDir Dir);

/// <summary>
/// 部件定义：一种部件的名称、基准尺寸、端点与外观集中在一个类里自管理。
/// 实现必须是无状态的（同一实例被多个部件控件复用）；
/// 绘制用的 Brush/Pen/Geometry 建议冻结（Freeze）后复用，<see cref="Render"/> 只会在 UI 线程被调用。
/// </summary>
public interface INodeDefinition
{
    /// <summary>该定义对应的部件类型。</summary>
    NodeKind Kind { get; }

    /// <summary>部件默认显示名（未命名时作为标签）。</summary>
    string DisplayName { get; }

    /// <summary>基准宽高（未缩放）。</summary>
    (double W, double H) BaseSize { get; }

    /// <summary>默认端口列表。</summary>
    IReadOnlyList<PortSpec> Ports { get; }

    /// <summary>在基准尺寸（未缩放）坐标系内绘制部件外观；缩放由调用方通过 Transform 施加。</summary>
    void Render(DrawingContext dc);
}

/// <summary>
/// 部件定义注册表：按 <see cref="NodeKind"/> 解析部件定义。
/// 内置定义覆盖当前全部枚举值，静态构造时自动注册；
/// 宿主可在启动时 <see cref="Register"/> 自定义定义整体替换某类型的名称/尺寸/端点/外观。
/// 注册/注销可从任意线程调用（内部使用并发字典）。
/// </summary>
public static class NodeDefinitionRegistry
{
    private static readonly ConcurrentDictionary<NodeKind, INodeDefinition> s_definitions = new();

    static NodeDefinitionRegistry()
    {
        Register(new SampleCupDefinition());
        Register(new ReagentBottleDefinition());
        Register(new WasteDefinition());
        Register(new ReactionCupDefinition());
        Register(new CuvetteDefinition());
        Register(new CentrifugeDefinition());
        Register(new NeedleWashDefinition());
        Register(new PumpModuleDefinition());
    }

    /// <summary>注册（或覆盖）某部件类型的定义。</summary>
    public static void Register(INodeDefinition definition)
        => s_definitions[(definition ?? throw new ArgumentNullException(nameof(definition))).Kind] = definition;

    /// <summary>移除注册。返回是否真的移除了；移除后该类型不可再被解析。</summary>
    public static bool Unregister(NodeKind kind) => s_definitions.TryRemove(kind, out _);

    /// <summary>按类型解析定义；该类型无注册定义时抛 <see cref="InvalidOperationException"/>。</summary>
    public static INodeDefinition Resolve(NodeKind kind)
        => s_definitions.TryGetValue(kind, out var d)
            ? d
            : throw new InvalidOperationException($"部件类型 {kind} 没有注册的部件定义");
}
