using System.Linq;
using System.Windows;
using FlowChartKit.ViewModels;
using Xunit;

namespace FlowChartKit.Tests;

/// <summary>
/// 拖动正交管路线段时的对齐吸附：与**其它管路**的水平 / 竖直段共线对齐（不考虑部件），
/// 与部件拖动吸附共用同一套开关与阈值。
/// </summary>
public class SegmentSnapTests
{
    // 参考管路：(300,200) → (600,350)，正交走线后折点为
    // [(300,200),(284,200),(284,350),(600,350)]：水平段 Y=200 / Y=350，竖直段 X=284
    private static DiagramViewModel NewDiagram()
    {
        var d = new DiagramViewModel(1000, 1000);
        d.AddFreeConnection(new Point(300, 200), new Point(600, 350));
        return d;
    }

    // 被拖管路：(0,0) → (200,120)，折点 [(0,0),(-16,0),(-16,120),(200,120)]
    private static ConnectionViewModel NewDragged(DiagramViewModel d)
        => d.AddFreeConnection(new Point(0, 0), new Point(200, 120));

    [Fact]
    public void HorizontalSegment_AlignsWithOtherPipeHorizontalRun()
    {
        var d = NewDiagram();
        var conn = NewDragged(d);

        // 拖水平段：距参考管路水平段 Y=200 仅 3px（阈值 6 内）
        var snap = d.SnapConnectionSegment(conn, horizontal: true, pos: 203);

        Assert.Equal(200, snap.Pos, 3);
        var guide = Assert.Single(snap.Guides);
        Assert.False(guide.IsVertical);          // 横线：Y 方向对齐
        Assert.Equal(200, guide.Offset, 3);
    }

    [Fact]
    public void VerticalSegment_AlignsWithOtherPipeVerticalRun()
    {
        var d = NewDiagram();
        var conn = NewDragged(d);

        // 拖竖直段：距参考管路竖直段 X=284 仅 2px
        var snap = d.SnapConnectionSegment(conn, horizontal: false, pos: 286);

        Assert.Equal(284, snap.Pos, 3);
        var guide = Assert.Single(snap.Guides);
        Assert.True(guide.IsVertical);           // 竖线：X 方向对齐
        Assert.Equal(284, guide.Offset, 3);
    }

    [Fact]
    public void GuideSpansBothPipes()
    {
        var d = NewDiagram();
        var conn = NewDragged(d);   // 折点 X 范围 -16..200

        // 横线跨度应覆盖被拖管路与参考水平段（X 284..300）的并集
        var snap = d.SnapConnectionSegment(conn, horizontal: true, pos: 203);
        var guide = Assert.Single(snap.Guides);

        Assert.True(guide.Start <= -16);
        Assert.True(guide.End >= 300);
    }

    [Fact]
    public void OutsideThreshold_NoSnap()
    {
        var d = NewDiagram();
        var conn = NewDragged(d);

        // 距最近的水平段（Y=200）也有 50px
        var snap = d.SnapConnectionSegment(conn, horizontal: true, pos: 150);

        Assert.Equal(150, snap.Pos, 3);
        Assert.Empty(snap.Guides);
    }

    [Fact]
    public void Disabled_ReturnsRawPosition()
    {
        var d = NewDiagram();
        d.AlignmentSnapEnabled = false;
        var conn = NewDragged(d);

        var snap = d.SnapConnectionSegment(conn, horizontal: true, pos: 201);

        Assert.Equal(201, snap.Pos, 3);
        Assert.Empty(snap.Guides);
    }

    [Fact]
    public void NodesDoNotParticipate()
    {
        var d = NewDiagram();
        d.AddNode(NodeKind.Switch, x: 400, y: 500);   // 部件上边 Y=500
        var conn = NewDragged(d);

        // 距部件上边仅 3px，但管路吸附不考虑部件 → 原样返回
        var snap = d.SnapConnectionSegment(conn, horizontal: true, pos: 503);

        Assert.Equal(503, snap.Pos, 3);
        Assert.Empty(snap.Guides);
    }

    [Fact]
    public void DragPipeline_ShowsGuidesDuringDragAndClearsOnEnd()
    {
        var d = NewDiagram();
        var conn = NewDragged(d);

        d.BeginConnectionSegmentDrag(conn);
        d.UpdateConnectionSegmentDrag(conn, horizontal: true, pos: 203);

        // 拖动中：捕捉线可见，且绕行位置已吸附到参考管路的水平段
        Assert.Single(d.AlignmentGuides);
        Assert.Equal(200, conn.DetourY!.Value, 3);

        d.EndConnectionSegmentDrag(conn);
        Assert.Empty(d.AlignmentGuides);      // 结束即清除
        Assert.Equal(200, conn.DetourY!.Value, 3);      // 吸附结果保留

        // 复位回自动走线
        conn.SetDetour(null, null);
        Assert.Null(conn.DetourY);
    }

    [Fact]
    public void CancelDrag_ClearsGuides()
    {
        var d = NewDiagram();
        var conn = NewDragged(d);

        d.BeginConnectionSegmentDrag(conn);
        d.UpdateConnectionSegmentDrag(conn, horizontal: true, pos: 203);
        Assert.NotEmpty(d.AlignmentGuides);

        d.CancelConnectionSegmentDrag(conn);
        Assert.Empty(d.AlignmentGuides);
        Assert.Null(conn.DetourY);            // Esc：回退到拖动前
    }
}
