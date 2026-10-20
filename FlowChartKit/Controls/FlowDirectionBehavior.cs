using System.Windows;

namespace FlowChartKit.Controls;

/// <summary>
/// 附加行为：挂在 <see cref="DiagramCanvasControl"/> 上，把控件原生的
/// "按编号设置流向"回调写进宿主对象（实现了 <see cref="IFlowDirectionSink"/> 的 ViewModel）。
/// </summary>
/// <remarks>
/// 用法：<c>fc:FlowDirectionBehavior.Sink="{Binding}"</c>——不带 Path 即绑定到 DataContext 自身。
/// 这里走的是常规方向的绑定（源 = ViewModel，目标 = 控件上的附加属性）：
/// DataContext 到位后绑定一定会重新求值并触发赋值，不像 OneWayToSource 那样
/// 依赖"绑定附加那一刻"的初值回传，因此不受控件入树时机影响，也不需要宿主写 Loaded。
/// </remarks>
public static class FlowDirectionBehavior
{
    public static readonly DependencyProperty SinkProperty = DependencyProperty.RegisterAttached(
        "Sink", typeof(IFlowDirectionSink), typeof(FlowDirectionBehavior),
        new PropertyMetadata(null, OnSinkChanged));

    public static IFlowDirectionSink? GetSink(DependencyObject element)
        => (IFlowDirectionSink?)element.GetValue(SinkProperty);

    public static void SetSink(DependencyObject element, IFlowDirectionSink? value)
        => element.SetValue(SinkProperty, value);

    private static void OnSinkChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // 宿主 VM 到位即写入；DataContext 变化导致重绑时会再次写入，幂等
        if (d is DiagramCanvasControl canvas && e.NewValue is IFlowDirectionSink sink)
            sink.SetDirection = canvas.SetConnectionDirection;
    }
}
