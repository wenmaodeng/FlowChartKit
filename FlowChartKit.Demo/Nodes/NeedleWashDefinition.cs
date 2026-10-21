using System.Windows;
using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Nodes;

/// <summary>洗针位（Demo 宿主自定义部件，历史上为核心库内置）。</summary>
public sealed class NeedleWashDefinition : INodeDefinition
{
    public NodeKind Kind => DemoNodeKinds.NeedleWash;
    public string DisplayName => "洗针位";
    public (double W, double H) BaseSize => (46, 84);
    public IReadOnlyList<PortSpec> Ports => NodePorts.Standard;

    public void Render(DrawingContext dc)
    {
        NodeRenderer.DrawPath(dc, "M23,10 L23,46", null, NodeRenderer.AccentPen2);
        NodeRenderer.DrawRoundRect(dc, 15, 2, 16, 8, 2, NodeRenderer.FillLight, NodeRenderer.OutlinePen);
        NodeRenderer.DrawRoundRect(dc, 8, 46, 30, 32, 4, NodeRenderer.FillLight, NodeRenderer.OutlinePen);
    }
}
