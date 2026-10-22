using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Controls;
using FlowChartKit.Data;
using FlowChartKit.ViewModels;
using Xunit;

namespace FlowChartKit.Tests;

/// <summary>文本模块：编号分配、按编号改文本、选中互斥、删除与序列化往返。</summary>
public class TextBlockTests
{
    [Fact]
    public void AddTextItem_AssignsSequentialNumbers_AndClampsIntoCanvas()
    {
        var d = new DiagramViewModel(800, 600);

        var t1 = d.AddTextItem(100, 80, "注释一");
        var t2 = d.AddTextItem(5000, 5000, "注释二");

        Assert.Equal(1, t1.Number);
        Assert.Equal(2, t2.Number);
        Assert.Equal(800, t2.X, 3);          // 越界坐标钳到画布内
        Assert.Equal(600, t2.Y, 3);
        Assert.Equal("注释一", t1.Content);
        Assert.Same(d, t1.Diagram);
    }

    [Fact]
    public void SetTextContent_UpdatesByNumber()
    {
        var d = new DiagramViewModel(800, 600);
        d.AddTextItem(10, 10, "旧文本");

        Assert.True(d.SetTextContent(1, "新文本"));
        Assert.Equal("新文本", d.FindTextBlock(1)!.Content);

        // null 按空字符串处理
        Assert.True(d.SetTextContent(1, null));
        Assert.Equal("", d.FindTextBlock(1)!.Content);
    }

    [Fact]
    public void SetTextContent_UnknownNumber_ReturnsFalse()
    {
        var d = new DiagramViewModel(800, 600);
        Assert.False(d.SetTextContent(9, "任意"));
        Assert.Null(d.FindTextBlock(9));
    }

    [Fact]
    public void Selection_IsMutuallyExclusive_AcrossKinds()
    {
        var d = new DiagramViewModel(800, 600);
        var node = d.AddNode(NodeKind.Led, 100, 100, "灯");
        d.AddFreeConnection(new Point(0, 0), new Point(50, 50));
        var text = d.AddTextItem(10, 10, "注释");

        d.SelectedNode = node;
        Assert.Same(node, d.SelectedNode);

        // 选中文本：部件自动取消
        d.SelectedTextBlock = text;
        Assert.Null(d.SelectedNode);
        Assert.True(text.IsSelected);

        // 选中管路：文本自动取消
        d.SelectedConnection = d.Connections[0];
        Assert.Null(d.SelectedTextBlock);
        Assert.False(text.IsSelected);
        Assert.Same(d.Connections[0], d.SelectedConnection);
    }

    [Fact]
    public void DeleteSelectedCommand_RemovesSelectedText()
    {
        var d = new DiagramViewModel(800, 600);
        var text = d.AddTextItem(10, 10, "注释");

        d.SelectedTextBlock = text;
        d.DeleteSelectedCommand.Execute(null);

        Assert.Empty(d.Texts);
        Assert.Null(d.SelectedTextBlock);
    }

    [Fact]
    public void RemoveTextItem_ClearsSelectionAndDiagramBackref()
    {
        var d = new DiagramViewModel(800, 600);
        var text = d.AddTextItem(10, 10, "注释");
        d.SelectedTextBlock = text;

        d.RemoveTextItem(text);

        Assert.Empty(d.Texts);
        Assert.Null(d.SelectedTextBlock);
        Assert.Null(text.Diagram);

        // 重复移除是安全的空转
        d.RemoveTextItem(text);
    }

    [Fact]
    public void Clear_RemovesTexts()
    {
        var d = new DiagramViewModel(800, 600);
        d.AddTextItem(10, 10, "注释");
        d.AddTextItem(20, 20, "备注");

        d.Clear();

        Assert.Empty(d.Texts);
        Assert.Null(d.SelectedTextBlock);
    }

    [Fact]
    public void JsonRoundTrip_PreservesTexts()
    {
        var d = new DiagramViewModel(800, 600);
        d.AddTextItem(30, 40, "样品信息", fontSize: 18);
        d.AddTextItem(100, 120, "备注");

        var restored = DiagramViewModel.FromData(DiagramSerializer.FromJson(DiagramSerializer.ToJson(d.ToData()))!)!;

        Assert.Equal(2, restored.Texts.Count);
        var first = restored.Texts[0];
        Assert.Equal(30, first.X, 3);
        Assert.Equal(40, first.Y, 3);
        Assert.Equal("样品信息", first.Content);
        Assert.Equal(18, first.FontSize, 3);
        Assert.Equal(1, first.Number);
        Assert.Equal(2, restored.Texts[1].Number);
        Assert.Same(restored, first.Diagram);

        // 往返后按编号改文本仍可用
        Assert.True(restored.SetTextContent(2, "改过了"));
        Assert.Equal("改过了", restored.Texts[1].Content);
    }

    [Fact]
    public void LegacyJson_WithoutTexts_LoadsEmpty()
    {
        const string legacy = """
        {
          "Version": 1,
          "Width": 800,
          "Height": 600,
          "Nodes": [],
          "Connections": []
        }
        """;

        var restored = DiagramViewModel.FromData(DiagramSerializer.FromJson(legacy)!);
        Assert.Empty(restored.Texts);
    }
}

/// <summary>正交管路拐角圆角：几何可构建、边界不超出原折线包围盒。</summary>
public class PipeCornerRoundTests
{
    private static Rect BuildBounds(Point[] pts)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
            PipeControl.BuildRoundedPolyline(ctx, pts);
        geo.Freeze();
        return geo.Bounds;
    }

    [Fact]
    public void LShape_BoundsMatchPolyline_AndCornerRounded()
    {
        // L 形：(0,0) → (0,100) → (100,100)，拐角 (0,100) 被圆角
        var bounds = BuildBounds(new[] { new Point(0, 0), new Point(0, 100), new Point(100, 100) });

        // 端点与段中段保持原位：包围盒与折线一致（圆角只往内收拐角，不改包围盒）
        Assert.Equal(0, bounds.Left, 3);
        Assert.Equal(0, bounds.Top, 3);
        Assert.Equal(100, bounds.Right, 3);
        Assert.Equal(100, bounds.Bottom, 3);
    }

    [Fact]
    public void ShortSegments_ClampRadiusToHalfSegment()
    {
        // 相邻段各 5px：半径钳到 2.5，圆角不会把端点"吃掉"
        var bounds = BuildBounds(new[] { new Point(0, 0), new Point(0, 5), new Point(5, 5) });

        Assert.Equal(0, bounds.Left, 3);
        Assert.Equal(0, bounds.Top, 3);
        Assert.Equal(5, bounds.Right, 3);
        Assert.Equal(5, bounds.Bottom, 3);
    }

    [Fact]
    public void CollinearPoints_StayStraight()
    {
        // 中间点共线：整条仍是直线，包围盒为线段包围盒
        var bounds = BuildBounds(new[] { new Point(0, 0), new Point(50, 0), new Point(100, 0) });

        Assert.Equal(0, bounds.Left, 3);
        Assert.Equal(0, bounds.Top, 3);
        Assert.Equal(100, bounds.Right, 3);
        Assert.Equal(0, bounds.Bottom, 3);
    }

    [Fact]
    public void TwoPoints_Unchanged()
    {
        var bounds = BuildBounds(new[] { new Point(0, 0), new Point(80, 60) });

        Assert.Equal(0, bounds.Left, 3);
        Assert.Equal(0, bounds.Top, 3);
        Assert.Equal(80, bounds.Right, 3);
        Assert.Equal(60, bounds.Bottom, 3);
    }
}

/// <summary>
/// 文本模块控件的控件级验证（STA）：占位尺寸由 Measure 给出（首帧即可命中）、命中限定文本框、
/// 光标随编辑模式切换，以及按编号改文本的命令桥（控件 Loaded 后灌入的 SetTextByNumberCommand）。
/// </summary>
public class TextBlockControlTests
{
    [Fact]
    public void Measure_GivesTextSize_BeforeRender()
    {
        RunOnSta(() =>
        {
            var d = new DiagramViewModel(800, 600);
            var text = d.AddTextItem(50, 40, "工位说明", fontSize: 16);

            var ctrl = new TestTextBlockControl { DataContext = text };

            // 未经渲染：Measure 阶段即给出文本尺寸（首帧就能命中，不依赖 OnRender 的副作用）
            ctrl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            ctrl.Arrange(new Rect(new Point(0, 0), ctrl.DesiredSize));

            Assert.True(ctrl.DesiredSize.Width > 0);
            Assert.True(ctrl.ActualHeight > 0);
            Assert.NotNull(ctrl.HitTest(new Point(1, 1)));                  // 文本内命中
            Assert.Null(ctrl.HitTest(new Point(ctrl.ActualWidth + 30, 5))); // 文本外让给下层管路
        });
    }

    [Fact]
    public void Cursor_FollowsEditableMode()
    {
        RunOnSta(() =>
        {
            var d = new DiagramViewModel(800, 600);
            var text = d.AddTextItem(10, 10, "注释");
            var ctrl = new TextBlockItemControl { DataContext = text };

            d.IsEditable = true;
            Assert.Same(Cursors.SizeAll, ctrl.Cursor);   // 编辑模式：提示可拖动

            d.IsEditable = false;
            Assert.Same(Cursors.Arrow, ctrl.Cursor);     // 运行模式：不可拖动，不误导
        });
    }

    [Fact]
    public void SetTextByNumberCommand_UpdatesContent_AndReportsResult()
    {
        RunOnSta(() =>
        {
            var canvas = new DiagramCanvasControl
            {
                // 指向不存在的临时路径：避免异步载入默认存档把本次新增的文本清掉
                LayoutFilePath = Path.Combine(Path.GetTempPath(), $"fck-{Guid.NewGuid():N}.json")
            };
            var window = HostInWindow(canvas);
            window.Show();
            try
            {
                var text = canvas.Diagram.AddTextItem(20, 20, "旧文本");

                // 与 SetDirectionByNumberCommand 同一模式：Loaded 时控件把命令灌入依赖属性
                Assert.NotNull(canvas.SetTextByNumberCommand);

                var request = new SetTextRequest(text.Number, "命令桥文本");
                canvas.SetTextByNumberCommand!.Execute(request);

                Assert.True(request.Result);
                Assert.Equal("命令桥文本", text.Content);

                var missing = new SetTextRequest(999, "无此编号");
                canvas.SetTextByNumberCommand.Execute(missing);

                Assert.False(missing.Result);
            }
            finally { window.Close(); }
        });
    }

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

    private sealed class TestTextBlockControl : TextBlockItemControl
    {
        public HitTestResult? HitTest(Point p) => HitTestCore(new PointHitTestParameters(p));
    }
}
