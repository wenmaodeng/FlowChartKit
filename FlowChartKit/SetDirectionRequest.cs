namespace FlowChartKit;

/// <summary>
/// "按编号设置流向"命令的参数：宿主 VM 构造后经 <c>ICommand.Execute</c> 传给画布控件，
/// 控件执行后把结果回填到 <see cref="Result"/>。
/// 配合画布控件的 SetDirectionByNumberCommand 依赖属性 + OneWayToSource 绑定使用，VM 不引用任何控件类型。
/// </summary>
public sealed class SetDirectionRequest
{
    public SetDirectionRequest(int number, FlowMode direction)
    {
        Number = number;
        Direction = direction;
    }

    /// <summary>管路编号，从 1 开始，与画布上的编号徽章一致。</summary>
    public int Number { get; }

    /// <summary>目标流向。</summary>
    public FlowMode Direction { get; }

    /// <summary>由处理方（画布控件）回填：找到并设置了该编号的管路为 true；未找到为 false；未执行为 null。</summary>
    public bool? Result { get; set; }
}
