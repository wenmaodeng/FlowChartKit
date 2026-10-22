using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Pipes;

/// <summary>
/// 宿主自定义管路示例：热水汽管——2px 的 #25F7D9 青绿线，
/// 流动画沿用内置节奏（{4,4} / 0.6s）。仅需实现 <see cref="IPipeDefinition"/> 并注册。
/// </summary>
public sealed class HotSteamPipeDefinition : IPipeDefinition
{
    public PipeKind Kind => HostPipeKinds.HotSteam;
    public string DisplayName => "热水汽管";

    public Brush Stroke { get; } = NodeRenderer.Frozen(0x25, 0xF7, 0xD9);
    public double Thickness => 2.0;

    // 选中态：同色系加深 + 略粗
    public Brush SelectedStroke { get; } = NodeRenderer.Frozen(0x11, 0xB7, 0x9C);
    public double SelectedThickness => 2.6;

    public PipeFlowSpec Flow { get; } = PipeFlowSpec.Create([4, 4], TimeSpan.FromSeconds(0.6));
}
