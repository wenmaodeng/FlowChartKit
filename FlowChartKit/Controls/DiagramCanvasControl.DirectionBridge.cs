using System.Windows;

namespace FlowChartKit.Controls;

/// <summary>
/// <see cref="DiagramCanvasControl"/> 的宿主桥接部分：
/// 宿主 ViewModel 实现 <see cref="IFlowDirectionRequester"/> 并触发
/// <see cref="IFlowDirectionRequester.SetDirectionRequested"/> 事件，控件订阅后调用
/// <see cref="SetConnectionDirection"/> 落到图模型——VM 完全不引用控件类型。
/// </summary>
public partial class DiagramCanvasControl
{
    /// <summary>当前订阅了设置流向事件的宿主请求方（DataContext 实现 <see cref="IFlowDirectionRequester"/> 时）。</summary>
    private IFlowDirectionRequester? _requester;

    /// <summary>
    /// 按管路编号设置流向。由 <see cref="OnSetDirectionRequested"/> 在收到 VM 的
    /// <see cref="IFlowDirectionRequester.SetDirectionRequested"/> 事件时调用，也可被宿主代码直接调用。
    /// 编号与画布上的编号徽章一致，从 1 开始。
    /// </summary>
    /// <returns>找到并设置了该编号的管路时为 true；编号不存在时为 false。</returns>
    public bool SetConnectionDirection(int number, FlowMode direction)
    {
        var connection = Diagram.Connections.FirstOrDefault(c => c.Number == number);
        if (connection is null)
        {
            StatusChanged?.Invoke($"未找到编号为 {number} 的管路");
            return false;
        }

        connection.Direction = direction;
        StatusChanged?.Invoke($"管路 #{number} 已设为{FlowModeDefaults.DisplayName(direction)}");
        return true;
    }

    /// <summary>DataContext 变为实现 <see cref="IFlowDirectionRequester"/> 的 VM 时订阅其请求事件；切换时先退订旧 VM。</summary>
    private void OnDataContextChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        if (_requester is not null)
            _requester.SetDirectionRequested -= OnSetDirectionRequested;
        _requester = e.NewValue as IFlowDirectionRequester;
        if (_requester is not null)
            _requester.SetDirectionRequested += OnSetDirectionRequested;
    }

    /// <summary>响应 VM 的设置流向请求，落到图模型并把结果回填到事件参数。</summary>
    private void OnSetDirectionRequested(object? sender, SetDirectionEventArgs e)
    {
        e.Result = SetConnectionDirection(e.Number, e.Direction);
    }
}
