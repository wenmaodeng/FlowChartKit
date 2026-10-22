using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Nodes;

/// <summary>
/// 3cm 比色皿（Demo 宿主自定义部件）：按 58×61 的 SVG 原样矢量还原——底部圆角的比色皿主体
/// （主色描边 + 白底）与右上角的「3cm」字样（#303030）。
/// 端口：**仅一个**，顶部居中朝上。
/// </summary>
public sealed class Cuvette3CmDefinition : INodeDefinition
{
    // 与 SVG 一致：#3161FF 主色描边、#303030 文字；mask 描边 3px → 视觉 1.5px
    private static readonly Brush Line = NodeRenderer.Frozen(0x31, 0x61, 0xFF);
    private static readonly Brush Text = NodeRenderer.Frozen(0x30, 0x30, 0x30);
    private static readonly Pen Outline = NodeRenderer.MakePen(Line, 1.5);

    /// <summary>皿体：底部圆角（r=4）。SVG 里 y0~21 与 y21~61 两段描边完全重叠，合并为一段。</summary>
    private const string ShellData =
        "M 0 0 H 58 V 57 C 58 59.2091 56.2091 61 54 61 H 4 C 1.79086 61 0 59.2091 0 57 V 0 Z";

    /// <summary>右上角「3cm」字形：沿用 SVG 原路径（与 1cm 版同形，整体右移 30px）。</summary>
    private const string LabelData =
        "M 46.77 4.72 C 47.66 4.72 48.37 4.93 48.9 5.37 C 49.4 5.78 49.71 6.37 49.84 7.15 H 49.02 " +
        "C 48.91 6.61 48.67 6.2 48.3 5.91 C 47.91 5.6 47.4 5.45 46.77 5.45 C 45.99 5.45 45.38 5.72 44.92 6.27 " +
        "C 44.46 6.81 44.24 7.54 44.24 8.46 C 44.24 9.36 44.46 10.07 44.9 10.59 C 45.37 11.14 46.07 11.42 47 11.42 " +
        "C 47.39 11.42 47.76 11.36 48.12 11.26 C 48.47 11.16 48.77 11.02 49.02 10.86 V 9.04 H 46.78 V 8.33 H 49.83 " +
        "V 11.25 C 49.49 11.53 49.07 11.74 48.58 11.9 C 48.06 12.06 47.51 12.14 46.92 12.14 " +
        "C 45.79 12.14 44.91 11.78 44.29 11.06 C 43.71 10.39 43.42 9.52 43.42 8.46 C 43.42 7.4 43.7 6.53 44.28 5.84 " +
        "C 44.89 5.09 45.72 4.72 46.77 4.72 Z";

    public NodeKind Kind => HostNodeKinds.Cuvette3Cm;
    public string DisplayName => "3cm比色皿";
    public (double W, double H) BaseSize => (58, 61);

    // 端口：顶部居中一个
    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec("T", 0.5, 0, PortDir.Up),
    ];

    public void Render(DrawingContext dc)
    {
        // 皿体（白底 + 主色描边，底部圆角）
        NodeRenderer.DrawPath(dc, ShellData, NodeRenderer.FillWhite, Outline);

        // 右上角「3cm」字样（深灰实心，无描边）
        NodeRenderer.DrawPath(dc, LabelData, Text, null);
    }
}
