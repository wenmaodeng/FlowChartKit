using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Nodes;

/// <summary>
/// 样品位（Demo 宿主自定义部件）：按 14×71 的 SVG 原样矢量还原——顶部与上部的浅蓝方块、
/// 中部颈块、下段圆底试管与贯穿的中心竖条（采样针/芯）。
/// 端口：**左右各一个**，位于上部宽块的中线高度（接近顶部）。
/// </summary>
public sealed class SampleSlotDefinition : INodeDefinition
{
    // 与 SVG 一致：#3161FF 主色描边、#BBCBFF 方块填充；mask 描边 3px → 视觉 1.5px
    private static readonly Brush Line = NodeRenderer.Frozen(0x31, 0x61, 0xFF);
    private static readonly Brush Core = NodeRenderer.Frozen(0xBB, 0xCB, 0xFF);
    private static readonly Pen Outline = NodeRenderer.MakePen(Line, 1.5);
    private static readonly Pen Thin = NodeRenderer.MakePen(Line, 1.0);

    /// <summary>下段试管：底部圆角（r=4）。SVG 里 y26~37 与 y37~71 两段描边完全重叠，合并为一段。</summary>
    private const string TubeData =
        "M 0 26 H 14 V 67 C 14 69.2091 12.2091 71 10 71 H 4 C 1.79086 71 0 69.2091 0 67 V 26 Z";

    public NodeKind Kind => HostNodeKinds.SampleSlot;
    public string DisplayName => "样品位";
    public (double W, double H) BaseSize => (14, 71);

    // 左右各一个端口：宽块（y 3.5~9.5）中线高度 6.5 / 71
    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec("L", 0, 0.0915, PortDir.Left),
        new PortSpec("R", 1, 0.0915, PortDir.Right),
    ];

    public void Render(DrawingContext dc)
    {
        // 顶部小块与上部宽块（浅蓝填充 + 1px 主色描边）
        NodeRenderer.DrawPath(dc, "M 2.5 0.5 H 11.5 V 3.5 H 2.5 V 0.5 Z", Core, Thin);
        NodeRenderer.DrawPath(dc, "M 0.5 3.5 H 13.5 V 9.5 H 0.5 V 3.5 Z", Core, Thin);

        // 下段试管（白底 + 1.5px 主色描边，底部圆角）
        NodeRenderer.DrawPath(dc, TubeData, NodeRenderer.FillWhite, Outline);

        // 中部颈块（白底 + 1.5px 主色描边）
        NodeRenderer.DrawPath(dc, "M 1 21 H 13 V 27 H 1 V 21 Z", NodeRenderer.FillWhite, Outline);

        // 中心竖条（采样针/芯，白底 + 1px 主色描边，从宽块伸入试管）
        NodeRenderer.DrawPath(dc, "M 6.39453 9.64395 H 7.60506 V 56.5152 H 6.39453 V 9.64395 Z",
            NodeRenderer.FillWhite, Thin);
    }
}
