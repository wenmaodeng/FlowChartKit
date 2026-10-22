using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Nodes;

/// <summary>
/// 试剂瓶（Demo 宿主自定义部件）：按 30×30 的 SVG 原样矢量还原——实心主色的瓶身
/// （瓶塞、瓶颈与锥形瓶体，内部由子路径挖空）。
/// 端口：**仅一个**，顶部居中朝上。
/// </summary>
public sealed class ReagentBottleDefinition : INodeDefinition
{
    // 与 SVG 一致：#3161FF 实心主色（无描边）
    private static readonly Brush Line = NodeRenderer.Frozen(0x31, 0x61, 0xFF);

    /// <summary>瓶身：单条路径含三段子路径（外轮廓 + 瓶口/瓶底内腔挖空），沿用 SVG 原数据。</summary>
    private const string BottleData =
        "M 29.5935 25.0232 L 29.6191 24.9792 L 20.6213 9.36035 V 1.87502 H 21.2475 " +
        "C 21.936 1.87502 22.4963 1.45386 22.4963 0.937495 C 22.4963 0.421131 21.936 0 21.2475 0 H 8.74879 " +
        "C 8.06032 0 7.50003 0.421131 7.50003 0.937495 C 7.50003 1.45386 8.06035 1.87499 8.74879 1.87499 H 9.37505 " +
        "V 9.37869 L 0.531026 24.7668 L 0.549332 24.7998 C 0.234396 25.3271 0 25.9351 0 26.6162 " +
        "C 0 28.4875 1.36964 30 3.06154 30 H 27.2607 C 28.7915 29.8169 30 28.3594 30 26.6162 " +
        "C 30 26.0303 29.8279 25.503 29.5935 25.0232 Z " +
        "M 18.75 1.87502 V 9.36035 C 18.75 9.68995 18.8379 10.0122 18.999 10.2978 L 20.6287 13.1287 H 9.38233 " +
        "L 10.9974 10.3162 C 11.1584 10.0305 11.2464 9.70826 11.2464 9.37866 V 1.87499 L 18.75 1.87502 Z " +
        "M 27.1142 28.125 H 3.06157 C 2.41703 28.125 1.87505 27.4329 1.87505 26.6126 " +
        "C 1.87505 26.3526 1.96658 26.0779 2.15701 25.7557 C 2.2156 25.6568 2.26321 25.5469 2.30716 25.437 " +
        "L 8.30932 15 H 21.709 L 27.8064 25.5908 C 27.836 25.6839 27.8728 25.7744 27.9163 25.8618 " +
        "C 28.0591 26.1512 28.125 26.3892 28.125 26.6162 C 28.1213 27.4292 27.6123 28.0115 27.1142 28.125 Z";

    public NodeKind Kind => HostNodeKinds.ReagentBottle;
    public string DisplayName => "试剂瓶";
    public (double W, double H) BaseSize => (30, 30);

    // 端口：顶部居中一个
    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec("T", 0.5, 0, PortDir.Up),
    ];

    public void Render(DrawingContext dc)
    {
        NodeRenderer.DrawPath(dc, BottleData, Line, null);
    }
}
