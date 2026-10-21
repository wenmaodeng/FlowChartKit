using System.Windows;
using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Nodes;

/// <summary>废液桶（Demo 宿主自定义部件，历史上为核心库内置）。</summary>
public sealed class WasteDefinition : INodeDefinition
{
    public NodeKind Kind => DemoNodeKinds.Waste;
    public string DisplayName => "废液";
    public (double W, double H) BaseSize => (64, 76);
    public IReadOnlyList<PortSpec> Ports => NodePorts.Standard;

    public void Render(DrawingContext dc)
    {
        NodeRenderer.DrawRoundRect(dc, 22, 0, 20, 10, 2, NodeRenderer.FillLight, NodeRenderer.OutlinePen);
        NodeRenderer.DrawRoundRect(dc, 8, 10, 48, 62, 6, NodeRenderer.FillLight, NodeRenderer.OutlinePen);
        NodeRenderer.DrawPath(dc, "M14,38 q5,-6 10,0 t10,0 t10,0", null, NodeRenderer.AccentPen14);
        NodeRenderer.DrawPath(dc, "M14,52 q5,-6 10,0 t10,0 t10,0", null, NodeRenderer.AccentPen14);
    }
}
