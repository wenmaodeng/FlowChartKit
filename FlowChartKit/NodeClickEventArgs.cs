using System.Windows;
using FlowChartKit.ViewModels;

namespace FlowChartKit;

/// <summary>
/// 部件点击事件参数：携带被点部件的视图模型（Kind/KindNumber/Id/Label/State 俱全），宿主按需分发。
/// 作为路由事件随 <see cref="Controls.FlowNodeControl.NodeClickEvent"/> 冒泡，画布控件接住后再以
/// CLR 事件与命令桥两种通道对外（见 Controls.DiagramCanvasControl.ClickBridge）。
/// 与 SetDirectionRequest/SetNodeStateRequest 一致放在根命名空间：宿主 VM 引用它无需接触 Controls 命名空间。
/// </summary>
public sealed class NodeClickEventArgs : RoutedEventArgs
{
    public NodeClickEventArgs(NodeViewModel node)
    {
        Node = node;
        RoutedEvent = Controls.FlowNodeControl.NodeClickEvent;
    }

    /// <summary>被点击的部件。</summary>
    public NodeViewModel Node { get; }
}
