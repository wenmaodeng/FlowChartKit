using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Pipes;

/// <summary>
/// 宿主自定义管路示例：采样管——细绿线 + 稀疏虚线慢速流动（{2,6}、1.2s 一个循环）。
/// 用于验证虚线图案与速度均可自定义（短划、长间隔、流速慢）。
/// </summary>
public sealed class SamplePipeDefinition : IPipeDefinition
{
    public PipeKind Kind => DemoPipeKinds.Sample;
    public string DisplayName => "采样管";

    public Brush Stroke { get; } = NodeRenderer.Frozen(0x2E, 0x9E, 0x5B);
    public double Thickness => 1.4;

    public Brush SelectedStroke { get; } = NodeRenderer.Frozen(0x1B, 0x7A, 0x44);
    public double SelectedThickness => 2.0;

    // 短划 + 长间隔、1.2s 一个循环：比内置更"慢"的流动
    public PipeFlowSpec Flow { get; } = PipeFlowSpec.Create([2, 6], TimeSpan.FromSeconds(1.2));
}
