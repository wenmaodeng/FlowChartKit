using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Nodes;

/// <summary>
/// 消解锅（Demo 宿主自定义部件）：按 55×55 的 SVG 原样矢量还原——锅体（锅盖把手、双层锅沿、
/// 锅身与内胆挖空）与中心圆环旋钮，均为 #3161FF 实心。
/// 端口：**左右各两个**，位于锅身中部（对应图示两条管路接口位置）。
/// </summary>
public sealed class DigestionPotDefinition : INodeDefinition
{
    // 与 SVG 一致：#3161FF 实心（无描边）
    private static readonly Brush Line = NodeRenderer.Frozen(0x31, 0x61, 0xFF);

    /// <summary>锅体：沿用 SVG 原数据（锅盖把手 / 双层锅沿 / 锅身，内胆由子路径挖空）。</summary>
    private const string PotData =
        "M51.25 10C51.25 7.25 49 5 46.25 5H35C35 2.25 32.625 0 29.875 0H25.125C22.375 0 20.125 2.25 20 5H8.75C6 5 3.75 7.25 3.75 10C1.625 10 0 11.625 0 13.75C0 15.875 1.875 17.5 4 17.5V48.75C4 52.25 6.75 55 10.25 55H45C48.5 55 51 52.25 51 48.75V17.5C53.125 17.5 55 15.875 55 13.75C55 11.625 53.375 10 51.25 10ZM25.125 2.5H29.875C31.25 2.5 32.5 3.625 32.5 5H22.5C22.5 3.625 23.75 2.5 25.125 2.5ZM8.75 7.5H46.25C47.625 7.5 48.75 8.625 48.75 10H6.25C6.25 8.625 7.375 7.5 8.75 7.5ZM48.5 48.75C48.5 50.875 47.125 52.5 45 52.5H10.25C8.125 52.5 6.5 50.875 6.5 48.75V17.5H16.375L17.875 32.75C18.125 35.375 20.25 37.5 22.875 37.5H32.125C34.75 37.5 36.875 35.375 37.125 32.875L38.625 17.5H48.5V48.75ZM18.875 17.5H36.125L34.625 32.625C34.5 34 33.375 35 32.125 35H22.875C21.625 35 20.5 34 20.375 32.625L18.875 17.5ZM51.25 15H3.75C3 15 2.5 14.5 2.5 13.75C2.5 13 3 12.5 3.75 12.5H51.25C52 12.5 52.5 13 52.5 13.75C52.5 14.5 52 15 51.25 15Z";

    /// <summary>中心圆环旋钮：外圆 + 内圆挖空（沿用 SVG 原数据）。</summary>
    private const string KnobData =
        "M28.5 31.25C31.25 31.25 33.5 29 33.5 26.25C33.5 23.5 31.25 21.25 28.5 21.25C25.75 21.25 23.5 23.5 23.5 26.25C23.5 29 25.75 31.25 28.5 31.25ZM28.5 23.75C29.875 23.75 31 24.875 31 26.25C31 27.625 29.875 28.75 28.5 28.75C27.125 28.75 26 27.625 26 26.25C26 24.875 27.125 23.75 28.5 23.75Z";

    public NodeKind Kind => HostNodeKinds.DigestionPot;
    public string DisplayName => "消解锅";
    public (double W, double H) BaseSize => (55, 55);

    // 左右各两个端口：锅身中部（图示两条管路接口，fy≈0.45 / 0.60）
    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec("L1", 0, 0.45, PortDir.Left),
        new PortSpec("L2", 0, 0.60, PortDir.Left),
        new PortSpec("R1", 1, 0.45, PortDir.Right),
        new PortSpec("R2", 1, 0.60, PortDir.Right),
    ];

    public void Render(DrawingContext dc)
    {
        // 锅体与中心旋钮（均为主色实心，内腔由子路径挖空）
        NodeRenderer.DrawPath(dc, PotData, Line, null);
        NodeRenderer.DrawPath(dc, KnobData, Line, null);
    }
}
