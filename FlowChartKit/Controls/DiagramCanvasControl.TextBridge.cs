using System.Windows;
using System.Windows.Input;
using FlowChartKit.Core;
using FlowChartKit.ViewModels;

namespace FlowChartKit.Controls;

/// <summary>
/// <see cref="DiagramCanvasControl"/> 的"修改文本内容"桥接部分：
/// 控件把实现好的 <see cref="SetTextByNumberCommand"/> 作为依赖属性暴露，
/// 宿主 VM 用 OneWayToSource 绑定取走后自行调用——VM 只依赖 <see cref="ICommand"/>
/// 与 <see cref="SetTextRequest"/>，完全不引用控件类型。
/// 命名为 ByNumber 以区别于图模型上作用于选中对象的编辑入口。
/// </summary>
public partial class DiagramCanvasControl
{
    public static readonly DependencyProperty SetTextByNumberCommandProperty = DependencyProperty.Register(
        nameof(SetTextByNumberCommand), typeof(ICommand), typeof(DiagramCanvasControl),
        new PropertyMetadata(null));

    /// <summary>
    /// 按编号修改文本模块内容的命令（控件内置实现，Loaded 时赋值；请勿在外部替换）。
    /// 宿主 VM 以 <c>OneWayToSource</c> 绑定取走，参数传 <see cref="SetTextRequest"/>，
    /// 执行结果回填到 <see cref="SetTextRequest.Result"/>。
    /// </summary>
    public ICommand? SetTextByNumberCommand
    {
        get => (ICommand?)GetValue(SetTextByNumberCommandProperty);
        set => SetValue(SetTextByNumberCommandProperty, value);
    }

    private void ExecuteSetText(object? parameter)
    {
        if (parameter is SetTextRequest request)
            request.Result = SetTextContent(request.Number, request.Content);
    }

    /// <summary>
    /// 按编号修改文本模块内容。由 <see cref="ExecuteSetText"/> 在 VM 执行命令时调用，
    /// 也可被宿主代码直接调用。编号与添加顺序一致，从 1 开始，随存档持久化。
    /// </summary>
    /// <returns>找到并修改了该编号的文本模块时为 true；编号不存在时为 false。</returns>
    public bool SetTextContent(int number, string content)
    {
        if (!Diagram.SetTextContent(number, content))
        {
            StatusChanged?.Invoke($"未找到编号为 {number} 的文本模块");
            return false;
        }

        StatusChanged?.Invoke($"文本 #{number} 已更新");
        return true;
    }
}
