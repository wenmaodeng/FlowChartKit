using System.Windows;
using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Nodes;

/// <summary>离心位（Demo 宿主自定义部件，历史上为核心库内置）。</summary>
public sealed class CentrifugeDefinition : INodeDefinition
{
    public NodeKind Kind => DemoNodeKinds.Centrifuge;
    public string DisplayName => "离心位";
    public (double W, double H) BaseSize => (56, 80);
    public IReadOnlyList<PortSpec> Ports => NodePorts.Standard;

    public void Render(DrawingContext dc)
    {
        NodeRenderer.DrawRoundRect(dc, 8, 4, 40, 72, 6, NodeRenderer.FillLight, NodeRenderer.OutlinePen);
        NodeRenderer.DrawPath(dc, "M8,22 L48,22", null, NodeRenderer.OutlinePen14);
        dc.DrawEllipse(NodeRenderer.FillWhite, NodeRenderer.AccentPen14, new Point(28, 48), 8, 8);
    }
}
