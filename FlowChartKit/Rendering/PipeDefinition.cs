using System.Collections.Concurrent;
using System.Windows.Media;

namespace FlowChartKit.Rendering;

/// <summary>
/// 管路流动画规格：虚线图案 + 一个循环所需时长。
/// 控件把 DashOffset 在一个 dash 周期内做往复/循环动画，形成"液体流动"的视觉效果。
/// </summary>
public sealed class PipeFlowSpec
{
    /// <summary>虚线图案（实线段与间隔交替，单位 px），如 {4, 4}；一个循环的位移量 = 各项之和。</summary>
    public double[] Dash { get; init; } = [4, 4];

    /// <summary>一个 dash 循环所需的时长；值越小流动越快。需为正值。</summary>
    public TimeSpan Duration { get; init; } = TimeSpan.FromSeconds(0.6);

    /// <summary>false 表示该类型管路不流动：即使设置了流向也恒为实线（不启动动画）。</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>默认流动规格：{4,4} 虚线、0.6s 一个循环（内置普通管路的取值）。</summary>
    public static PipeFlowSpec Default { get; } = new();

    /// <summary>不流动（恒实线）。</summary>
    public static PipeFlowSpec None { get; } = new() { Enabled = false };

    /// <summary>自定义流动规格；dash 为空或时长非正时回退为 <see cref="Default"/>。</summary>
    public static PipeFlowSpec Create(double[]? dash, TimeSpan duration, bool enabled = true)
        => dash is { Length: > 0 } && duration > TimeSpan.Zero
            ? new PipeFlowSpec { Dash = dash, Duration = duration, Enabled = enabled }
            : (enabled ? Default : None);

    /// <summary>一个循环的位移量（dash 各项之和），用于动画的起止值。</summary>
    public double Cycle
    {
        get
        {
            double sum = 0;
            foreach (var d in Dash) sum += d;
            return sum > 0 ? sum : 8.0;
        }
    }
}

/// <summary>
/// 管路定义：一种管路的颜色、粗细与流动画集中在一个类里自管理。
/// 实现必须是无状态的（同一实例被多条管路复用）；
/// 画刷建议冻结（Freeze）后复用（可直接使用 <see cref="NodeRenderer.Frozen"/>）。
/// 宿主实现后经 <see cref="PipeDefinitionRegistry.Register"/> 注册即可使用。
/// </summary>
public interface IPipeDefinition
{
    /// <summary>该定义对应的管路类型。</summary>
    PipeKind Kind { get; }

    /// <summary>管路类型默认显示名（菜单与属性对话框用）。</summary>
    string DisplayName { get; }

    /// <summary>管路颜色（普通态）。</summary>
    Brush Stroke { get; }

    /// <summary>管路线宽（普通态）。</summary>
    double Thickness { get; }

    /// <summary>管路颜色（选中态）。</summary>
    Brush SelectedStroke { get; }

    /// <summary>管路线宽（选中态）。</summary>
    double SelectedThickness { get; }

    /// <summary>
    /// 流向为**停止**（<c>FlowMode.None</c>）时的管路颜色；返回 null 表示与 <see cref="Stroke"/> 同色
    /// （默认实现，宿主不关心流向配色时无需重写）。
    /// </summary>
    Brush? StoppedStroke => null;

    /// <summary>
    /// 流向为**停止**（<c>FlowMode.None</c>）时的线宽；默认与 <see cref="Thickness"/> 相同。
    /// 非正值同样按 <see cref="Thickness"/> 处理。
    /// </summary>
    double StoppedThickness => Thickness;

    /// <summary>流动画规格；<see cref="PipeFlowSpec.Enabled"/> 为 false 时该类型恒为实线。</summary>
    PipeFlowSpec Flow { get; }
}

/// <summary>
/// 内置普通管路定义：按流向区分颜色——正向/反向 #3161FF（主色，2.0px）、停止 #B1C4FF（浅色，2.0px）；
/// 选中态 #E67E22 / 2.6px，流动画 {4,4} 0.6s（仅在有流向时流动，停止时恒为实线）。
/// </summary>
public sealed class StandardPipeDefinition : IPipeDefinition
{
    public PipeKind Kind => PipeKind.Standard;
    public string DisplayName => "普通管路";

    // 有流向（正向/反向）
    public Brush Stroke { get; } = NodeRenderer.Frozen(0x31, 0x61, 0xFF);
    public double Thickness => 2.0;

    // 流向停止
    public Brush? StoppedStroke { get; } = NodeRenderer.Frozen(0xB1, 0xC4, 0xFF);
    public double StoppedThickness => 2.0;

    public Brush SelectedStroke { get; } = NodeRenderer.Frozen(0xE6, 0x7E, 0x22);
    public double SelectedThickness => 2.6;

    public PipeFlowSpec Flow => PipeFlowSpec.Default;
}

/// <summary>
/// 管路定义注册表：按 <see cref="PipeKind"/> 解析管路定义（颜色/粗细/流动画）。
/// 两层结构——内置定义层只读不可注销，保证内置枚举值恒可解析；
/// 自定义覆盖层承载宿主的 <see cref="Register"/>/<see cref="Unregister"/>，注销后自动回退内置定义。
/// 注册/注销可从任意线程调用（覆盖层使用并发字典）。
/// </summary>
public static class PipeDefinitionRegistry
{
    private static readonly IReadOnlyDictionary<PipeKind, IPipeDefinition> s_builtIns =
        new Dictionary<PipeKind, IPipeDefinition>
        {
            [PipeKind.Standard] = new StandardPipeDefinition(),
        };

    private static readonly ConcurrentDictionary<PipeKind, IPipeDefinition> s_overrides = new();

    private static volatile IReadOnlyCollection<IPipeDefinition>? s_cachedAll;

    /// <summary>注册（或覆盖）某管路类型的自定义定义，优先于内置定义生效。</summary>
    public static void Register(IPipeDefinition definition)
    {
        s_overrides[(definition ?? throw new ArgumentNullException(nameof(definition))).Kind] = definition;
        s_cachedAll = null;
    }

    /// <summary>移除某类型的自定义定义并回退到内置定义。返回是否真的移除了自定义定义。</summary>
    public static bool Unregister(PipeKind kind)
    {
        bool removed = s_overrides.TryRemove(kind, out _);
        if (removed) s_cachedAll = null;
        return removed;
    }

    /// <summary>按类型解析定义：自定义覆盖优先，其次内置定义；两者皆无（未知枚举值）时抛 <see cref="InvalidOperationException"/>。</summary>
    public static IPipeDefinition Resolve(PipeKind kind)
        => s_overrides.TryGetValue(kind, out var d)
            ? d
            : s_builtIns.TryGetValue(kind, out var b)
                ? b
                : throw new InvalidOperationException($"管路类型 {kind} 没有注册的管路定义");

    /// <summary>该类型当前是否有可解析的定义（内置或宿主注册）；载入存档时用于回退无效类型。</summary>
    public static bool IsAvailable(PipeKind kind)
        => s_overrides.ContainsKey(kind) || s_builtIns.ContainsKey(kind);

    /// <summary>该类型当前是否由内置定义提供：未注册覆盖层（或覆盖已注销回退）即内置。</summary>
    public static bool IsBuiltIn(PipeKind kind) => !s_overrides.ContainsKey(kind);

    /// <summary>当前生效的全部定义快照：内置定义与自定义覆盖的并集（同类型时自定义优先），供菜单等枚举场景使用。
    /// 注册/注销时自动失效重建；注册频率远低于枚举频率，缓存有效。</summary>
    public static IReadOnlyCollection<IPipeDefinition> All
    {
        get
        {
            var cached = s_cachedAll;
            if (cached != null) return cached;

            var merged = new Dictionary<PipeKind, IPipeDefinition>(s_builtIns);
            foreach (var kv in s_overrides) merged[kv.Key] = kv.Value;
            cached = merged.Values.ToArray();
            s_cachedAll = cached;
            return cached;
        }
    }
}
