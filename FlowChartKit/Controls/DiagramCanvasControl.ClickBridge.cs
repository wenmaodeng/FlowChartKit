using System.Windows;
using System.Windows.Input;

namespace FlowChartKit.Controls;

/// <summary>
/// <see cref="DiagramCanvasControl"/> 的"部件点击"桥接部分：
/// 接住 <see cref="FlowNodeControl"/> 冒泡上来的 <see cref="NodeClickEventArgs"/>，以两种通道对外分发——
/// CLR 事件 <see cref="NodeClick"/>（与 StatusChanged 同风格，宿主 code-behind 订阅后转发 VM），
/// 以及命令桥 <see cref="NodeClickCommand"/>（宿主 VM 提供自己的命令，控件在点击时执行，参数即 <see cref="NodeClickEventArgs"/>）。
/// 两种通道同时生效，宿主按偏好选用。
/// </summary>
public partial class DiagramCanvasControl
{
    /// <summary>部件被点击时触发（仅运行模式，即非编辑状态；编辑模式下点击用于选中/拖拽，不上抛；拖动结束同样不触发）。参数携带被点部件的视图模型。</summary>
    public event Action<NodeClickEventArgs>? NodeClick;

    public static readonly DependencyProperty NodeClickCommandProperty = DependencyProperty.Register(
        nameof(NodeClickCommand), typeof(ICommand), typeof(DiagramCanvasControl),
        new PropertyMetadata(null));

    /// <summary>
    /// 部件点击命令桥：方向为"控件→宿主"的上抛——宿主 VM 用普通 OneWay 绑定把自己的命令赋进来
    /// （同 <c>Button.Command</c>，默认 Mode 即可），控件在点击时以 <see cref="NodeClickEventArgs"/> 为参数执行。
    /// 与 <see cref="SetNodeStateByNumberCommand"/> 的差异：那些是"宿主→控件"的下发方向，
    /// 控件在 Loaded 时把内置命令经 OneWayToSource 灌入 VM；本命令是宿主提供、控件调用，不要用 OneWayToSource。
    /// </summary>
    public ICommand? NodeClickCommand
    {
        get => (ICommand?)GetValue(NodeClickCommandProperty);
        set => SetValue(NodeClickCommandProperty, value);
    }

    public static readonly DependencyProperty AllowNodeInteractionProperty = DependencyProperty.Register(
        nameof(AllowNodeInteraction), typeof(bool), typeof(DiagramCanvasControl),
        new PropertyMetadata(true));

    /// <summary>
    /// 是否允许对部件进行单击/双击操作（默认 true）。设为 false 时：
    /// 双击不再触发部件自切换（LED 换色、开关通断）与事件上抛，单击不再上抛
    /// <see cref="NodeClick"/>/<see cref="NodeClickCommand"/>——用于纯展示屏等
    /// 只读运行场景。选中/拖拽（<c>IsEditable</c>）、右键菜单（<c>AllowContextMenu</c>）
    /// 各由独立开关控制，本属性不影响它们。
    /// </summary>
    public bool AllowNodeInteraction
    {
        get => (bool)GetValue(AllowNodeInteractionProperty);
        set => SetValue(AllowNodeInteractionProperty, value);
    }

    /// <summary>构造时挂接冒泡监听：子级 FlowNodeControl 的点击统一从这里出口。</summary>
    private void HookNodeClick()
        => AddHandler(FlowNodeControl.NodeClickEvent, new RoutedEventHandler(OnNodeClickBubbled));

    private void OnNodeClickBubbled(object sender, RoutedEventArgs e)
    {
        if (e is not NodeClickEventArgs args) return;
        // 兜底拦截：子级入口已在源头抑制，此处防御未来新增的上抛路径
        if (!AllowNodeInteraction) { e.Handled = true; return; }
        NodeClick?.Invoke(args);
        NodeClickCommand?.Execute(args);
    }
}
