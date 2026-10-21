using System.Windows;
using System.Windows.Input;
using FlowChartKit.Core;
using FlowChartKit.Rendering;

namespace FlowChartKit.Controls;

/// <summary>
/// <see cref="DiagramCanvasControl"/> 的"设置部件状态"桥接部分：
/// 与管路流向桥接（DirectionBridge 分部）同一模式——
/// 控件把内置命令作为依赖属性暴露，宿主 VM 用 OneWayToSource 绑定取走后自行调用，
/// 参数传 <see cref="SetNodeStateRequest"/>，执行结果回填到 <see cref="SetNodeStateRequest.Result"/>。
/// 命名为 ByNumber 以表明定位方式：部件类型 + 同类序号（与画布上的序号徽章一致）。
/// </summary>
public partial class DiagramCanvasControl
{
    public static readonly DependencyProperty SetNodeStateByNumberCommandProperty = DependencyProperty.Register(
        nameof(SetNodeStateByNumberCommand), typeof(ICommand), typeof(DiagramCanvasControl),
        new PropertyMetadata(null));

    /// <summary>
    /// 按类型与编号设置部件状态的命令（控件内置实现，Loaded 时赋值；请勿在外部替换）。
    /// 宿主 VM 以 <c>OneWayToSource</c> 绑定取走，参数传 <see cref="SetNodeStateRequest"/>，
    /// 执行结果回填到 <see cref="SetNodeStateRequest.Result"/>。
    /// </summary>
    public ICommand? SetNodeStateByNumberCommand
    {
        get => (ICommand?)GetValue(SetNodeStateByNumberCommandProperty);
        set => SetValue(SetNodeStateByNumberCommandProperty, value);
    }

    private void ExecuteSetNodeState(object? parameter)
    {
        if (parameter is SetNodeStateRequest request)
            request.Result = SetNodeState(request.Kind, request.Number, request.State);
    }

    /// <summary>
    /// 按部件类型与同类序号设置部件状态（写入 <see cref="ViewModels.NodeViewModel.State"/>，带变更通知）。
    /// 由 <see cref="ExecuteSetNodeState"/> 在 VM 执行命令时调用，也可被宿主代码直接调用。
    /// 序号与画布上的序号徽章一致，从 1 开始；状态语义由宿主自定义。
    /// </summary>
    /// <returns>找到并设置了该部件时为 true；未找到时为 false。</returns>
    public bool SetNodeState(NodeKind kind, int number, object? state)
    {
        var node = Diagram.FindNode(kind, number);
        if (node is null)
        {
            StatusChanged?.Invoke($"未找到 {kind} #{number} 对应的部件");
            return false;
        }

        node.State = state;
        // 能按类型+序号命中的部件必然已注册定义，此处 Resolve 不会抛异常
        StatusChanged?.Invoke($"{NodeDefinitionRegistry.Resolve(kind).DisplayName} #{number} 状态已更新");
        return true;
    }
}
