using System.Windows;
using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Nodes;

/// <summary>
/// 注射泵（Demo 宿主自定义部件）：按 72×281 的 SVG 原样矢量还原——圆角外壳、顶部多通阀盘
/// （外圈 + 内芯 + 四向进出标记与左右耳片）、注射器筒与柱塞。
/// 端口布局：**上 / 下 / 左 / 右各 4 个，共 16 个**（T1~T4、B1~B4、L1~L4、R1~R4）。
/// </summary>
public sealed class SyringePumpDefinition : INodeDefinition
{
    // 与 SVG 一致：#3161FF 主色（描边与实心块）、#BBCBFF 阀盘填充
    private static readonly Brush Line = NodeRenderer.Frozen(0x31, 0x61, 0xFF);
    private static readonly Brush Core = NodeRenderer.Frozen(0xBB, 0xCB, 0xFF);
    private static readonly Pen ShellPen = NodeRenderer.MakePen(Line, 2.0);
    private static readonly Pen LinePen = NodeRenderer.MakePen(Line, 1.0);

    /// <summary>阀盘四向的进出标记（斜置方块）与左右耳片：沿用 SVG 原路径，DrawPath 内部缓存几何。</summary>
    private static readonly string[] ValveMarks =
    [
        "M 48.2335 84.6152 L 54.5974 90.9792 L 56.7188 88.8579 L 50.3548 82.4939 Z",
        "M 46.8975 87.5222 L 51.6901 92.3148 L 53.8115 90.1935 L 49.0189 85.4009 Z",
        "M 17.8271 115.021 L 24.1911 121.385 L 22.0698 123.506 L 15.7058 117.142 Z",
        "M 20.7344 113.685 L 25.527 118.478 L 23.4057 120.599 L 18.6131 115.806 Z",
        "M 17.8281 90.9792 L 24.1921 84.6152 L 22.0708 82.4939 L 15.7068 88.8579 Z",
        "M 20.7354 92.3148 L 25.528 87.5222 L 23.4066 85.4009 L 18.614 90.1935 Z",
        "M 48.2335 121.385 L 54.5974 115.021 L 56.7188 117.142 L 50.3548 123.506 Z",
        "M 46.8985 118.478 L 51.6911 113.685 L 53.8124 115.806 L 49.0198 120.599 Z",
        "M 14.5 107.5 V 98.5 H 11.5 V 107.5 Z",                                  // 左耳片
        "M 17.5 106.389 V 99.6111 H 14.5 V 106.389 Z",
        "M 57.5 107.5 V 98.5 H 60.5 V 107.5 Z",                                  // 右耳片
        "M 54.5 106.389 V 99.6111 L 57.5 99.6111 V 106.389 Z",
    ];

    public NodeKind Kind => DemoNodeKinds.SyringePump;
    public string DisplayName => "注射泵";
    public (double W, double H) BaseSize => (72, 281);

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
        NodeRenderer.DrawRoundRect(dc, 1, 1, 70, 279, 8, NodeRenderer.FillWhite, ShellPen);

        // 注射器筒与活塞底托
        dc.DrawRectangle(NodeRenderer.FillWhite, LinePen, new Rect(26.5, 115.5, 19, 69));
        dc.DrawRectangle(null, LinePen, new Rect(24.5, 184.5, 23, 9));

        // 顶部多通阀盘：外圈 + 内芯（圆心 (36.21,103)，半径 18.5 / 11.5）
        dc.DrawEllipse(Core, LinePen, new Point(36.2129, 103), 18.5, 18.5);
        dc.DrawEllipse(Core, LinePen, new Point(36.2129, 103), 11.5, 11.5);

        // 阀盘顶部两个接口方块
        NodeRenderer.DrawPath(dc, "M 31.7129 81.5 H 40.7129 V 78.5 H 31.7129 Z", NodeRenderer.FillWhite, LinePen);
        NodeRenderer.DrawPath(dc, "M 32.8242 84.5 H 39.602 V 81.5 H 32.8242 Z", NodeRenderer.FillWhite, LinePen);

        // 四向进出标记与左右耳片
        foreach (var data in ValveMarks)
            NodeRenderer.DrawPath(dc, data, NodeRenderer.FillWhite, LinePen);

        // 柱塞与刻度：主色实心块
        dc.DrawRectangle(Line, null, new Rect(34, 121, 4, 55));
        dc.DrawRectangle(Line, null, new Rect(29, 170, 14, 6));
        dc.DrawRectangle(Line, null, new Rect(29, 177, 14, 2));
    }
}
