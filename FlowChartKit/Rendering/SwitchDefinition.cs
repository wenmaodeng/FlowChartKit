using System.Windows;
using System.Windows.Media;

namespace FlowChartKit.Rendering;

/// <summary>
/// 开关部件（内置）：矩形底座 + 滑槽 + 拨钮，左进右出接入流路，State 表示通/断。
/// 约定载荷类型为 <see cref="SwitchState"/>（默认 Off）；双击切换通断，
/// 由定义经 <see cref="INodeDefinition.InvokeEvent"/> 自消费，宿主零代码开箱即用。
/// 宿主也可经 SetNodeStateByNumberCommand 程序化设置（第三个参数传 <see cref="SwitchState"/>
/// ——未匹配的载荷（null 或自定义对象）按 Off 渲染）。
/// </summary>
public sealed class SwitchDefinition : INodeDefinition
{
    private static readonly Brush s_rodFill = Frozen(0xC9, 0xD2, 0xDF);    // 金属底座
    private static readonly Brush s_slotFill = Frozen(0xF7, 0xFA, 0xFF);   // 滑槽（白）
    private static readonly Brush s_knobOn = Frozen(0x2E, 0xCC, 0x71);     // 闭合：绿
    private static readonly Brush s_knobOff = Frozen(0xB2, 0xB9, 0xC6);    // 断开：灰
    private static readonly Brush s_knobGloss = Frozen(0xFF, 0xFF, 0xFF, 0x8C);

    private static Brush Frozen(byte r, byte g, byte b, byte a = 0xFF)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    public NodeKind Kind => NodeKind.Switch;
    public string DisplayName => "开关";
    public (double W, double H) BaseSize => (48, 36);

    // 左进右出：开关串联在流路中
    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec("L", 0, 0.5, PortDir.Left),
        new PortSpec("R", 1, 0.5, PortDir.Right),
    ];

    public IReadOnlyList<NodeEventSpec> Events { get; } =
    [
        new(NodeEventGesture.DoubleClick, "ToggleSwitch", "切换通断"),
    ];

    public bool InvokeEvent(ViewModels.NodeViewModel node, NodeEventGesture gesture, string eventId)
    {
        if (gesture != NodeEventGesture.DoubleClick || eventId != "ToggleSwitch")
            return false;

        // 未匹配的 State（null 或宿主自定义载荷）视为 Off，切换后为 On
        node.State = IsOn(node?.State) ? SwitchState.Off : SwitchState.On;
        return true;
    }

    public void Render(DrawingContext dc) => RenderSwitch(dc, SwitchState.Off);

    public void Render(DrawingContext dc, ViewModels.NodeViewModel node)
        => RenderSwitch(dc, IsOn(node?.State) ? SwitchState.On : SwitchState.Off);

    private static bool IsOn(object? state) => state is SwitchState s && s == SwitchState.On;

    private static void RenderSwitch(DrawingContext dc, SwitchState state)
    {
        var on = state == SwitchState.On;
        // 左右端子接线
        dc.DrawLine(NodeRenderer.AccentPen, new Point(1, 18), new Point(7, 18));
        dc.DrawLine(NodeRenderer.AccentPen, new Point(41, 18), new Point(47, 18));
        // 底座
        NodeRenderer.DrawRoundRect(dc, 2, 4, 44, 28, 8, s_rodFill, NodeRenderer.OutlinePen);
        // 滑槽
        NodeRenderer.DrawRoundRect(dc, 11, 13, 26, 10, 5, s_slotFill, NodeRenderer.OutlinePen14);
        // 拨钮：闭合滑向右端（绿），断开停在左端（灰）
        var knobX = on ? 31 : 17;
        var center = new Point(knobX, 18);
        dc.DrawEllipse(on ? s_knobOn : s_knobOff, NodeRenderer.OutlinePen14, center, 7, 7);
        // 左上高光
        dc.DrawEllipse(s_knobGloss, null, new Point(knobX - 2.4, 15.4), 2.2, 1.6);
    }
}
