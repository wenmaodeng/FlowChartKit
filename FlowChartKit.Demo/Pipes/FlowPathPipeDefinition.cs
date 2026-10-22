using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Pipes;

/// <summary>
/// 宿主自定义管路示例：流路——2px 的 #3161FF 蓝线（与 Demo 注射泵等部件同色系），
/// 流动画沿用内置节奏（{4,4} / 0.6s）。仅需实现 <see cref="IPipeDefinition"/> 并注册。
/// </summary>
public sealed class FlowPathPipeDefinition : IPipeDefinition
{
    public PipeKind Kind => DemoPipeKinds.FlowPath;
    public string DisplayName => "流路";

    public Brush Stroke { get; } = NodeRenderer.Frozen(0x31, 0x61, 0xFF);
    public double Thickness => 2.0;

    // 选中态：同色系加深 + 略粗
    public Brush SelectedStroke { get; } = NodeRenderer.Frozen(0x1B, 0x47, 0xB8);
    public double SelectedThickness => 2.6;

    public PipeFlowSpec Flow { get; } = PipeFlowSpec.Create([4, 4], TimeSpan.FromSeconds(0.6));
}
