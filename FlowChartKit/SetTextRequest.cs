namespace FlowChartKit;

/// <summary>
/// "按编号修改文本内容"命令的参数：宿主 VM 构造后经 <c>ICommand.Execute</c> 传给画布控件，
/// 控件执行后把结果回填到 <see cref="Result"/>。
/// 配合画布控件的 SetTextByNumberCommand 依赖属性 + OneWayToSource 绑定使用，VM 不引用任何控件类型。
/// </summary>
public sealed class SetTextRequest
{
    public SetTextRequest(int number, string content)
    {
        Number = number;
        Content = content;
    }

    /// <summary>文本模块编号，从 1 开始（添加顺序递增，随存档持久化）。</summary>
    public int Number { get; }

    /// <summary>目标文本内容（null 按空字符串处理）。</summary>
    public string Content { get; }

    /// <summary>由处理方（画布控件）回填：找到并修改了该编号的文本模块为 true；未找到为 false；未执行为 null。</summary>
    public bool? Result { get; set; }
}
