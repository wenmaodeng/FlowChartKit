using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Nodes;

/// <summary>
/// 反应杯（Demo 宿主自定义部件）：按 39×56 的 SVG 原样矢量还原——底部圆角的杯体
/// （主色描边 + 白底，无其它装饰）。
/// 端口：**仅一个**，顶部居中朝上。
/// </summary>
public sealed class ReactionCupDefinition : INodeDefinition
{
    // 与 SVG 一致：#3161FF 主色描边；mask 描边 3px → 视觉 1.5px
    private static readonly Brush Line = NodeRenderer.Frozen(0x31, 0x61, 0xFF);
    private static readonly Pen Outline = NodeRenderer.MakePen(Line, 1.5);

    /// <summary>杯体：底部圆角（r=4）。</summary>
    private const string ShellData =
        "M 0 0 H 39 V 52 C 39 54.2091 37.2091 56 35 56 H 4 C 1.79086 56 0 54.2091 0 52 V 0 Z";

    public NodeKind Kind => HostNodeKinds.ReactionCup;
    public string DisplayName => "反应杯";
    public (double W, double H) BaseSize => (39, 56);

    // 端口：顶部居中一个
    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec("T", 0.5, 0, PortDir.Up),
    ];

    public void Render(DrawingContext dc)
    {
        // 杯体（白底 + 主色描边，底部圆角）
        NodeRenderer.DrawPath(dc, ShellData, NodeRenderer.FillWhite, Outline);
    }
}
