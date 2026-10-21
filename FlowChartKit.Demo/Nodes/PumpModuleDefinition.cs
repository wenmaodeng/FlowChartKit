using System.Windows;
using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Nodes;

/// <summary>进样注射泵（Demo 宿主自定义部件，历史上为核心库内置）。</summary>
public sealed class PumpModuleDefinition : INodeDefinition
{
    public NodeKind Kind => DemoNodeKinds.PumpModule;
    public string DisplayName => "进样注射泵";
    public (double W, double H) BaseSize => (130, 320);

    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec("T",  0.50, 0.02, PortDir.Up),
        new PortSpec("L1", 0.00, 0.10, PortDir.Left),
        new PortSpec("L2", 0.00, 0.24, PortDir.Left),
        new PortSpec("R1", 1.00, 0.10, PortDir.Right),
        new PortSpec("R2", 1.00, 0.24, PortDir.Right),
        new PortSpec("B1", 0.30, 1.00, PortDir.Down),
        new PortSpec("B2", 0.45, 1.00, PortDir.Down),
        new PortSpec("B3", 0.60, 1.00, PortDir.Down),
        new PortSpec("B4", 0.75, 1.00, PortDir.Down),
    ];

    public void Render(DrawingContext dc)
    {
        // 外壳
        NodeRenderer.DrawRoundRect(dc, 2, 2, 126, 316, 18, NodeRenderer.FillLight, NodeRenderer.OutlinePen);
        // 多通阀：外圈
        dc.DrawEllipse(NodeRenderer.FillWhite, NodeRenderer.OutlinePen, new Point(65, 64), 30, 30);
        // 8 个接口
        for (int i = 0; i < 8; i++)
        {
            double a = i * Math.PI / 4;
            double cx = 65 + 44 * Math.Cos(a);
            double cy = 64 + 44 * Math.Sin(a);
            dc.DrawEllipse(NodeRenderer.FillLight, NodeRenderer.OutlinePen14, new Point(cx, cy), 6, 6);
        }
        // 内芯
        dc.DrawEllipse(NodeRenderer.PumpCoreFill, NodeRenderer.OutlinePen14, new Point(65, 64), 14, 14);
        // 注射器
        NodeRenderer.DrawRoundRect(dc, 50, 130, 30, 100, 4, NodeRenderer.FillWhite, NodeRenderer.OutlinePen);
        NodeRenderer.DrawPath(dc, "M50,160 L80,160", null, NodeRenderer.AccentPen);
        NodeRenderer.DrawRoundRect(dc, 61, 230, 8, 56, 2, NodeRenderer.PlungerFill, NodeRenderer.OutlinePen12);
        NodeRenderer.DrawRoundRect(dc, 44, 286, 42, 14, 3, NodeRenderer.FillLight, NodeRenderer.OutlinePen);
    }
}
