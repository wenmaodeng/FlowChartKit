using System.Reflection;
using System.Runtime.ExceptionServices;
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
/// P2 一致性/健壮性修复的控件级回归测试：
/// 1) 部件命中让位——相连管路端点（索引直达）与自由端（自由端索引）靠近时把命中让给管路层，
///    索引化不得改变原语义（自由端落在部件体内部时仍须可抓取）；
/// 2) 画布级键盘——Delete 删除选中、Esc 取消放置模式、方向键微调（事件经焦点元素冒泡到画布）；
/// 3) 管路端点拖拽中按 Esc 回退到拖拽前锚点。
/// </summary>
public class KeyboardAndHitTestTests
{
    // ---- STA 基础设施（与 DragTerminationTests 相同的宿主方式） ----

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
            ExceptionDispatchInfo.Capture(error).Throw();
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

    private static FieldInfo Field(object target, string name)
    {
        for (var t = target.GetType(); t is not null; t = t.BaseType)
        {
            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f is not null) return f;
        }
        throw new MissingFieldException(name);
    }

    private static KeyEventArgs KeyArgs(Window window, Key key)
        => new(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, 0, key)
        {
            RoutedEvent = UIElement.KeyDownEvent
        };

    private sealed class TestFlowNodeControl : FlowNodeControl
    {
        public HitTestResult? HitTest(Point p) => HitTestCore(new PointHitTestParameters(p));
    }

    private sealed class TestPipeControl : PipeControl
    {
        public void SimulateKeyDown(KeyEventArgs e) => OnKeyDown(e);
    }

    // ---- 命中让位（索引化后的语义保持） ----

    [Fact]
    public void NodeHitTest_YieldsToAttachedEndpointNearPort()
    {
        RunOnSta(() =>
        {
            var d = new DiagramViewModel(800, 600);
            var node = d.AddNode(DemoNodeKinds.SampleCup, 100, 100);
            var conn = d.AddFreeConnection(new Point(60, 60), new Point(250, 150));
            conn.SetEnd(true, node, d.FindPort(node, "L")!); // 起点吸附到左端口，画布坐标 (100,132)

            var ctrl = new TestFlowNodeControl { DataContext = node };

            // 端口处（控件相对 (0,32) = 画布 (100,132)）让位给管路层，否则边界端点抓不住
            Assert.Null(ctrl.HitTest(new Point(0, 32)));
            // 远离端点的图形内部仍归部件所有
            Assert.NotNull(ctrl.HitTest(new Point(23, 32)));
        });
    }

    [Fact]
    public void NodeHitTest_YieldsToFreeEndUnderNodeBody()
    {
        RunOnSta(() =>
        {
            var d = new DiagramViewModel(800, 600);
            var node = d.AddNode(DemoNodeKinds.SampleCup, 100, 100);
            // 自由端 (122,130) 落在部件体内部、且不靠近任何端口（距左端口约 22px > 12px）
            var conn = d.AddFreeConnection(new Point(250, 150), new Point(122, 130));

            var ctrl = new TestFlowNodeControl { DataContext = node };

            // 自由端（控件相对 (22,30)）必须让位给管路层，否则大部件体下的自由端永远抓不住
            Assert.Null(ctrl.HitTest(new Point(22, 30)));
        });
    }

    // ---- 管路端点拖拽 Esc 取消 ----

    [Fact]
    public void PipeEscCancel_RevertsEndpointToPreDragAnchor()
    {
        RunOnSta(() =>
        {
            var pipe = new TestPipeControl();
            var window = HostInWindow(pipe);
            window.Show();
            try
            {
                var d = new DiagramViewModel(800, 600);
                var node = d.AddNode(DemoNodeKinds.SampleCup, 200, 200);
                var port = d.FindPort(node, "L")!;
                var conn = d.AddFreeConnection(new Point(60, 60), new Point(250, 150));
                conn.SetEnd(false, node, port);

                pipe.DataContext = conn;
                d.BeginConnectionEndpointDrag(conn, isStart: false, new Point(300, 150));
                Field(pipe, "_draggingEndpoint").SetValue(pipe, true);
                Field(pipe, "_dragIsStart").SetValue(pipe, false);
                Assert.True(pipe.CaptureMouse());

                pipe.SimulateKeyDown(KeyArgs(window, Key.Escape));

                Assert.Same(node, conn.TargetNode); // 回退到拖拽前锚点
                Assert.Same(port, conn.TargetPort);
                Assert.False((bool)Field(pipe, "_draggingEndpoint").GetValue(pipe)!);
                Assert.False(pipe.IsMouseCaptured);
            }
            finally { window.Close(); }
        });
    }

    // ---- 设置部件状态（枚举载荷） ----

    [Fact]
    public void Led_SetNodeStateByNumber_AcceptsColorEnumPayload()
    {
        RunOnSta(() =>
        {
            var canvas = new DiagramCanvasControl();
            var window = HostInWindow(canvas);
            window.Show();
            try
            {
                var led = canvas.Diagram.AddNode(NodeKind.Led, 100, 100);

                // 宿主经 SetNodeState（命令同路径）下发枚举载荷：原样写入 State
                Assert.True(canvas.SetNodeState(NodeKind.Led, led.KindNumber, LedColorState.Green));
                Assert.Equal(LedColorState.Green, led.State);

                // 双击从枚举态继续循环：绿 → 蓝
                NodeDefinitionRegistry.Resolve(NodeKind.Led)
                    .InvokeEvent(led, NodeEventGesture.DoubleClick, "ToggleColor");
                Assert.Equal(LedColorState.Blue, led.State);
            }
            finally { window.Close(); }
        });
    }

    // ---- 画布级键盘 ----

    [Fact]
    public void CanvasKeyboard_DeleteRemovesSelectedNode()
    {
        RunOnSta(() =>
        {
            var canvas = new DiagramCanvasControl();
            var window = HostInWindow(canvas);
            window.Show();
            try
            {
                var d = canvas.Diagram;
                var node = d.AddNode(DemoNodeKinds.SampleCup, 100, 100);
                d.SelectedNode = node;

                canvas.RaiseEvent(KeyArgs(window, Key.Delete));

                Assert.DoesNotContain(node, d.Nodes);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void CanvasKeyboard_EscCancelsPendingPlacement()
    {
        RunOnSta(() =>
        {
            var canvas = new DiagramCanvasControl();
            var window = HostInWindow(canvas);
            window.Show();
            try
            {
                canvas.PendingNodeKind = DemoNodeKinds.SampleCup; // 进入放置模式（变更回调会落焦点；未激活窗口无法取键盘焦点，不在此断言）

                canvas.RaiseEvent(KeyArgs(window, Key.Escape));

                Assert.Null(canvas.PendingNodeKind);
                Assert.Empty(canvas.Diagram.Nodes); // 未放置任何部件
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void CanvasKeyboard_ArrowNudgesSelectedNode()
    {
        RunOnSta(() =>
        {
            var canvas = new DiagramCanvasControl();
            var window = HostInWindow(canvas);
            window.Show();
            try
            {
                var d = canvas.Diagram;
                var node = d.AddNode(DemoNodeKinds.SampleCup, 100, 100);
                d.SelectedNode = node;

                canvas.RaiseEvent(KeyArgs(window, Key.Right));
                Assert.Equal(101, node.X);
                Assert.Equal(100, node.Y);

                canvas.RaiseEvent(KeyArgs(window, Key.Up));
                Assert.Equal(101, node.X);
                Assert.Equal(99, node.Y);
            }
            finally { window.Close(); }
        });
    }
}
