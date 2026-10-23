using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using FlowChartKit.Controls;
using FlowChartKit.Data;
using FlowChartKit.ViewModels;
using Xunit;

namespace FlowChartKit.Tests;

public class NodeResizeTests
{
    // Switch 内置基准尺寸 64x48（见 SwitchDefinition），此处按实测值断言比例关系
    private static (DiagramViewModel Diagram, NodeViewModel Node) NewNode(int canvasW = 1000, int canvasH = 1000)
    {
        var d = new DiagramViewModel(canvasW, canvasH);
        var node = d.AddNode(NodeKind.Switch, x: 100, y: 100);
        return (d, node);
    }

    [Fact]
    public void ResizeNodeTo_ScalesProportionally()
    {
        var (d, node) = NewNode();
        double bw = node.BaseWidth, bh = node.BaseHeight;

        // 拖到 (100 + 2*bw, 100 + 2*bh)：两维都需要 2 倍
        d.ResizeNodeTo(node, node.X + bw * 2, node.Y + bh * 2);

        Assert.Equal(2, node.Scale, 3);
        Assert.Equal(bw * 2, node.ShapeWidth, 3);
        Assert.Equal(bh * 2, node.ShapeHeight, 3);
        // 锚点为左上角：位置不变
        Assert.Equal(100, node.X);
        Assert.Equal(100, node.Y);
    }

    [Fact]
    public void ResizeNodeTo_UsesLargerDimension()
    {
        var (d, node) = NewNode();
        double bw = node.BaseWidth, bh = node.BaseHeight;

        // 宽要 3 倍、高只要 1 倍：等比缩放取较大者（3），部件至少覆盖鼠标位置
        d.ResizeNodeTo(node, node.X + bw * 3, node.Y + bh);

        Assert.Equal(3, node.Scale, 3);
        Assert.True(node.ShapeWidth >= bw * 3 - 0.01);
    }

    [Fact]
    public void ResizeNodeTo_ClampedToCanvasBounds()
    {
        // 画布 300x300，部件在 (100,100)：最多只能放大到填满右下角
        var (d, node) = NewNode(300, 300);

        d.ResizeNodeTo(node, 5000, 5000);

        Assert.True(node.X + node.ShapeWidth <= 300 + 0.01);
        Assert.True(node.Y + node.ShapeHeight <= 300 + 0.01);
        Assert.True(node.Scale > 1);
    }

    [Fact]
    public void ResizeNodeTo_ClampedToMinScale()
    {
        var (d, node) = NewNode();

        // 往左上角拖：比例 <= 0 时回落到最小缩放，而不是 0 或负值
        d.ResizeNodeTo(node, -500, -500);

        Assert.Equal(NodeViewModel.MinScale, node.Scale);
    }

    [Fact]
    public void ResizeNodeTo_ClampedToMaxScale()
    {
        var (d, node) = NewNode(100000, 100000);

        d.ResizeNodeTo(node, 1e9, 1e9);

        Assert.Equal(NodeViewModel.MaxScale, node.Scale);
    }

    [Fact]
    public void ScaleChange_UpdatesShapeAndPersists()
    {
        var (d, node) = NewNode();
        double bw = node.BaseWidth;

        d.ResizeNodeTo(node, node.X + bw * 1.5, node.Y + node.BaseHeight * 1.5);

        // 存档往返保留缩放
        var loaded = DiagramViewModel.FromData(d.ToData());
        var reloaded = Assert.Single(loaded.Nodes);
        Assert.Equal(1.5, reloaded.Scale, 3);
        Assert.Equal(node.ShapeWidth, reloaded.ShapeWidth, 3);
    }

    [Fact]
    public void LabelFontSize_DefaultsAndClamps()
    {
        var (_, node) = NewNode();

        Assert.Equal(NodeViewModel.DefaultLabelFontSize, node.LabelFontSize);

        node.LabelFontSize = 20;
        Assert.Equal(20, node.LabelFontSize);

        node.LabelFontSize = 1000;                 // 超出上限
        Assert.Equal(NodeViewModel.MaxLabelFontSize, node.LabelFontSize);

        node.LabelFontSize = 0;                    // 低于下限
        Assert.Equal(NodeViewModel.MinLabelFontSize, node.LabelFontSize);
    }

    [Fact]
    public void LabelFontSize_RoundTrips()
    {
        var (d, node) = NewNode();
        node.LabelFontSize = 18;

        var loaded = DiagramViewModel.FromData(d.ToData());

        Assert.Equal(18, Assert.Single(loaded.Nodes).LabelFontSize);
    }

    [Fact]
    public void LabelFontSize_LegacyArchiveFallsBackToDefault()
    {
        var d = new DiagramViewModel(1000, 1000);
        d.AddNode(NodeKind.Switch, x: 0, y: 0);

        // 旧存档没有 LabelFontSize 字段
        var json = DiagramSerializer.ToJson(d.ToData()).Replace("\"LabelFontSize\":11", "");

        var loaded = DiagramViewModel.FromData(DiagramSerializer.FromJson(json)!);

        Assert.Equal(NodeViewModel.DefaultLabelFontSize, Assert.Single(loaded.Nodes).LabelFontSize);
    }

    [Fact]
    public void ResizeNodeTo_KeepsAspectRatio()
    {
        var (d, node) = NewNode();
        double ratio = node.BaseWidth / node.BaseHeight;

        d.ResizeNodeTo(node, node.X + node.BaseWidth * 2.5, node.Y + node.BaseHeight);

        Assert.Equal(ratio, node.ShapeWidth / node.ShapeHeight, 3);
    }
}

/// <summary>
/// 分方向缩放：右侧手柄只改宽（<see cref="NodeViewModel.ScaleX"/>）、下方手柄只改高
/// （<see cref="NodeViewModel.ScaleY"/>），右下角手柄仍等比；<c>Scale</c> 保持等比兼容语义。
/// </summary>
public class NodeScaleXYTests
{
    private static (DiagramViewModel Diagram, NodeViewModel Node) NewNode(int canvasW = 1000, int canvasH = 1000)
    {
        var d = new DiagramViewModel(canvasW, canvasH);
        var node = d.AddNode(NodeKind.Switch, x: 100, y: 100);
        return (d, node);
    }

    [Fact]
    public void ResizeWidthTo_ChangesOnlyWidth()
    {
        var (d, node) = NewNode();
        double bw = node.BaseWidth, bh = node.BaseHeight;

        d.ResizeNodeWidthTo(node, node.X + bw * 3);

        Assert.Equal(3, node.ScaleX, 3);
        Assert.Equal(1, node.ScaleY, 3);          // 高度不受影响
        Assert.Equal(bw * 3, node.ShapeWidth, 3);
        Assert.Equal(bh, node.ShapeHeight, 3);
        Assert.Equal(100, node.Y);                // 上边锚定
    }

    [Fact]
    public void ResizeHeightTo_ChangesOnlyHeight()
    {
        var (d, node) = NewNode();
        double bw = node.BaseWidth, bh = node.BaseHeight;

        d.ResizeNodeHeightTo(node, node.Y + bh * 2);

        Assert.Equal(1, node.ScaleX, 3);          // 宽度不受影响
        Assert.Equal(2, node.ScaleY, 3);
        Assert.Equal(bw, node.ShapeWidth, 3);
        Assert.Equal(bh * 2, node.ShapeHeight, 3);
        Assert.Equal(100, node.X);                // 左边锚定
    }

    [Fact]
    public void IndependentScales_BreakAspectRatio()
    {
        var (d, node) = NewNode();

        d.ResizeNodeWidthTo(node, node.X + node.BaseWidth * 2);
        d.ResizeNodeHeightTo(node, node.Y + node.BaseHeight * 0.5);

        Assert.Equal(2, node.ScaleX, 3);
        Assert.Equal(0.5, node.ScaleY, 3);
        Assert.NotEqual(node.BaseWidth / node.BaseHeight, node.ShapeWidth / node.ShapeHeight, 3);
    }

    [Fact]
    public void SingleDirection_ClampedToCanvas_AndMinMax()
    {
        // 画布 300x300，部件在 (100,100)：横/纵最多只能填到对应边界
        var (d, node) = NewNode(300, 300);
        d.ResizeNodeWidthTo(node, 5000);
        d.ResizeNodeHeightTo(node, 5000);

        Assert.True(node.X + node.ShapeWidth <= 300 + 0.01);
        Assert.True(node.Y + node.ShapeHeight <= 300 + 0.01);

        d.ResizeNodeWidthTo(node, -500);
        d.ResizeNodeHeightTo(node, -500);
        Assert.Equal(NodeViewModel.MinScale, node.ScaleX);
        Assert.Equal(NodeViewModel.MinScale, node.ScaleY);

        var (big, bigNode) = NewNode(100000, 100000);
        big.ResizeNodeWidthTo(bigNode, 1e9);
        big.ResizeNodeHeightTo(bigNode, 1e9);
        Assert.Equal(NodeViewModel.MaxScale, bigNode.ScaleX);
        Assert.Equal(NodeViewModel.MaxScale, bigNode.ScaleY);
    }

    [Fact]
    public void Scale_SetsBothDirections()
    {
        var (_, node) = NewNode();

        node.ScaleX = 2;                 // 先造成非等比
        Assert.Equal(1, node.ScaleY, 3);

        node.Scale = 1.5;                // 等比入口：两个方向一起改
        Assert.Equal(1.5, node.ScaleX, 3);
        Assert.Equal(1.5, node.ScaleY, 3);
        Assert.Equal(1.5, node.Scale, 3);
    }

    [Fact]
    public void ScaleChange_FiresGeometryChangedOnce()
    {
        var (_, node) = NewNode();
        int count = 0;
        node.GeometryChanged += _ => count++;

        node.Scale = 2;                  // 等比：两维都改，只应触发一次重算
        Assert.Equal(1, count);

        node.ScaleX = 3;
        Assert.Equal(2, count);
    }

    [Fact]
    public void NonUniformScale_RoundTripsInArchive()
    {
        var (d, node) = NewNode();
        node.SetScale(2.5, 0.5);

        var reloaded = Assert.Single(DiagramViewModel.FromData(d.ToData()).Nodes);

        Assert.Equal(2.5, reloaded.ScaleX, 3);
        Assert.Equal(0.5, reloaded.ScaleY, 3);
        Assert.Equal(node.ShapeWidth, reloaded.ShapeWidth, 3);
        Assert.Equal(node.ShapeHeight, reloaded.ShapeHeight, 3);
    }

    [Fact]
    public void LegacyArchive_WithoutScaleXY_FallsBackToUniform()
    {
        var d = new DiagramViewModel(1000, 1000);
        var node = d.AddNode(NodeKind.Switch, x: 0, y: 0);
        node.Scale = 2;

        // 旧存档只有等比 Scale 字段
        var json = DiagramSerializer.ToJson(d.ToData());
        json = json.Replace("\"ScaleX\":2", "").Replace("\"ScaleY\":2", "");

        var reloaded = Assert.Single(DiagramViewModel.FromData(DiagramSerializer.FromJson(json)!).Nodes);

        Assert.Equal(2, reloaded.ScaleX, 3);
        Assert.Equal(2, reloaded.ScaleY, 3);
    }
}

/// <summary>三只缩放手柄的命中验证（STA）：右下角（等比）、右侧（只改宽）、下方（只改高）。</summary>
public class NodeResizeHandleTests
{
    [Fact]
    public void ThreeHandles_AreHittable()
    {
        RunOnSta(() =>
        {
            var d = new DiagramViewModel(800, 600);
            var node = d.AddNode(NodeKind.Switch, x: 10, y: 10);
            node.SetScale(2, 2);          // 64x48 → 128x96
            node.IsSelected = true;       // 手柄在选中（或悬停）时才可抓

            var ctrl = new TestNodeControl { DataContext = node };
            double w = node.ShapeWidth, h = node.ShapeHeight;

            Assert.NotNull(ctrl.HitTest(new Point(w - 4, h - 4)));                  // 右下角
            Assert.NotNull(ctrl.HitTest(new Point(w - 4, (h - 8) / 2 + 4)));        // 右侧中部
            Assert.NotNull(ctrl.HitTest(new Point((w - 8) / 2 + 4, h - 4)));        // 下边中部
            Assert.Null(ctrl.HitTest(new Point(w + 20, h + 20)));                   // 图形外不命中
        });
    }

    [Fact]
    public void Handles_FollowNonUniformSize()
    {
        RunOnSta(() =>
        {
            var d = new DiagramViewModel(800, 600);
            var node = d.AddNode(NodeKind.Switch, x: 10, y: 10);
            node.SetScale(2, 0.5);        // 128 x 24：手柄随各自方向的实际尺寸走
            node.IsSelected = true;

            var ctrl = new TestNodeControl { DataContext = node };
            double w = node.ShapeWidth, h = node.ShapeHeight;

            Assert.NotNull(ctrl.HitTest(new Point(w - 4, h - 4)));
            Assert.NotNull(ctrl.HitTest(new Point(w - 4, (h - 8) / 2 + 4)));
            Assert.NotNull(ctrl.HitTest(new Point((w - 8) / 2 + 4, h - 4)));
        });
    }

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

    private sealed class TestNodeControl : FlowNodeControl
    {
        public HitTestResult? HitTest(Point p) => HitTestCore(new PointHitTestParameters(p));
    }
}
