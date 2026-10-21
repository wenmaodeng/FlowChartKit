using System.Windows;
using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Nodes;

/// <summary>反应杯（Demo 宿主自定义部件，历史上为核心库内置）。</summary>
public sealed class ReactionCupDefinition : INodeDefinition
{
    public NodeKind Kind => DemoNodeKinds.ReactionCup;
    public string DisplayName => "反应杯";
    public (double W, double H) BaseSize => (56, 80);
    public IReadOnlyList<PortSpec> Ports => NodePorts.Standard;

    public void Render(DrawingContext dc)
    {
        NodeRenderer.DrawRoundRect(dc, 16, 6, 24, 68, 4, NodeRenderer.FillLight, NodeRenderer.OutlinePen);
        NodeRenderer.DrawPath(dc, "M20,18 L36,18", null, NodeRenderer.AccentPen);
    }
}
