using System.Windows;
using FlowChartKit;
using FlowChartKit.ViewModels;
using Xunit;

namespace FlowChartKit.Tests;

public class DiagramViewModelTests
{
    private static (DiagramViewModel Diagram, NodeViewModel A, NodeViewModel B) NewDiagramWithTwoNodes()
    {
        var d = new DiagramViewModel(800, 600);
        var a = d.AddNode(NodeKind.PumpModule, 100, 100, "A");
        var b = d.AddNode(NodeKind.SampleCup, 300, 200, "B");
        return (d, a, b);
    }

    [Fact]
    public void AddConnection_AssignsSequentialNumbers()
    {
        var (d, a, b) = NewDiagramWithTwoNodes();
        var c1 = d.AddConnection(a, a.Ports[0], b, b.Ports[0]);
        var c2 = d.AddConnection(a, a.Ports[0], b, b.Ports[0]);
        Assert.Equal(1, c1.Number);
        Assert.Equal(2, c2.Number);
    }

    [Fact]
    public void Selection_NodeAndConnectionAreMutuallyExclusive()
    {
        var (d, a, b) = NewDiagramWithTwoNodes();
        var conn = d.AddConnection(a, a.Ports[0], b, b.Ports[0]);

        d.SelectedConnection = conn;
        Assert.True(conn.IsSelected);

        d.SelectedNode = a;
        Assert.False(conn.IsSelected);
        Assert.Null(d.SelectedConnection);
        Assert.True(a.IsSelected);
    }

    [Fact]
    public void RemoveNode_RemovesAttachedConnections()
    {
        var (d, a, b) = NewDiagramWithTwoNodes();
        d.AddConnection(a, a.Ports[0], b, b.Ports[0]);
        d.AddFreeConnection(new Point(0, 0), new Point(50, 50));

        d.RemoveNode(a);

        Assert.Single(d.Connections); // 只剩自由连线
        Assert.DoesNotContain(d.Nodes, n => n == a);
    }

    [Fact]
    public void MoveTo_RecomputesOnlyAttachedConnectionPoints()
    {
        var (d, a, b) = NewDiagramWithTwoNodes();
        var attached = d.AddConnection(a, a.Ports[0], b, b.Ports[0]);
        var detached = d.AddFreeConnection(new Point(0, 0), new Point(50, 50));

        var attachedBefore = attached.Points.ToArray();
        var detachedBefore = detached.Points;

        a.MoveTo(a.X + 40, a.Y + 30);

        Assert.NotEqual(attachedBefore, attached.Points);
        Assert.Same(detachedBefore, detached.Points); // 不相连的管路不重算（数组引用不变）
    }

    [Fact]
    public void FindNearestPort_WithinSnapRadius_ReturnsPort()
    {
        var (d, a, _) = NewDiagramWithTwoNodes();
        var port = a.Ports[0];
        var p = port.AbsolutePoint;

        var (found, _) = d.FindNearestPort(new Point(p.X + 5, p.Y + 5), exclude: null);
        Assert.Same(port, found);
    }

    [Fact]
    public void FindNearestPort_FarAway_ReturnsNull()
    {
        var (d, _, _) = NewDiagramWithTwoNodes();
        var (found, pos) = d.FindNearestPort(new Point(790, 590), exclude: null);
        Assert.Null(found);
        Assert.Equal(new Point(790, 590), pos);
    }

    [Fact]
    public void FindNearestPort_RespectsExclude()
    {
        var (d, a, _) = NewDiagramWithTwoNodes();
        var port = a.Ports[0];
        var p = port.AbsolutePoint;

        var (found, _) = d.FindNearestPort(new Point(p.X + 2, p.Y + 2), exclude: port);
        Assert.NotSame(port, found);
    }

    [Fact]
    public void EndpointDrag_SnapToPort_CommitsEndpoint()
    {
        var (d, a, b) = NewDiagramWithTwoNodes();
        var conn = d.AddFreeConnection(new Point(0, 0), new Point(50, 50));

        var targetPort = b.Ports[0];
        var target = targetPort.AbsolutePoint;

        d.BeginConnectionEndpointDrag(conn, isStart: false, new Point(50, 50));
        d.UpdateConnectionEndpointDrag(conn, isStart: false, new Point(target.X + 3, target.Y + 3));
        d.EndConnectionEndpointDrag(conn);

        Assert.Same(b, conn.TargetNode);
        Assert.Same(targetPort, conn.TargetPort);
        Assert.Null(conn.FreeTarget); // 已接端口后不再是自由端
        Assert.False(targetPort.IsHighlighted); // 结束后高亮已清除
    }
}
