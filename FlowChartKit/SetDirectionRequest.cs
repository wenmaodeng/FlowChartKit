namespace FlowChartKit;

/// <summary>
/// <see cref="Controls.DiagramCanvasControl.SetDirectionCommand"/> 的命令参数：按编号设置管路流向。
/// 用可写属性的类而不是元组，是为了能在 XAML 里用对象元素语法直接声明。
/// </summary>
public class SetDirectionRequest
{
    /// <summary>管路编号，从 1 开始，与画布上的编号徽章一致。</summary>
    public int Number { get; set; }

    /// <summary>目标流向。</summary>
    public FlowMode Direction { get; set; }
}
