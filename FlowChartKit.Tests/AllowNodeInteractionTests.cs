using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Controls;
using FlowChartKit.Demo.Nodes;
using FlowChartKit.Rendering;
using FlowChartKit.ViewModels;
using Xunit;

namespace FlowChartKit.Tests;

/// <summary>
/// AllowNodeInteraction（部件单击/双击操作开关）的控件级回归测试：
/// 画布设为 false 时，运行模式下部件双击不再自切换（LED 换色）、单击不再上抛；
/// 恢复 true 后两者照常。选中/拖拽（编辑模式）与该开关正交，不受影响。
/// </summary>
public class AllowNodeInteractionTests
{
    // ---- STA 基础设施（与 KeyboardAndHitTestTests 相同的宿主方式） ----

    private static void RunOnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }

    private static Window HostInWindow(UIElement content) => new()
    {
        Content = content,
        Width = 200,
        Height = 200,
        ShowActivated = false,
        ShowInTaskbar = false,
        WindowStyle = WindowStyle.None,
    };

    /// <summary>在画布可视树中找自动生成的部件控件（Show 后容器已生成）。</summary>
    private static T? FindDescendant<T>(DependencyObject root) where T : class
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit) return hit;
            var nested = FindDescendant<T>(child);
            if (nested is not null) return nested;
        }
        return null;
    }

    /// <summary>合成左键按下事件（ClickCount 经反射注入 backing 字段 _count——公开 API 只读，WPF 输入系统内部赋值）。</summary>
    private static MouseButtonEventArgs MouseDownArgs(int clickCount)
    {
        var args = new MouseButtonEventArgs(InputManager.Current.PrimaryMouseDevice,
            Environment.TickCount, MouseButton.Left)
        {
            RoutedEvent = Mouse.MouseDownEvent,
        };
        typeof(MouseButtonEventArgs).GetField("_count",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(args, clickCount);
        return args;
    }

    private static MouseButtonEventArgs MouseUpArgs()
        => new(InputManager.Current.PrimaryMouseDevice, Environment.TickCount, MouseButton.Left)
        {
            RoutedEvent = Mouse.MouseUpEvent,
        };

    // ---- 双击自切换开关 ----

    [Fact]
    public void LedDoubleClick_Suppressed_WhenInteractionDisabled()
    {
        RunOnSta(() =>
        {
            var canvas = new DiagramCanvasControl { AllowNodeInteraction = false };
            var window = HostInWindow(canvas);
            window.Show();
            canvas.UpdateLayout(); // 触发模板与 ItemsControl 容器生成，否则可视树里还没有 FlowNodeControl
            try
            {
                canvas.Diagram.IsEditable = false; // 运行模式
                var led = canvas.Diagram.AddNode(NodeKind.Led, 100, 100);
                canvas.UpdateLayout();
                var nodeCtrl = FindDescendant<FlowNodeControl>(canvas)
                    ?? throw new InvalidOperationException("容器未生成：可视树中未找到 FlowNodeControl");

                // 开关关闭：双击不换色（State 保持初始 null，渲染层才视 null 为灰）
                nodeCtrl.RaiseEvent(DownOn(nodeCtrl, 2));
                Assert.Null(led.State);

                // 恢复开关：双击照常自切换（Gray → Red）
                canvas.AllowNodeInteraction = true;
                nodeCtrl.RaiseEvent(DownOn(nodeCtrl, 2));
                Assert.Equal(LedColorState.Red, led.State);
            }
            finally { window.Close(); }
        });
    }

    // ---- 单击上抛开关 ----

    [Fact]
    public void DefaultClick_NotRaised_WhenInteractionDisabled()
    {
        RunOnSta(() =>
        {
            var canvas = new DiagramCanvasControl { AllowNodeInteraction = false };
            var window = HostInWindow(canvas);
            window.Show();
            canvas.UpdateLayout();
            try
            {
                canvas.Diagram.IsEditable = false; // 运行模式
                // SampleCup 无事件目录：走默认单击语义上抛（EventId=null）
                var cup = canvas.Diagram.AddNode(DemoNodeKinds.SampleCup, 100, 100);
                canvas.UpdateLayout();
                var nodeCtrl = FindDescendant<FlowNodeControl>(canvas)
                    ?? throw new InvalidOperationException("容器未生成：可视树中未找到 FlowNodeControl");

                var clicks = 0;
                nodeCtrl.NodeClick += (_, _) => clicks++;

                // 开关关闭：按下-抬起不产生单击事件
                nodeCtrl.RaiseEvent(DownOn(nodeCtrl, 1));
                nodeCtrl.RaiseEvent(UpOn(nodeCtrl));
                Assert.Equal(0, clicks);

                // 恢复开关：同一部件单击照常上抛
                canvas.AllowNodeInteraction = true;
                nodeCtrl.RaiseEvent(DownOn(nodeCtrl, 1));
                nodeCtrl.RaiseEvent(UpOn(nodeCtrl));
                Assert.Equal(1, clicks);
            }
            finally { window.Close(); }
        });
    }

    /// <summary>按下事件定位到目标控件（源控件坐标），供 OnMouseLeftButtonDown 识别。</summary>
    private static MouseButtonEventArgs DownOn(FlowNodeControl target, int clickCount)
    {
        var args = MouseDownArgs(clickCount);
        args.Source = target;
        return args;
    }

    private static MouseButtonEventArgs UpOn(FlowNodeControl target)
    {
        var args = MouseUpArgs();
        args.Source = target;
        return args;
    }
}
