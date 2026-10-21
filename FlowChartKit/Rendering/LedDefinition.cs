using System.Windows;
using System.Windows.Media;

namespace FlowChartKit.Rendering;

/// <summary>
/// LED 指示灯（内置）：圆形灯座 + 状态色灯泡 + 高光。State 即颜色，
/// 约定载荷类型为 <see cref="LedColorState"/>（默认 Gray）；双击循环切换四色，
/// 由定义经 <see cref="INodeDefinition.InvokeEvent"/> 自消费，宿主零代码开箱即用。
/// 宿主也可经 SetNodeStateByNumberCommand 程序化设置（第三个参数传 <see cref="LedColorState"/>
/// ——未匹配的载荷（null 或自定义对象）按 Gray 渲染）。
/// </summary>
public sealed class LedDefinition : INodeDefinition
{
    /// <summary>双击循环顺序：灰 → 红 → 绿 → 蓝 → 灰。</summary>
    public static readonly IReadOnlyList<LedColorState> ColorCycle =
        [LedColorState.Gray, LedColorState.Red, LedColorState.Green, LedColorState.Blue];

    private static readonly Brush s_bezelFill = Frozen(0xC9, 0xD2, 0xDF);   // 金属灯座
    private static readonly Brush s_grayBulb = Frozen(0xB2, 0xB9, 0xC6);    // 灰（熄灭）
    private static readonly Brush s_redBulb = Frozen(0xE7, 0x4C, 0x3C);
    private static readonly Brush s_greenBulb = Frozen(0x2E, 0xCC, 0x71);
    private static readonly Brush s_blueBulb = Frozen(0x3D, 0x8B, 0xD9);
    private static readonly Brush s_gloss = Frozen(0xFF, 0xFF, 0xFF, 0x8C); // 半透明白高光

    private static Brush Frozen(byte r, byte g, byte b, byte a = 0xFF)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    public NodeKind Kind => NodeKind.Led;
    public string DisplayName => "指示灯";
    public (double W, double H) BaseSize => (32, 32);

    // 指示灯同样接入流路：常规四端口（上下左右）
    public IReadOnlyList<PortSpec> Ports => NodePorts.Standard;

    public IReadOnlyList<NodeEventSpec> Events { get; } =
    [
        new(NodeEventGesture.DoubleClick, "ToggleColor", "切换颜色"),
    ];

    public bool InvokeEvent(ViewModels.NodeViewModel node, NodeEventGesture gesture, string eventId)
    {
        if (gesture != NodeEventGesture.DoubleClick || eventId != "ToggleColor")
            return false;

        // 循环取下一色；当前值不在约定内（null 或宿主自定义载荷）时视为灰，从头开始
        var current = node.State as LedColorState?;
        var index = IndexOfColor(current);
        node.State = ColorCycle[(index + 1) % ColorCycle.Count];
        return true;
    }

    public void Render(DrawingContext dc) => RenderBulb(dc, LedColorState.Gray);

    public void Render(DrawingContext dc, ViewModels.NodeViewModel node)
        => RenderBulb(dc, node?.State as LedColorState?);

    private void RenderBulb(DrawingContext dc, LedColorState? state)
    {
        var center = new Point(16, 16);
        // 金属灯座
        dc.DrawEllipse(s_bezelFill, NodeRenderer.OutlinePen, center, 14, 14);
        // 灯泡（未匹配的 State 载荷按灰渲染）
        var bulb = state switch
        {
            LedColorState.Red => s_redBulb,
            LedColorState.Green => s_greenBulb,
            LedColorState.Blue => s_blueBulb,
            _ => s_grayBulb,
        };
        dc.DrawEllipse(bulb, NodeRenderer.OutlinePen14, center, 10, 10);
        // 左上高光，营造玻璃质感
        dc.DrawEllipse(s_gloss, null, new Point(11.5, 11.5), 3.2, 2.4);
    }

    private static int IndexOfColor(LedColorState? state) => state switch
    {
        LedColorState.Red => 1,
        LedColorState.Green => 2,
        LedColorState.Blue => 3,
        _ => 0,
    };
}
