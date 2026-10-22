using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Nodes;

/// <summary>
/// 样品杯（Demo 宿主自定义部件）：按 32×48 的 SVG 原样矢量还原——底部半圆的杯体
/// （主色描边 + 白底）与左侧三道刻度线（1.5px 圆头）。
/// 端口：**仅一个**，顶部居中朝上。
/// </summary>
public sealed class SampleCupDefinition : INodeDefinition
{
    // 与 SVG 一致：#3161FF 主色；mask 描边 4px → 视觉 2px，刻度线 1.5px 圆头
    private static readonly Brush Line = NodeRenderer.Frozen(0x31, 0x61, 0xFF);
    private static readonly Pen Outline = NodeRenderer.MakePen(Line, 2.0);
    private static readonly Pen Tick = NodeRenderer.MakePen(Line, 1.5);

    /// <summary>杯体：下部半圆（半径 16）。</summary>
    private const string ShellData =
        "M 0 0 H 32 V 32 C 32 40.8366 24.8366 48 16 48 C 7.16344 48 0 40.8366 0 32 V 0 Z";

    /// <summary>三道刻度线：沿用 SVG 原路径（长度递减）。</summary>
    private static readonly string[] Ticks =
    [
        "M 2 14 H 16",
        "M 2 21 H 13",
        "M 2 28 H 10",
    ];

    public NodeKind Kind => HostNodeKinds.SampleCup;
    public string DisplayName => "样品杯";
    public (double W, double H) BaseSize => (32, 48);

    // 端口：顶部居中一个
    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec("T", 0.5, 0, PortDir.Up),
    ];

    public void Render(DrawingContext dc)
    {
        // 杯体（白底 + 主色描边）
        NodeRenderer.DrawPath(dc, ShellData, NodeRenderer.FillWhite, Outline);

        // 三道刻度线（圆头、无填充）
        foreach (var data in Ticks)
            NodeRenderer.DrawPath(dc, data, null, Tick);
    }
}
