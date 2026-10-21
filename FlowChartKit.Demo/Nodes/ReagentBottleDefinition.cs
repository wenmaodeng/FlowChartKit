using System.Windows;
using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Nodes;

/// <summary>试剂瓶（锥形瓶）（Demo 宿主自定义部件，历史上为核心库内置）。</summary>
public sealed class ReagentBottleDefinition : INodeDefinition
{
    public NodeKind Kind => DemoNodeKinds.ReagentBottle;
    public string DisplayName => "试剂瓶";
    public (double W, double H) BaseSize => (56, 58);
    public IReadOnlyList<PortSpec> Ports => NodePorts.Standard;

    public void Render(DrawingContext dc)
    {
        NodeRenderer.DrawPath(dc, "M22,4 L22,16 L10,48 Q10,56 18,56 L38,56 Q46,56 46,48 L34,16 L34,4 Z", NodeRenderer.FillLight, NodeRenderer.OutlinePen);
        NodeRenderer.DrawPath(dc, "M15,42 L41,42", null, NodeRenderer.AccentPen);
    }
}
