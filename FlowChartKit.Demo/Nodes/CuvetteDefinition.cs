using System.Windows;
using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Nodes;

/// <summary>2cm比色皿（Demo 宿主自定义部件，历史上为核心库内置）。</summary>
public sealed class CuvetteDefinition : INodeDefinition
{
    public NodeKind Kind => DemoNodeKinds.Cuvette;
    public string DisplayName => "2cm比色皿";
    public (double W, double H) BaseSize => (46, 74);
    public IReadOnlyList<PortSpec> Ports => NodePorts.Standard;

    // "G" 字样：一次性转为矢量轮廓并冻结——分辨率无关（高 DPI 不糊），且免每次渲染构造 FormattedText
    private static readonly Geometry s_glyph = NodeRenderer.BuildGlyph("G", 12.0, new Point(18, 28));

    public void Render(DrawingContext dc)
    {
        NodeRenderer.DrawRoundRect(dc, 16, 2, 14, 10, 1, NodeRenderer.FillLight, NodeRenderer.OutlinePen);
        NodeRenderer.DrawRoundRect(dc, 8, 12, 30, 56, 2, NodeRenderer.FillLight, NodeRenderer.OutlinePen);
        dc.DrawGeometry(NodeRenderer.Outline, null, s_glyph);
    }
}
