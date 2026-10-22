using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Nodes;

/// <summary>
/// 洗针槽（Demo 宿主自定义部件）：按 17×70 的 SVG 原样矢量还原——顶部与上部的浅蓝方块、
/// 下段方形槽体与贯穿的中心竖条（洗针针头）。
/// 端口：**左右各一个**，位于上部宽块的中线高度（接近顶部）。
/// </summary>
public sealed class WashSlotDefinition : INodeDefinition
{
    // 与 SVG 一致：#3161FF 主色描边、#BBCBFF 方块填充；mask 描边 3px → 视觉 1.5px
    private static readonly Brush Line = NodeRenderer.Frozen(0x31, 0x61, 0xFF);
    private static readonly Brush Core = NodeRenderer.Frozen(0xBB, 0xCB, 0xFF);
    private static readonly Pen Outline = NodeRenderer.MakePen(Line, 1.5);
    private static readonly Pen Thin = NodeRenderer.MakePen(Line, 1.0);

    public NodeKind Kind => HostNodeKinds.WashSlot;
    public string DisplayName => "洗针槽";
    public (double W, double H) BaseSize => (17, 70);

    // 左右各一个端口：宽块（y 3.5~8.5）中线高度 6 / 70
    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec("L", 0, 0.0857, PortDir.Left),
        new PortSpec("R", 1, 0.0857, PortDir.Right),
    ];

    public void Render(DrawingContext dc)
    {
        // 顶部小块与上部宽块（浅蓝填充 + 1px 主色描边）
        NodeRenderer.DrawPath(dc, "M 5.5 0.5 H 11.5 V 3.5 H 5.5 V 0.5 Z", Core, Thin);
        NodeRenderer.DrawPath(dc, "M 3.5 3.5 H 13.5 V 8.5 H 3.5 V 3.5 Z", Core, Thin);

        // 下段方形槽体（白底 + 1.5px 主色描边；SVG 里 y32~39 与 y39~70 两段描边完全重叠，合并为一段）
        NodeRenderer.DrawPath(dc, "M 0 32 H 17 V 70 H 0 V 32 Z", NodeRenderer.FillWhite, Outline);

        // 中心竖条（洗针针头，白底 + 1px 主色描边，从宽块伸入槽体）
        NodeRenderer.DrawPath(dc, "M 7.5 8.5 H 9.5 V 55.5 H 7.5 V 8.5 Z", NodeRenderer.FillWhite, Thin);
    }
}
