using System.Windows;
using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Nodes;

/// <summary>Demo 自定义部件类型：核心库枚举之外的扩展值（序列化按数值落盘，回读不受影响）。</summary>
public static class DemoNodeKinds
{
    public const NodeKind SolenoidValve = (NodeKind)1000;
}

/// <summary>
/// 电磁阀（两位两通，左进右出）：蝶形阀体 + 顶部线圈盒。
/// 无状态实现，几何与画笔全部冻结复用；配色复用核心库公开的标准画刷。
/// </summary>
public sealed class SolenoidValveDefinition : INodeDefinition
{
    private static readonly Pen s_outlinePen = MakePen(NodeRenderer.Outline, 1.8);
    private static readonly Pen s_linePen = MakePen(NodeRenderer.Outline, 1.3);
    private static readonly StreamGeometry s_body = BuildBody();

    // 状态外观画刷：开=绿（通流）、关=红（断流）、未设置=默认浅蓝灰；全部冻结复用（实例无状态）
    private static readonly Brush s_onFill = Frozen(0x4C, 0xAF, 0x7D);
    private static readonly Brush s_onCoil = Frozen(0x2F, 0x8A, 0x59);
    private static readonly Brush s_offFill = Frozen(0xE5, 0x88, 0x82);
    private static readonly Brush s_offCoil = Frozen(0xC6, 0x4A, 0x42);
    private static readonly Pen s_onLine = MakePen(s_onCoil, 1.3);
    private static readonly Pen s_offLine = MakePen(s_offCoil, 1.3);

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public NodeKind Kind => DemoNodeKinds.SolenoidValve;

    public string DisplayName => "电磁阀";

    public (double W, double H) BaseSize => (64, 52);

    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec("L", 0, 0.5, PortDir.Left),
        new PortSpec("R", 1, 0.5, PortDir.Right),
    ];

    /// <summary>事件目录演示：声明后仅这两个手势会上抛事件，宿主按 EventId 分发。</summary>
    public IReadOnlyList<NodeEventSpec> Events { get; } =
    [
        new(NodeEventGesture.Click, "ValveInfo", "查看阀门信息"),
        new(NodeEventGesture.DoubleClick, "ToggleState", "切换开关状态"),
    ];

    public void Render(DrawingContext dc) => Render(dc, node: null!);

    /// <summary>按 State 区分外观："开"绿色（线圈盒+引线）、"关"红色、其余默认配色。State 语义由宿主约定。</summary>
    public void Render(DrawingContext dc, FlowChartKit.ViewModels.NodeViewModel node)
    {
        var state = node?.State as string;
        var on = state == "开";
        var off = state == "关";
        var coilFill = on ? s_onFill : off ? s_offFill : NodeRenderer.FillLight;
        var linePen = on ? s_onLine : off ? s_offLine : s_linePen;

        // 蝶形阀体（两个顶点相接的三角形）
        dc.DrawGeometry(NodeRenderer.FillWhite, s_outlinePen, s_body);
        // 线圈引线（颜色随状态）
        dc.DrawLine(linePen, new Point(32, 14), new Point(32, 26));
        // 顶部线圈盒（填充随状态）
        dc.DrawRectangle(coilFill, s_outlinePen, new Rect(22, 2, 20, 12));
    }

    private static StreamGeometry BuildBody()
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            // 左三角：左边线 + 中尖
            ctx.BeginFigure(new Point(2, 14), isFilled: true, isClosed: true);
            ctx.LineTo(new Point(32, 26), isStroked: true, isSmoothJoin: false);
            ctx.LineTo(new Point(2, 38), isStroked: true, isSmoothJoin: false);
            // 右三角
            ctx.BeginFigure(new Point(62, 14), isFilled: true, isClosed: true);
            ctx.LineTo(new Point(32, 26), isStroked: true, isSmoothJoin: false);
            ctx.LineTo(new Point(62, 38), isStroked: true, isSmoothJoin: false);
        }
        g.Freeze();
        return g;
    }

    private static Pen MakePen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();
        return pen;
    }
}
