using System.Windows;
using System.Windows.Input;
using FlowChartKit.Core;

namespace FlowChartKit.Controls;

/// <summary>
/// <see cref="DiagramCanvasControl"/> 的"设置流向"桥接部分：
/// 控件把实现好的 <see cref="SetDirectionCommand"/> 作为依赖属性暴露，
/// 宿主 VM 用 OneWayToSource 绑定取走后自行调用——VM 只依赖 <see cref="ICommand"/>
/// 与 <see cref="SetDirectionRequest"/>，完全不引用控件类型。
/// </summary>
public partial class DiagramCanvasControl
{
    public static readonly DependencyProperty SetDirectionCommandProperty = DependencyProperty.Register(
        nameof(SetDirectionCommand), typeof(ICommand), typeof(DiagramCanvasControl),
        new PropertyMetadata(null));

    /// <summary>
    /// 按编号设置流向的命令（控件内置实现，构造时赋值；请勿在外部替换）。
    /// 宿主 VM 以 <c>OneWayToSource</c> 绑定取走，参数传 <see cref="SetDirectionRequest"/>，
    /// 执行结果回填到 <see cref="SetDirectionRequest.Result"/>。
    /// </summary>
    public ICommand? SetDirectionCommand
    {
        get => (ICommand?)GetValue(SetDirectionCommandProperty);
        set => SetValue(SetDirectionCommandProperty, value);
    }

    private void ExecuteSetDirection(object? parameter)
    {
        if (parameter is SetDirectionRequest request)
            request.Result = SetConnectionDirection(request.Number, request.Direction);
    }

    /// <summary>
    /// 按管路编号设置流向。由 <see cref="ExecuteSetDirection"/> 在 VM 执行命令时调用，
    /// 也可被宿主代码直接调用。编号与画布上的编号徽章一致，从 1 开始。
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
}
