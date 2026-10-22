using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Pipes;

/// <summary>
/// 宿主自定义管路示例：热水汽管——配色与内置普通管路一致（有流向 #3161FF、停止 #B1C4FF、
/// 选中 #E67E22），2.0px / 选中 2.6px，流动画沿用内置节奏（{4,4} / 0.6s）。
/// 仅需实现 <see cref="IPipeDefinition"/> 并注册。
/// </summary>
public sealed class HotSteamPipeDefinition : IPipeDefinition
{
    public PipeKind Kind => HostPipeKinds.HotSteam;
    public string DisplayName => "热水汽管";

    // 有流向（正向 / 反向）
    public Brush Stroke { get; } = NodeRenderer.Frozen(0x31, 0x61, 0xFF);
    public double Thickness => 2.0;

    // 流向停止（与内置普通管路同色）
    public Brush StoppedStroke { get; } = NodeRenderer.Frozen(0xB1, 0xC4, 0xFF);
    public double StoppedThickness => 2.0;

    // 选中态与内置一致
    public Brush SelectedStroke { get; } = NodeRenderer.Frozen(0xE6, 0x7E, 0x22);
    public double SelectedThickness => 2.6;

    public PipeFlowSpec Flow { get; } = PipeFlowSpec.Create([4, 4], TimeSpan.FromSeconds(0.6));
}
