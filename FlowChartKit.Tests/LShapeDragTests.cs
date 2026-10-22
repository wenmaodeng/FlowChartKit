using System.Linq;
using System.Windows;
using FlowChartKit.ViewModels;
using Xunit;

namespace FlowChartKit.Tests;

/// <summary>L 形（单折点）正交管路的可拖动区域判定与拖动效果。</summary>
public class LShapeDragTests
{
    /// <summary>
    /// 构造一条 L 形管路：起点水平向右出线、终点自上方端口向上出线，
    /// 路由后被 Clean 合成为 3 个点的单折点路径。
    /// </summary>
    private static (DiagramViewModel Diagram, ConnectionViewModel Conn) NewLShape()
    {
        var d = new DiagramViewModel(1000, 1000);
        var a = d.AddNode(NodeKind.Switch, x: 0, y: 0);        // 右端口：水平向右出线
        var b = d.AddNode(NodeKind.Led, x: 100, y: 100);       // 上端口：竖直向上出线
        var aPort = a.Ports.First(p => p.Dir == PortDir.Right);
        var bPort = b.Ports.First(p => p.Dir == PortDir.Up);
        var conn = d.AddConnection(a, aPort, b, bPort);

        Assert.Equal(3, conn.Points.Length);   // 前提：确实是 L 形
        return (d, conn);
    }

    [Fact]
    public void LShape_MiddlePartIsDraggable()
    {
        var (_, conn) = NewLShape();
        var pts = conn.Points;

        // 水平段中部（离起点远于一段端口出线）可拖，且判定为水平段
        Assert.True(conn.TryHitDraggableSegment(new Point((pts[0].X + pts[1].X) / 2, pts[0].Y), out bool horizontal));
        Assert.True(horizontal);

        // 竖直段中部可拖，且判定为竖直段
        Assert.True(conn.TryHitDraggableSegment(new Point(pts[1].X, (pts[1].Y + pts[2].Y) / 2), out bool vertical));
        Assert.False(vertical);
    }

    [Fact]
    public void LShape_PortStubsAreNotDraggable()
    {
        var (_, conn) = NewLShape();
        var pts = conn.Points;

        // 起点侧端口出线（前 16px）不可拖
        Assert.False(conn.TryHitDraggableSegment(new Point(pts[0].X + 4, pts[0].Y), out _));

        // 终点侧端口出线（末 16px）不可拖
        Assert.False(conn.TryHitDraggableSegment(new Point(pts[2].X, pts[2].Y + 4), out _));
    }

    [Fact]
    public void LShape_DragTurnsIntoDetourAndStaysOrthogonal()
    {
        var (d, conn) = NewLShape();
        var pts = conn.Points.ToArray();

        // 拖竖直段（左右移动）：命中竖直段中部后把该段拖到新的 X
        Assert.True(conn.TryHitDraggableSegment(new Point(pts[1].X, (pts[1].Y + pts[2].Y) / 2), out bool horizontal));
        Assert.False(horizontal);

        d.BeginConnectionSegmentDrag(conn);
        d.UpdateConnectionSegmentDrag(conn, horizontal, pts[1].X - 40);
        d.EndConnectionSegmentDrag(conn);

        var dragged = conn.Points;
        Assert.True(dragged.Length >= 4);                      // 由 L 形变成带绕行的 Z 形
        Assert.Equal(pts[1].X - 40, conn.DetourX);             // 绕行位置已记录
        Assert.Equal(pts[0], dragged[0]);                      // 起点端口不动
        Assert.Equal(pts[^1], dragged[^1]);                    // 终点端口不动

        // 相邻折点仍横平竖直
        for (int i = 1; i < dragged.Length; i++)
        {
            var dx = Math.Abs(dragged[i].X - dragged[i - 1].X);
            var dy = Math.Abs(dragged[i].Y - dragged[i - 1].Y);
            Assert.True(dx < 0.01 || dy < 0.01);
        }

        // 复位回自动走线后仍是原来的 L 形
        conn.SetDetour(null, null);
        Assert.Equal(pts, conn.Points);
    }

    [Fact]
    public void StraightRouting_HasNoDraggableSegment()
    {
        var d = new DiagramViewModel(1000, 1000);
        var conn = d.AddFreeConnection(new Point(0, 0), new Point(200, 120), routing: PipeRouting.Straight);

        // 直线布局不走正交拖动
        Assert.False(conn.TryHitDraggableSegment(new Point(100, 60), out _));
    }
}
