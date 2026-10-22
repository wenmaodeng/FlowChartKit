using System.Windows;
using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Nodes;

/// <summary>
/// 注射泵2（Demo 宿主自定义部件）：按 72×200 的 SVG 原样矢量还原——圆角外壳、顶部多通阀盘
/// （外圈 + 内芯 + 四向进出标记与左右耳片）、注射器筒与柱塞。
/// 端口布局：**上 / 下 / 左 / 右各 4 个，共 16 个**（T1~T4、B1~B4、L1~L4、R1~R4）。
/// </summary>
public sealed class SyringePump2Definition : INodeDefinition
{
    // 与 SVG 一致：#3161FF 主色（描边与实心块）、#B1C4FF 阀盘填充
    private static readonly Brush Line = NodeRenderer.Frozen(0x31, 0x61, 0xFF);
    private static readonly Brush Core = NodeRenderer.Frozen(0xB1, 0xC4, 0xFF);
    private static readonly Pen ShellPen = NodeRenderer.MakePen(Line, 2.0);
    private static readonly Pen LinePen = NodeRenderer.MakePen(Line, 1.0);

    /// <summary>阀盘四向的进出标记（斜置方块）与左右耳片：沿用 SVG 原路径，DrawPath 内部缓存几何。
    /// 原 SVG 中这 12 条路径重复了一遍，去重后只绘一次（视觉一致）。</summary>
    private static readonly string[] ValveMarks =
    [
        // 右上 / 右下 标记
        "M 48.2335 48.6152 L 54.5974 54.9792 L 56.7188 52.8579 L 50.3548 46.4939 Z",
        "M 46.8975 51.5225 L 51.6901 56.3151 L 53.8115 54.1938 L 49.0189 49.4011 Z",
        // 左下 / 左上 标记
        "M 17.8281 79.0206 L 24.1921 85.3845 L 22.0708 87.5059 L 15.7068 81.1419 Z",
        "M 20.7344 77.6856 L 25.527 82.4782 L 23.4057 84.5996 L 18.6131 79.8069 Z",
        "M 17.8281 54.9794 L 24.1921 48.6155 L 22.0708 46.4941 L 15.7068 52.8581 Z",
        "M 20.7354 56.3154 L 25.528 51.5227 L 23.4066 49.4014 L 18.614 54.194 Z",
        // 右上 / 右下 另一向标记
        "M 48.2335 85.3848 L 54.5974 79.0208 L 56.7188 81.1421 L 50.3548 87.5061 Z",
        "M 46.8985 82.4785 L 51.6911 77.6859 L 53.8124 79.8072 L 49.0198 84.5998 Z",
        // 左耳片
        "M 14.5 71.5 V 62.5 H 11.5 V 71.5 Z",
        "M 17.5 70.3887 V 63.6109 H 14.5 V 70.3887 Z",
        // 右耳片
        "M 57.5 71.5 V 62.5 H 60.5 V 71.5 Z",
        "M 54.5 70.3887 V 63.6109 L 57.5 63.6109 V 70.3887 Z",
    ];

    public NodeKind Kind => HostNodeKinds.SyringePump2;
    public string DisplayName => "注射泵2";
    public (double W, double H) BaseSize => (72, 200);

    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        // 上边 4 个（自左而右）
        new PortSpec("T1", 0.20, 0.00, PortDir.Up),
        new PortSpec("T2", 0.40, 0.00, PortDir.Up),
        new PortSpec("T3", 0.60, 0.00, PortDir.Up),
        new PortSpec("T4", 0.80, 0.00, PortDir.Up),
        // 下边 4 个（自左而右）
        new PortSpec("B1", 0.20, 1.00, PortDir.Down),
        new PortSpec("B2", 0.40, 1.00, PortDir.Down),
        new PortSpec("B3", 0.60, 1.00, PortDir.Down),
        new PortSpec("B4", 0.80, 1.00, PortDir.Down),
        // 左边 4 个（自上而下）
        new PortSpec("L1", 0.00, 0.20, PortDir.Left),
        new PortSpec("L2", 0.00, 0.40, PortDir.Left),
        new PortSpec("L3", 0.00, 0.60, PortDir.Left),
        new PortSpec("L4", 0.00, 0.80, PortDir.Left),
        // 右边 4 个（自上而下）
        new PortSpec("R1", 1.00, 0.20, PortDir.Right),
        new PortSpec("R2", 1.00, 0.40, PortDir.Right),
        new PortSpec("R3", 1.00, 0.60, PortDir.Right),
        new PortSpec("R4", 1.00, 0.80, PortDir.Right),
    ];

    public void Render(DrawingContext dc)
    {
        // 外壳：圆角矩形（r=8，2px 主色描边，与原 SVG 一致）
        NodeRenderer.DrawRoundRect(dc, 1, 1, 70, 198, 8, NodeRenderer.FillWhite, ShellPen);

        // 注射器筒与活塞底托
        dc.DrawRectangle(NodeRenderer.FillWhite, LinePen, new Rect(26.5, 79.5, 19, 69));
        dc.DrawRectangle(null, LinePen, new Rect(24.5, 148.5, 23, 9));

        // 顶部多通阀盘：外圈 + 内芯（圆心 (36.2129,67)，半径 18.5 / 11.5）
        dc.DrawEllipse(Core, LinePen, new Point(36.2129, 67), 18.5, 18.5);
        dc.DrawEllipse(Core, LinePen, new Point(36.2129, 67), 11.5, 11.5);

        // 阀盘顶部两个接口方块
        NodeRenderer.DrawPath(dc, "M 31.7129 45.5 H 40.7129 V 42.5 H 31.7129 Z", NodeRenderer.FillWhite, LinePen);
        NodeRenderer.DrawPath(dc, "M 32.8242 48.5 H 39.602 V 45.5 H 32.8242 Z", NodeRenderer.FillWhite, LinePen);

        // 四向进出标记与左右耳片
        foreach (var data in ValveMarks)
            NodeRenderer.DrawPath(dc, data, NodeRenderer.FillWhite, LinePen);

        // 柱塞与刻度：主色实心块
        dc.DrawRectangle(Line, null, new Rect(34, 85, 4, 55));
        dc.DrawRectangle(Line, null, new Rect(29, 134, 14, 6));
        dc.DrawRectangle(Line, null, new Rect(29, 141, 14, 2));
    }
}
