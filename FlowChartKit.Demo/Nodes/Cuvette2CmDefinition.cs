using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Nodes;

/// <summary>
/// 2cm 比色皿（Demo 宿主自定义部件）：按 25×55 的 SVG 原样矢量还原——底部圆角的比色皿主体
/// （主色描边 + 白底）与右上角的「cm」字样（#303030）。
/// 端口：**仅一个**，顶部居中朝上。
/// </summary>
public sealed class Cuvette2CmDefinition : INodeDefinition
{
    // 与 SVG 一致：#3161FF 主色描边、#303030 文字；mask 描边 3px → 视觉 1.5px
    private static readonly Brush Line = NodeRenderer.Frozen(0x31, 0x61, 0xFF);
    private static readonly Brush Text = NodeRenderer.Frozen(0x30, 0x30, 0x30);
    private static readonly Pen Outline = NodeRenderer.MakePen(Line, 1.5);

    /// <summary>皿体：底部圆角（r=4）。SVG 里 y0~21 与 y21~55 两段描边完全重叠，合并为一段。</summary>
    private const string ShellData =
        "M 0 0 H 25 V 51 C 25 53.2091 23.2091 55 21 55 H 4 C 1.79086 55 0 53.2091 0 51 V 0 Z";

    /// <summary>右上角字形：沿用 SVG 原路径。</summary>
    private const string LabelData =
        "M 16.77 4.72 C 17.66 4.72 18.37 4.93 18.9 5.37 C 19.4 5.78 19.71 6.37 19.84 7.15 H 19.02 " +
        "C 18.91 6.61 18.67 6.2 18.3 5.91 C 17.91 5.6 17.4 5.45 16.77 5.45 C 15.99 5.45 15.38 5.72 14.92 6.27 " +
        "C 14.46 6.81 14.24 7.54 14.24 8.46 C 14.24 9.36 14.46 10.07 14.9 10.59 C 15.37 11.14 16.07 11.42 17 11.42 " +
        "C 17.39 11.42 17.76 11.36 18.12 11.26 C 18.47 11.16 18.77 11.02 19.02 10.86 V 9.04 H 16.78 V 8.33 H 19.83 " +
        "V 11.25 C 19.49 11.53 19.07 11.74 18.58 11.9 C 18.06 12.06 17.51 12.14 16.92 12.14 " +
        "C 15.79 12.14 14.91 11.78 14.29 11.06 C 13.71 10.39 13.42 9.52 13.42 8.46 C 13.42 7.4 13.7 6.53 14.28 5.84 " +
        "C 14.89 5.09 15.72 4.72 16.77 4.72 Z";

    public NodeKind Kind => HostNodeKinds.Cuvette2Cm;
    public string DisplayName => "2cm比色皿";
    public (double W, double H) BaseSize => (25, 55);

    // 端口：顶部居中一个
    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec("T", 0.5, 0, PortDir.Up),
    ];

    public void Render(DrawingContext dc)
    {
        // 皿体（白底 + 主色描边，底部圆角）
        NodeRenderer.DrawPath(dc, ShellData, NodeRenderer.FillWhite, Outline);

        // 右上角字形（深灰实心，无描边）
        NodeRenderer.DrawPath(dc, LabelData, Text, null);
    }
}
