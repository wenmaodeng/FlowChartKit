using System.Windows;
using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Nodes;

/// <summary>样品杯（Demo 宿主自定义部件，历史上为核心库内置）。</summary>
public sealed class SampleCupDefinition : INodeDefinition
{
    public NodeKind Kind => DemoNodeKinds.SampleCup;
    public string DisplayName => "样品杯";
    public (double W, double H) BaseSize => (46, 64);
    public IReadOnlyList<PortSpec> Ports => NodePorts.Standard;

    public void Render(DrawingContext dc)
    {
        NodeRenderer.DrawPath(dc, "M8,6 L8,44 Q8,58 23,58 Q38,58 38,44 L38,6 Z", NodeRenderer.FillLight, NodeRenderer.OutlinePen);
        NodeRenderer.DrawPath(dc, "M14,32 L32,32", null, NodeRenderer.AccentPen);
        NodeRenderer.DrawPath(dc, "M14,42 L32,42", null, NodeRenderer.AccentPen);
    }
}
