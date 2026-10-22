using System.Linq;
using System.Windows;
using FlowChartKit;
using FlowChartKit.Data;
using FlowChartKit.Routing;
using FlowChartKit.ViewModels;
using Xunit;

namespace FlowChartKit.Tests;

public class PipeRoutingTests
{
    private static DiagramViewModel NewDiagram() => new(800, 600);

    // ---- 正交布线器 ----

    [Fact]
    public void Route_Default_ProducesZShape()
    {
        // 起点向右出线、终点向左出线且不同高：经典 Z 形
        // （端口短直段与首段共线，由 Clean 合并，故中间不计 (16,0)）
        var pts = OrthogonalRouter.Route(new Point(0, 0), PortDir.Right, new Point(100, 50), PortDir.Left);

        Assert.Equal(new[]
        {
            new Point(0, 0),
            new Point(84, 0),
            new Point(84, 50),
            new Point(100, 50)
        }, pts);
    }

    [Fact]
    public void Route_Default_IsUnchangedWithExplicitNullDetours()
        => Assert.Equal(
            OrthogonalRouter.Route(new Point(0, 0), PortDir.Right, new Point(100, 50), PortDir.Left),
            OrthogonalRouter.Route(new Point(0, 0), PortDir.Right, new Point(100, 50), PortDir.Left, null, null));

    [Fact]
    public void Route_DetourY_MovesMiddleHorizontalSegment()
    {
        // 中间水平段抬到 Y=80：竖线落在两端中点（50），形成 (50,80) → (84,80) 的绕行水平段
        var pts = OrthogonalRouter.Route(new Point(0, 0), PortDir.Right, new Point(100, 50), PortDir.Left,
            null, 80);

        Assert.Contains(new Point(50, 80), pts);
        Assert.Contains(new Point(84, 80), pts);
        Assert.Equal(new Point(0, 0), pts[0]);
        Assert.Equal(new Point(100, 50), pts[^1]);
        // 全部相邻段仍保持横平竖直
        for (int i = 1; i < pts.Count; i++)
            Assert.True(Math.Abs(pts[i].X - pts[i - 1].X) < 0.01 || Math.Abs(pts[i].Y - pts[i - 1].Y) < 0.01);
    }

    [Fact]
    public void Route_DetourX_MovesMiddleVerticalSegment()
    {
        // 中间竖直段移到 X=40
        var pts = OrthogonalRouter.Route(new Point(0, 0), PortDir.Right, new Point(100, 50), PortDir.Left,
            40, null);

        Assert.Contains(new Point(40, 0), pts);
        Assert.Contains(new Point(40, 50), pts);
        Assert.Equal(new Point(0, 0), pts[0]);
        Assert.Equal(new Point(100, 50), pts[^1]);
    }

    [Fact]
    public void Route_VerticalOutgoing_DefaultIsLShape()
    {
        var pts = OrthogonalRouter.Route(new Point(0, 0), PortDir.Down, new Point(60, 80), PortDir.Up);

        // 默认：先竖直下到目标出线段高度（64 = 80 - 16 stub），再水平进入
        // （端口短直段与首段共线，由 Clean 合并）
        Assert.Equal(new[]
        {
            new Point(0, 0),
            new Point(0, 64),
            new Point(60, 64),
            new Point(60, 80)
        }, pts);
    }

    // ---- 连线布局 ----

    [Fact]
    public void Straight_Routing_ConnectsEndpointsDirectly()
    {
        var d = NewDiagram();
        var conn = d.AddFreeConnection(new Point(0, 0), new Point(100, 0),
            routing: PipeRouting.Straight);

        Assert.Equal(PipeRouting.Straight, conn.Routing);
        Assert.Equal(2, conn.Points.Length);
        Assert.Equal(new Point(0, 0), conn.Points[0]);
        Assert.Equal(new Point(100, 0), conn.Points[^1]);
    }

    [Fact]
    public void Curved_Routing_SetsControlPointsAlongPortDirections()
    {
        var d = NewDiagram();
        var conn = d.AddFreeConnection(new Point(0, 0), new Point(100, 0),
            routing: PipeRouting.Curved);

        Assert.Equal(PipeRouting.Curved, conn.Routing);
        Assert.Equal(2, conn.Points.Length);
        // 起点自由端方向为 Left（背离另一端），控制点 1 应在起点左侧
        Assert.True(conn.CurveControl1.X < 0);
        // 终点方向为 Right，控制点 2 应在终点右侧
        Assert.True(conn.CurveControl2.X > 100);
    }

    [Fact]
    public void Orthogonal_IsDefault_AndProducesPolyline()
    {
        var d = NewDiagram();
        var conn = d.AddFreeConnection(new Point(0, 0), new Point(100, 60));

        Assert.Equal(PipeRouting.Orthogonal, conn.Routing);
        Assert.True(conn.Points.Length >= 4);
    }

    [Fact]
    public void Routing_Change_RecomputesPoints()
    {
        var d = NewDiagram();
        var conn = d.AddFreeConnection(new Point(0, 0), new Point(100, 60));
        int orthoCount = conn.Points.Length;

        conn.Routing = PipeRouting.Straight;
        Assert.Equal(2, conn.Points.Length);
        Assert.True(orthoCount > 2);
    }

    // ---- 正交布局：拖动中间线段 ----

    [Fact]
    public void SegmentDrag_Horizontal_SetsDetourY()
    {
        var d = NewDiagram();
        var conn = d.AddFreeConnection(new Point(0, 0), new Point(100, 60));

        d.BeginConnectionSegmentDrag(conn);
        d.UpdateConnectionSegmentDrag(conn, horizontal: true, pos: 300);
        Assert.Equal(300, conn.DetourY);
        Assert.Null(conn.DetourX);

        // 路径里出现该高度的水平段
        Assert.Contains(conn.Points, p => Math.Abs(p.Y - 300) < 0.01);

        d.EndConnectionSegmentDrag(conn);
        Assert.Equal(300, conn.DetourY);   // 提交后保留
    }

    [Fact]
    public void SegmentDrag_Vertical_SetsDetourX()
    {
        var d = NewDiagram();
        var conn = d.AddFreeConnection(new Point(0, 0), new Point(100, 60));

        d.BeginConnectionSegmentDrag(conn);
        d.UpdateConnectionSegmentDrag(conn, horizontal: false, pos: 420);
        Assert.Equal(420, conn.DetourX);
        Assert.Contains(conn.Points, p => Math.Abs(p.X - 420) < 0.01);
    }

    [Fact]
    public void SegmentDrag_Cancel_RestoresPreviousDetours()
    {
        var d = NewDiagram();
        var conn = d.AddFreeConnection(new Point(0, 0), new Point(100, 60));

        d.BeginConnectionSegmentDrag(conn);
        d.UpdateConnectionSegmentDrag(conn, horizontal: true, pos: 300);
        d.CancelConnectionSegmentDrag(conn);

        Assert.Null(conn.DetourY);
    }

    [Fact]
    public void SegmentDrag_IgnoredWhenNotOrthogonal()
    {
        var d = NewDiagram();
        var conn = d.AddFreeConnection(new Point(0, 0), new Point(100, 60),
            routing: PipeRouting.Straight);

        d.BeginConnectionSegmentDrag(conn);
        d.UpdateConnectionSegmentDrag(conn, horizontal: true, pos: 300);

        Assert.Null(conn.DetourY);
        Assert.Equal(2, conn.Points.Length);
    }

    // ---- 持久化 ----

    [Fact]
    public void SetDetour_ResetsToAutoRouting()
    {
        var d = NewDiagram();
        var conn = d.AddFreeConnection(new Point(0, 0), new Point(100, 60));
        var defaultPoints = conn.Points.ToArray();

        d.BeginConnectionSegmentDrag(conn);
        d.UpdateConnectionSegmentDrag(conn, horizontal: true, pos: 300);
        d.EndConnectionSegmentDrag(conn);
        Assert.NotNull(conn.DetourY);

        // 复位后回到"按端口方向自动取位"，路径与新建时一致
        conn.SetDetour(null, null);
        Assert.Null(conn.DetourX);
        Assert.Null(conn.DetourY);
        Assert.Equal(defaultPoints, conn.Points);
    }

    [Fact]
    public void SegmentDrag_ClampedToCanvas()
    {
        var d = NewDiagram();   // 800 x 600
        var conn = d.AddFreeConnection(new Point(0, 0), new Point(100, 60));

        d.BeginConnectionSegmentDrag(conn);
        d.UpdateConnectionSegmentDrag(conn, horizontal: false, pos: 5000);
        Assert.Equal(800, conn.DetourX);

        d.UpdateConnectionSegmentDrag(conn, horizontal: true, pos: 5000);
        Assert.Equal(600, conn.DetourY);
    }

    [Fact]
    public void RoundTrip_PreservesRoutingAndDetours()
    {
        var d = NewDiagram();
        var conn = d.AddFreeConnection(new Point(0, 0), new Point(100, 60),
            routing: PipeRouting.Curved);

        // 先以正交布局写入绕行位置，再切成弧线保存：三项均需保留
        conn.Routing = PipeRouting.Orthogonal;
        d.BeginConnectionSegmentDrag(conn);
        d.UpdateConnectionSegmentDrag(conn, horizontal: false, pos: 321);
        d.EndConnectionSegmentDrag(conn);

        var data = d.ToData();
        var cd = Assert.Single(data.Connections);
        Assert.Equal(PipeRouting.Orthogonal, cd.Routing);
        Assert.Equal(321, cd.DetourX);

        var loaded = DiagramViewModel.FromData(data);
        var reloaded = Assert.Single(loaded.Connections);
        Assert.Equal(PipeRouting.Orthogonal, reloaded.Routing);
        Assert.Equal(321, reloaded.DetourX);
    }

    [Fact]
    public void Load_OldArchiveWithoutRouting_UsesOrthogonal()
    {
        // 旧存档 JSON 无 Routing/Detour 字段：按垂直水平回读，且不会产生绕行
        var json = """
        {
          "Version": 1, "Width": 800, "Height": 600,
          "Nodes": [],
          "Connections": [
            { "Id": "c1", "Number": 1, "SourceFreeX": 0, "SourceFreeY": 0,
              "TargetFreeX": 100, "TargetFreeY": 60, "Direction": "Forward" }
          ]
        }
        """;
        // 用库自带序列化器（含枚举字符串转换器）
        var data = DiagramSerializer.FromJson(json)!;

        var d = DiagramViewModel.FromData(data);
        var conn = Assert.Single(d.Connections);

        Assert.Equal(PipeRouting.Orthogonal, conn.Routing);
        Assert.Null(conn.DetourX);
        Assert.Null(conn.DetourY);
    }
}
