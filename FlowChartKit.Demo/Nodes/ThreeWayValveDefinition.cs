using System.Windows;
using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Nodes;

/// <summary>三通阀（Demo 宿主自定义部件，历史上为核心库内置）。</summary>
public sealed class ThreeWayValveDefinition : INodeDefinition
{
    public NodeKind Kind => DemoNodeKinds.ThreeWayValve;
    public string DisplayName => "三通阀";
    public (double W, double H) BaseSize => (56, 52);
    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec("L", 0,   0.5, PortDir.Left),
        new PortSpec("R", 1,   0.5, PortDir.Right),
        new PortSpec("B", 0.5, 1,   PortDir.Down),
    ];

    public void Render(DrawingContext dc)
    {
        // 圆形阀体
        dc.DrawEllipse(NodeRenderer.FillWhite, NodeRenderer.OutlinePen, new Point(28, 26), 16, 16);
        // 内部 Y 形流道指示（左/右/下三个端口方向）
        NodeRenderer.DrawPath(dc, "M14,26 L28,26 L42,26", null, NodeRenderer.AccentPen);
        NodeRenderer.DrawPath(dc, "M28,26 L28,40", null, NodeRenderer.AccentPen);
    }
}
