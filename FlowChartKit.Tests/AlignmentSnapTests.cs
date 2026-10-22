using System.Linq;
using System.Windows;
using FlowChartKit;
using FlowChartKit.ViewModels;
using Xunit;

namespace FlowChartKit.Tests;

/// <summary>
/// 对齐吸附的临时关闭（拖动中按住 Alt）与部件的画布边距读数。
/// </summary>
public class SnapSuppressAndMarginTests
{
    [Fact]
    public void SnapSuppressed_NodeKeepsRawPosition_AndNoGuides()
    {
        var d = new DiagramViewModel(800, 600);
        var anchor = d.AddNode(NodeKind.Led, 100, 100, "锚点");
        var moving = d.AddNode(NodeKind.Switch, 300, 300, "被拖");

        d.SnapSuppressed = true;
        var result = d.SnapNode(moving, anchor.X + 2, 300);

        Assert.Equal(anchor.X + 2, result.X, 3);
        Assert.Empty(result.Guides);
    }

    [Fact]
    public void SnapSuppressed_SegmentKeepsRawPosition()
    {
        var d = new DiagramViewModel(1000, 1000);
        d.AddFreeConnection(new Point(300, 200), new Point(600, 350));
        var conn = d.AddFreeConnection(new Point(0, 0), new Point(200, 120));

        d.SnapSuppressed = true;
        var snap = d.SnapConnectionSegment(conn, horizontal: true, pos: 203);

        Assert.Equal(203, snap.Pos, 3);
        Assert.Empty(snap.Guides);
    }

    [Fact]
    public void SnapSuppressed_DoesNotChangeConfiguredSwitch()
    {
        var d = new DiagramViewModel(800, 600);
        d.AlignmentSnapEnabled = false;

        d.SnapSuppressed = true;
        Assert.False(d.AlignmentSnapEnabled);

        // 复位临时关闭后，仍按宿主配置（关闭）工作
        d.SnapSuppressed = false;
        Assert.False(d.AlignmentSnapEnabled);
    }

    [Fact]
    public void GetNodeMargins_ReturnsDistancesToCanvasEdges()
    {
        var d = new DiagramViewModel(800, 600);
        var node = d.AddNode(NodeKind.Led, 100, 40, "灯");

        var (left, top, right, bottom) = d.GetNodeMargins(node);

        Assert.Equal(100, left, 3);
        Assert.Equal(40, top, 3);
        Assert.Equal(800 - 100 - node.ShapeWidth, right, 3);
        Assert.Equal(600 - 40 - node.ShapeHeight, bottom, 3);
    }

    [Fact]
    public void GetNodeMargins_ClampsAtZero_WhenNodeIsClampedInsideCanvas()
    {
        var d = new DiagramViewModel(800, 600);
        var node = d.AddNode(NodeKind.Led, 0, 0, "灯");

        var (left, top, _, _) = d.GetNodeMargins(node);
        Assert.Equal(0, left, 3);
        Assert.Equal(0, top, 3);

        // 贴右下角：两个方向均为 0（不出现负值）
        node.MoveTo(800 - node.ShapeWidth, 600 - node.ShapeHeight);
        var margins = d.GetNodeMargins(node);
        Assert.Equal(0, margins.Right, 3);
        Assert.Equal(0, margins.Bottom, 3);
    }
}

public class AlignmentSnapTests
{
    private static (DiagramViewModel Diagram, NodeViewModel Anchor, NodeViewModel Moving) CreatePair()
    {
        var d = new DiagramViewModel(800, 600);
        var anchor = d.AddNode(NodeKind.Led, 100, 100, "锚点");
        var moving = d.AddNode(NodeKind.Switch, 300, 300, "被拖");
        return (d, anchor, moving);
    }

    [Fact]
    public void SnapNode_AlignsLeftEdges_AndEmitsVerticalGuide()
    {
        var (d, anchor, moving) = CreatePair();

        // 左边缘差 2px（阈值 6 内）：吸附到锚点左边缘，产生一条竖线
        var result = d.SnapNode(moving, anchor.X + 2, 300);

        Assert.Equal(anchor.X, result.X);
        Assert.Contains(result.Guides, g => g.IsVertical && g.Offset == anchor.X);
    }

    [Fact]
    public void SnapNode_AlignsHorizontalCenters()
    {
        var (d, anchor, moving) = CreatePair();

        // 被拖部件中心 = 锚点中心 + 3px：吸附到居中
        double targetX = anchor.X + anchor.ShapeWidth / 2 - moving.ShapeWidth / 2 + 3;
        var result = d.SnapNode(moving, targetX, 300);

        double expectedCenter = anchor.X + anchor.ShapeWidth / 2;
        Assert.Equal(expectedCenter, result.X + moving.ShapeWidth / 2, 3);
        Assert.Contains(result.Guides, g => g.IsVertical && g.Offset == expectedCenter);
    }

    [Fact]
    public void SnapNode_AlignsVertically_AndEmitsHorizontalGuide()
    {
        var (d, anchor, moving) = CreatePair();

        var result = d.SnapNode(moving, 300, anchor.Y + 2);

        // 纵向吸附后必然与锚点的 上边 / 中线 / 下边 之一严格重合
        Assert.True(AlignedVertically(result.Y, moving, anchor));
        Assert.Contains(result.Guides, g => !g.IsVertical && IsAnchorLine(g.Offset, anchor));
    }

    /// <summary>三种纵向对齐（上 / 居中 / 下）分别可达：阈值调小以排除相邻候选的干扰。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void SnapNode_EachVerticalAlignment_IsReachable(int mode)
    {
        var (d, anchor, moving) = CreatePair();
        d.AlignmentSnapDistance = 2;

        double y = mode switch
        {
            0 => anchor.Y + 1,                                                              // 上边
            1 => anchor.Y + anchor.ShapeHeight / 2 - moving.ShapeHeight / 2 + 1,            // 居中
            _ => anchor.Y + anchor.ShapeHeight - moving.ShapeHeight + 1,                    // 下边
        };

        var result = d.SnapNode(moving, 300, y);

        switch (mode)
        {
            case 0:
                Assert.Equal(anchor.Y, result.Y);
                break;
            case 1:
                Assert.Equal(anchor.Y + anchor.ShapeHeight / 2, result.Y + moving.ShapeHeight / 2, 3);
                break;
            default:
                Assert.Equal(anchor.Y + anchor.ShapeHeight, result.Y + moving.ShapeHeight, 3);
                break;
        }
        Assert.Contains(result.Guides, g => !g.IsVertical);
    }

    private static bool AlignedVertically(double y, NodeViewModel moving, NodeViewModel anchor)
        => Nearly(y, anchor.Y)
        || Nearly(y + moving.ShapeHeight / 2, anchor.Y + anchor.ShapeHeight / 2)
        || Nearly(y + moving.ShapeHeight, anchor.Y + anchor.ShapeHeight);

    private static bool IsAnchorLine(double offset, NodeViewModel anchor)
        => Nearly(offset, anchor.Y)
        || Nearly(offset, anchor.Y + anchor.ShapeHeight / 2)
        || Nearly(offset, anchor.Y + anchor.ShapeHeight);

    private static bool Nearly(double a, double b) => System.Math.Abs(a - b) < 0.001;

    [Fact]
    public void SnapNode_NoCandidate_ReturnsOriginalPosition_WithoutGuides()
    {
        var (d, _, moving) = CreatePair();

        var result = d.SnapNode(moving, 500, 500);

        Assert.Equal(500, result.X);
        Assert.Equal(500, result.Y);
        Assert.Empty(result.Guides);
    }

    [Fact]
    public void SnapNode_RespectsDistanceThreshold()
    {
        var (d, anchor, moving) = CreatePair();

        d.AlignmentSnapDistance = 2;
        // 差 5px 超过阈值 2：不吸附
        var result = d.SnapNode(moving, anchor.X + 5, 300);
        Assert.Equal(anchor.X + 5, result.X);
        Assert.Empty(result.Guides);
    }

    [Fact]
    public void SnapNode_Disabled_DoesNotSnap()
    {
        var (d, anchor, moving) = CreatePair();

        d.AlignmentSnapEnabled = false;
        var result = d.SnapNode(moving, anchor.X + 2, anchor.Y + 2);

        Assert.Equal(anchor.X + 2, result.X);
        Assert.Equal(anchor.Y + 2, result.Y);
        Assert.Empty(result.Guides);
    }

    [Fact]
    public void AlignmentGuides_SetAndClear_RaiseNotification()
    {
        var d = new DiagramViewModel(800, 600);
        int raised = 0;
        d.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DiagramViewModel.AlignmentGuides)) raised++;
        };

        d.SetAlignmentGuides([new AlignmentGuide(true, 10, 0, 100)]);
        Assert.Single(d.AlignmentGuides);
        Assert.Equal(1, raised);

        d.ClearAlignmentGuides();
        Assert.Empty(d.AlignmentGuides);
        Assert.Equal(2, raised);

        // 重复清空不产生多余通知
        d.ClearAlignmentGuides();
        Assert.Equal(2, raised);
    }

    [Fact]
    public void SnapNode_GuideSpanCoversBothNodes()
    {
        var (d, anchor, moving) = CreatePair();

        var result = d.SnapNode(moving, anchor.X + 2, 300);
        var guide = result.Guides.First(g => g.IsVertical);

        double top = System.Math.Min(300, anchor.Y);
        double bottom = System.Math.Max(300 + moving.ShapeHeight, anchor.Y + anchor.ShapeHeight);

        Assert.True(guide.Start <= top);
        Assert.True(guide.End >= bottom);
    }
}
