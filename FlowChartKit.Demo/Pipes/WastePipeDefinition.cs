using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Pipes;

/// <summary>
/// 宿主自定义管路示例：废液管——灰色、不流动（<see cref="PipeFlowSpec.None"/>）。
/// 用于验证"即使设置了流向也恒为实线"的类型：选中它设置流向后仍不会启动流动动画。
/// </summary>
public sealed class WastePipeDefinition : IPipeDefinition
{
    public PipeKind Kind => DemoPipeKinds.Waste;
    public string DisplayName => "废液管";

    public Brush Stroke { get; } = NodeRenderer.Frozen(0x8A, 0x8F, 0x98);
    public double Thickness => 2.2;

    public Brush SelectedStroke { get; } = NodeRenderer.Frozen(0x5A, 0x60, 0x6A);
    public double SelectedThickness => 2.8;

    // 不流动：流向仅影响箭头/徽章，管路恒为实线
    public PipeFlowSpec Flow { get; } = PipeFlowSpec.None;
}
