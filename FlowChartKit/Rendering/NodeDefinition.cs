using System.Collections.Concurrent;
using System.Windows.Media;

namespace FlowChartKit.Rendering;

/// <summary>端口规格：相对位置（FX/FY 为形状宽高比例）与出口方向。</summary>
public readonly record struct PortSpec(string Id, double FX, double FY, PortDir Dir);

/// <summary>
/// 部件定义：一种部件的名称、基准尺寸、端点与外观集中在一个类里自管理。
/// 实现必须是无状态的（同一实例被多个部件控件复用）；
/// 绘制用的 Brush/Pen/Geometry 建议冻结（Freeze）后复用，<see cref="Render"/> 只会在 UI 线程被调用。
/// 程序集外的实现可使用 <see cref="NodeRenderer"/> 公开的标准画刷
/// （Outline/FillLight/FillWhite/Accent）保持配色一致；Pen 需自行创建并冻结。
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

    /// <summary>
    /// 事件目录：声明该部件支持哪些手势及对应语义事件（见 <see cref="NodeEventSpec"/>）。
    /// 默认空目录 = 未启用事件目录，控件按默认单击语义上抛（EventId 为 null，向后兼容）；
    /// 目录非空后仅目录中声明过的手势才触发事件。同一手势只应声明一个条目。
    /// </summary>
    IReadOnlyList<NodeEventSpec> Events => [];
}

/// <summary>
/// 部件定义注册表：按 <see cref="NodeKind"/> 解析部件定义。
/// 两层结构——内置定义层只读不可注销，保证任何枚举值恒可解析；
/// 自定义覆盖层承载宿主的 <see cref="Register"/>/<see cref="Unregister"/>，注销后自动回退内置定义。
/// 注册/注销可从任意线程调用（覆盖层使用并发字典）。
/// </summary>
public static class NodeDefinitionRegistry
{
    // 内置定义层：初始化后只读，并发读取安全
    private static readonly IReadOnlyDictionary<NodeKind, INodeDefinition> s_builtIns =
        new Dictionary<NodeKind, INodeDefinition>
        {
            [NodeKind.SampleCup] = new SampleCupDefinition(),
            [NodeKind.ReagentBottle] = new ReagentBottleDefinition(),
            [NodeKind.Waste] = new WasteDefinition(),
            [NodeKind.ReactionCup] = new ReactionCupDefinition(),
            [NodeKind.Cuvette] = new CuvetteDefinition(),
            [NodeKind.Centrifuge] = new CentrifugeDefinition(),
            [NodeKind.NeedleWash] = new NeedleWashDefinition(),
            [NodeKind.PumpModule] = new PumpModuleDefinition(),
            [NodeKind.ThreeWayValve] = new ThreeWayValveDefinition(),
        };

    // 自定义覆盖层：宿主注册/注销只作用于此层
    private static readonly ConcurrentDictionary<NodeKind, INodeDefinition> s_overrides = new();

    // All 属性的缓存快照；注册/注销时标记为 null 以触发重建
    private static volatile IReadOnlyCollection<INodeDefinition>? s_cachedAll;

    /// <summary>注册（或覆盖）某部件类型的自定义定义，优先于内置定义生效。</summary>
    public static void Register(INodeDefinition definition)
    {
        s_overrides[(definition ?? throw new ArgumentNullException(nameof(definition))).Kind] = definition;
        s_cachedAll = null;
    }

    /// <summary>移除某类型的自定义定义并回退到内置定义。返回是否真的移除了自定义定义。</summary>
    public static bool Unregister(NodeKind kind)
    {
        bool removed = s_overrides.TryRemove(kind, out _);
        if (removed) s_cachedAll = null;
        return removed;
    }

    /// <summary>按类型解析定义：自定义覆盖优先，其次内置定义；两者皆无（未知枚举值）时抛 <see cref="InvalidOperationException"/>。</summary>
    public static INodeDefinition Resolve(NodeKind kind)
        => s_overrides.TryGetValue(kind, out var d)
            ? d
            : s_builtIns.TryGetValue(kind, out var b)
                ? b
                : throw new InvalidOperationException($"部件类型 {kind} 没有注册的部件定义");

    /// <summary>当前生效的全部定义快照：内置定义与自定义覆盖的并集（同类型时自定义优先），供菜单等枚举场景使用。
    /// 注册/注销时自动失效重建；注册频率远低于枚举频率，缓存有效。</summary>
    public static IReadOnlyCollection<INodeDefinition> All
    {
        get
        {
            var cached = s_cachedAll;
            if (cached != null) return cached;
            var map = new Dictionary<NodeKind, INodeDefinition>(s_builtIns);
            foreach (var kv in s_overrides)
                map[kv.Key] = kv.Value;
            cached = map.Values.ToList();
            s_cachedAll = cached;
            return cached;
        }
    }
}
