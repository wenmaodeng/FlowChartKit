using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Nodes;

/// <summary>
/// 边框（Demo 宿主自定义部件）：按 234×148 的 SVG 原样矢量还原——仅一圈圆角虚线框
/// （#B1C4FF，1px，3-3 虚线，圆头圆角），无填充。
/// **没有端口**：作分组/区域框使用，不接入流路。
/// </summary>
public sealed class BorderFrameDefinition : INodeDefinition
{
    // 与 SVG 一致：#B1C4FF 虚线；dash 3 3、圆头圆角
    private static readonly Pen Dash = CreateDashPen();

    private static Pen CreateDashPen()
    {
        var pen = new Pen(NodeRenderer.Frozen(0xB1, 0xC4, 0xFF), 1.0)
        {
            DashStyle = new DashStyle([3, 3], 0),
            LineJoin = PenLineJoin.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };
        pen.Freeze();
        return pen;
    }

    public NodeKind Kind => HostNodeKinds.BorderFrame;
    public string DisplayName => "边框";
    public (double W, double H) BaseSize => (234, 148);

    // 无端口
    public IReadOnlyList<PortSpec> Ports { get; } = [];

    public void Render(DrawingContext dc)
    {
        // 圆角虚线框（r=12，无填充）
        NodeRenderer.DrawRoundRect(dc, 0.5, 0.5, 233, 147, 12, null, Dash);
    }
}
