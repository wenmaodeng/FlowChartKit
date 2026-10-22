using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Nodes;

/// <summary>
/// 废液桶（Demo 宿主自定义部件）：按 46×56 的 SVG 原样矢量还原——上下宽、中段略窄的桶身
/// （主色描边 + 白底）、顶部接口方块与桶内两道液体波纹。
/// 端口：**仅一个**，位于顶部接口方块（瓶盖）的中心朝上。
/// </summary>
public sealed class WasteBucketDefinition : INodeDefinition
{
    // 与 SVG 一致：#3161FF 主色；外框 mask 描边 3px → 视觉 1.5px 内描边
    private static readonly Brush Line = NodeRenderer.Frozen(0x31, 0x61, 0xFF);
    private static readonly Pen Outline = NodeRenderer.MakePen(Line, 1.5);
    private static readonly Pen Wave = NodeRenderer.MakePen(Line, 2.0);

    /// <summary>桶身外框：上下两段全宽圆角块 + 中段收窄（y 19~39 处内缩 2.76px）。</summary>
    private const string ShellData =
        "M 44 5 C 45.1046 5 46 5.89543 46 7 V 17 C 46 18.1046 45.1046 19 44 19 H 43.7402 " +
        "C 43.4641 19 43.2402 19.2239 43.2402 19.5 V 38.5 C 43.2402 38.7761 43.4641 39 43.7402 39 H 44 " +
        "C 45.1046 39 46 39.8954 46 41 V 54 C 46 55.1046 45.1046 56 44 56 H 2 " +
        "C 0.895431 56 0 55.1046 0 54 V 41 C 0 39.8954 0.895431 39 2 39 H 2.25977 " +
        "C 2.53591 39 2.75977 38.7761 2.75977 38.5 V 19.5 C 2.75977 19.2239 2.53591 19 2.25977 19 H 2 " +
        "C 0.895431 19 0 18.1046 0 17 V 7 C 0 5.89543 0.895431 5 2 5 H 44 Z";

    /// <summary>桶内两道液体波纹（圆头，stroke 2px）。</summary>
    private static readonly string[] Waves =
    [
        "M 10 28.2768 C 10 28.2768 12.3469 30.0982 16.5714 30.0982 " +
        "C 20.7959 30.0982 24.0556 25.9993 28.3061 26 C 33 26.0008 34.7211 27.8209 36.2857 28.7313",
        "M 10 36.4732 C 10 36.4732 12.3469 38.2947 16.5714 38.2947 " +
        "C 20.7959 38.2947 24.0556 34.1957 28.3061 34.1964 C 33 34.1973 34.7211 36.0173 36.2857 36.9278",
    ];

    public NodeKind Kind => HostNodeKinds.WasteBucket;
    public string DisplayName => "废液桶";
    public (double W, double H) BaseSize => (46, 56);

    // 端口在瓶盖（顶部接口方块 x 27.75~39.25）的中心：33.5 / 46
    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec("T", 0.7283, 0, PortDir.Up),
    ];

    public void Render(DrawingContext dc)
    {
        // 桶身（白底 + 主色描边）
        NodeRenderer.DrawPath(dc, ShellData, NodeRenderer.FillWhite, Outline);

        // 顶部接口方块（瓶盖，无填充描边 1.5px）
        NodeRenderer.DrawPath(dc, "M 27.75 0.75 H 39.25 V 5.25 H 27.75 V 0.75 Z", null, Outline);

        // 两道液体波纹
        foreach (var data in Waves)
            NodeRenderer.DrawPath(dc, data, null, Wave);
    }
}
