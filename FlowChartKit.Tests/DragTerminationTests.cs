using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Input;
using FlowChartKit;
using FlowChartKit.Controls;
using FlowChartKit.ViewModels;
using Xunit;

namespace FlowChartKit.Tests;

/// <summary>
/// 拖拽终止回归测试（控件级）：覆盖两项约定——
/// 1) 拖拽进行中切出编辑模式必须立即终止（管路端点拖拽此前漏检，与部件拖拽同一模式）；
/// 2) 鼠标捕获被外部夺走（LostMouseCapture）必须复位拖拽状态，避免标志与吸附高亮残留。
/// 控件挂进真实显示的窗口（脱离可视树时 CaptureMouse 无呈现源会失败）；捕获丢失经真实
/// 路由事件路径触发（ReleaseMouseCapture）。拖拽状态字段用反射置位，避免依赖 GetPosition 命中。
/// </summary>
public class DragTerminationTests
{
    // ---- STA 基础设施 ----

    /// <summary>在独立 STA 线程上执行（WPF 控件的线程亲和性要求），异常保留原始堆栈抛回。</summary>
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

    /// <summary>把控件挂进已显示的无边框小窗口，使其具有呈现源（CaptureMouse 才会生效）。</summary>
    private static Window HostInWindow(UIElement content) => new()
    {
        Content = content,
        Width = 200,
        Height = 200,
        ShowActivated = false,
        ShowInTaskbar = false,
        WindowStyle = WindowStyle.None,
    };

    private sealed class TestPipeControl : PipeControl
    {
        public void SimulateMouseMove(MouseEventArgs e) => OnMouseMove(e);
        public void SimulateMouseUp(MouseButtonEventArgs e) => OnMouseLeftButtonUp(e);
    }

    private sealed class TestFlowNodeControl : FlowNodeControl
    {
        public void SimulateMouseMove(MouseEventArgs e) => OnMouseMove(e);
    }

    /// <summary>私有字段沿基类链查找（控件类的拖拽状态字段声明在基类上）。</summary>
    private static FieldInfo Field(object target, string name)
    {
        for (var t = target.GetType(); t is not null; t = t.BaseType)
        {
            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f is not null) return f;
        }
        throw new MissingFieldException(name);
    }

    private static MouseEventArgs MoveArgs() => new(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseMoveEvent };
    private static MouseButtonEventArgs UpArgs() => new(Mouse.PrimaryDevice, 0, MouseButton.Left);

    /// <summary>构建"终点已吸附到部件、控制侧+VM侧均处于端点拖拽中"的管路场景。</summary>
    private static (DiagramViewModel Diagram, ConnectionViewModel Conn, PortViewModel TargetPort)
        NewPipeInEndpointDrag(DiagramViewModel diagram, TestPipeControl pipe)
    {
        var node = diagram.AddNode(NodeKind.SampleCup, 200, 200);
        var port = diagram.FindPort(node, "L")!;
        var conn = diagram.AddFreeConnection(new Point(60, 60), new Point(250, 150));
        conn.SetEnd(false, node, port);

        pipe.DataContext = conn;
        diagram.BeginConnectionEndpointDrag(conn, isStart: false, new Point(300, 150));
        Field(pipe, "_draggingEndpoint").SetValue(pipe, true);
        Field(pipe, "_dragIsStart").SetValue(pipe, false);
        Assert.True(pipe.CaptureMouse());
        return (diagram, conn, port);
    }

    // ---- 管路端点拖拽 ----

    [Fact]
    public void PipeEndpointDrag_Stops_WhenEditModeTurnsOff()
    {
        RunOnSta(() =>
        {
            var pipe = new TestPipeControl();
            var window = HostInWindow(pipe);
            window.Show();
            try
            {
                var (diagram, conn, _) = NewPipeInEndpointDrag(new DiagramViewModel(800, 600), pipe);

                diagram.IsEditable = false; // 拖拽进行中切出编辑模式
                pipe.SimulateMouseMove(MoveArgs());

                // 关键回归信号：拖拽已终止——连接端点脱离部件、控制侧标志复位。
                // CaptureMouse 会同步重放一条真实光标位置的移动事件（输入系统行为），落点含真实坐标，
                // 故不作精确位置断言；落点精确性由 DiagramViewModelTests 的 VM 层测试覆盖。
                Assert.Null(conn.TargetNode);
                Assert.NotNull(conn.FreeTarget);
                Assert.False((bool)Field(pipe, "_draggingEndpoint").GetValue(pipe)!);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void PipeEndpointDrag_Finishes_WhenMouseCaptureLost()
    {
        RunOnSta(() =>
        {
            var pipe = new TestPipeControl();
            var window = HostInWindow(pipe);
            window.Show();
            try
            {
                var (diagram, conn, port) = NewPipeInEndpointDrag(new DiagramViewModel(800, 600), pipe);

                diagram.SetSnapHighlight(port); // 模拟拖拽中的吸附高亮
                Assert.True(port.IsHighlighted);

                pipe.ReleaseMouseCapture(); // 真实路径：捕获丢失 → LostMouseCapture 路由事件 → 收尾

                Assert.False(port.IsHighlighted); // 高亮已随收尾清除
                Assert.Null(conn.TargetNode);
                Assert.NotNull(conn.FreeTarget);
                Assert.False((bool)Field(pipe, "_draggingEndpoint").GetValue(pipe)!);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void PipeEndpointDrag_Commits_OnMouseUp()
    {
        RunOnSta(() =>
        {
            var pipe = new TestPipeControl();
            var window = HostInWindow(pipe);
            window.Show();
            try
            {
                var (_, conn, _) = NewPipeInEndpointDrag(new DiagramViewModel(800, 600), pipe);

                pipe.SimulateMouseUp(UpArgs());

                Assert.Null(conn.TargetNode);
                Assert.NotNull(conn.FreeTarget);
                Assert.False((bool)Field(pipe, "_draggingEndpoint").GetValue(pipe)!);
            }
            finally { window.Close(); }
        });
    }

    // ---- 部件拖拽 ----

    [Fact]
    public void NodeDrag_Resets_WhenMouseCaptureLost()
    {
        RunOnSta(() =>
        {
            var diagram = new DiagramViewModel(800, 600);
            var node = diagram.AddNode(NodeKind.SampleCup, 100, 100);
            var ctrl = new TestFlowNodeControl();
            var window = HostInWindow(ctrl);
            window.Show();
            try
            {
                ctrl.DataContext = node;
                Assert.True(ctrl.CaptureMouse());
                Field(ctrl, "_dragging").SetValue(ctrl, true);
                ctrl.Cursor = Cursors.SizeAll;

                ctrl.ReleaseMouseCapture(); // 真实路径：捕获丢失 → LostMouseCapture 路由事件 → 复位

                Assert.False((bool)Field(ctrl, "_dragging").GetValue(ctrl)!);
                Assert.Equal(Cursors.Arrow, ctrl.Cursor);

                // 复位后即便再收到移动事件也不会继续拖动部件
                ctrl.SimulateMouseMove(MoveArgs());
                Assert.Equal(100, node.X);
                Assert.Equal(100, node.Y);
            }
            finally { window.Close(); }
        });
    }
}
