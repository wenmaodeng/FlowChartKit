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

    public NodeKind Kind => DemoNodeKinds.SolenoidValve;

    public string DisplayName => "电磁阀";

    public (double W, double H) BaseSize => (64, 52);

    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec("L", 0, 0.5, PortDir.Left),
        new PortSpec("R", 1, 0.5, PortDir.Right),
    ];

    public void Render(DrawingContext dc)
    {
        // 蝶形阀体（两个顶点相接的三角形）
        dc.DrawGeometry(NodeRenderer.FillWhite, s_outlinePen, s_body);
        // 线圈引线
        dc.DrawLine(s_linePen, new Point(32, 14), new Point(32, 26));
        // 顶部线圈盒
        dc.DrawRectangle(NodeRenderer.FillLight, s_outlinePen, new Rect(22, 2, 20, 12));
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
