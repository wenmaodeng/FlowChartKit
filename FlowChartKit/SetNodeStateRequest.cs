namespace FlowChartKit;

/// <summary>
/// "按类型与编号设置部件状态"命令的参数：宿主 VM 构造后经 <c>ICommand.Execute</c> 传给画布控件，
/// 控件执行后把结果回填到 <see cref="Result"/>。
/// 配合画布控件的 SetNodeStateByNumberCommand 依赖属性 + OneWayToSource 绑定使用，VM 不引用任何控件类型。
/// 命名与参数风格参照 <see cref="SetDirectionRequest"/>（管路流向桥接）。
/// </summary>
public sealed class SetNodeStateRequest
{
    public SetNodeStateRequest(NodeKind kind, int number, object? state)
    {
        Kind = kind;
        Number = number;
        State = state;
    }

    /// <summary>部件类型。</summary>
    public NodeKind Kind { get; }

    /// <summary>同类部件内的序号，从 1 开始，与画布上的序号徽章一致。</summary>
    public int Number { get; }

    /// <summary>状态参数：语义由宿主自定义，控件不解释，原样写入 <see cref="ViewModels.NodeViewModel.State"/>。</summary>
    public object? State { get; }

    /// <summary>由处理方（画布控件）回填：找到并设置了该部件为 true；未找到为 false；未执行为 null。</summary>
    public bool? Result { get; set; }
}
