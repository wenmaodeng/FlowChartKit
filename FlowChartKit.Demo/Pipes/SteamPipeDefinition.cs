using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Pipes;

/// <summary>
/// 宿主自定义管路示例：蒸汽管——粗红线 + 长虚线快速流动，与内置普通管路形成明显对比。
/// 仅需实现 <see cref="IPipeDefinition"/> 并注册，颜色/粗细/流动画全部自管理。
/// </summary>
public sealed class SteamPipeDefinition : IPipeDefinition
{
    public PipeKind Kind => DemoPipeKinds.Steam;
    public string DisplayName => "蒸汽管";

    public Brush Stroke { get; } = NodeRenderer.Frozen(0xD0, 0x3A, 0x2B);
    public double Thickness => 3.5;

    public Brush SelectedStroke { get; } = NodeRenderer.Frozen(0xFF, 0x8A, 0x00);
    public double SelectedThickness => 4.2;

    // 长虚线段 + 0.3s 一个循环：比内置（0.6s）更快，视觉上"流速更快"
    public PipeFlowSpec Flow { get; } = PipeFlowSpec.Create([6, 3], TimeSpan.FromSeconds(0.3));
}
