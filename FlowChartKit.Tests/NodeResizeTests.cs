using System.Windows;
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
