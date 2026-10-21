using System.Windows;
using FlowChartKit;
using FlowChartKit.Data;
using FlowChartKit.Demo.Nodes;
using FlowChartKit.ViewModels;
using Xunit;

namespace FlowChartKit.Tests;

public class DiagramViewModelTests
{
    private static (DiagramViewModel Diagram, NodeViewModel A, NodeViewModel B) NewDiagramWithTwoNodes()
    {
        var d = new DiagramViewModel(800, 600);
        var a = d.AddNode(DemoNodeKinds.PumpModule, 100, 100, "A");
        var b = d.AddNode(DemoNodeKinds.SampleCup, 300, 200, "B");
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

    [Fact]
    public void EndpointDrag_OutsideCanvas_ClampedToBounds()
    {
        var (d, _, _) = NewDiagramWithTwoNodes(); // 画布 800×600
        var conn = d.AddFreeConnection(new Point(10, 10), new Point(50, 50));

        d.BeginConnectionEndpointDrag(conn, isStart: false, new Point(50, 50));
        d.UpdateConnectionEndpointDrag(conn, isStart: false, new Point(1200, -80));
        d.EndConnectionEndpointDrag(conn);

        Assert.NotNull(conn.FreeTarget);
        Assert.Equal(800, conn.FreeTarget!.Value.X);
        Assert.Equal(0, conn.FreeTarget!.Value.Y);
    }

    [Fact]
    public void RemoveNode_ClearsNodeDiagramReference()
    {
        var (d, a, _) = NewDiagramWithTwoNodes();

        d.RemoveNode(a);

        Assert.Null(a.Diagram);
    }

    [Fact]
    public void Load_RestoresCanvasSize()
    {
        var d = new DiagramViewModel(800, 600);
        var data = new DiagramData { Width = 1600, Height = 1200 };

        d.Load(data);

        Assert.Equal(1600, d.CanvasWidth);
        Assert.Equal(1200, d.CanvasHeight);
    }

    [Fact]
    public void KindNumber_NumberedByKindInCollectionOrder()
    {
        var d = new DiagramViewModel(800, 600);
        var c1 = d.AddNode(DemoNodeKinds.SampleCup, 0, 0);
        var v1 = d.AddNode(DemoNodeKinds.ThreeWayValve, 0, 0);
        var c2 = d.AddNode(DemoNodeKinds.SampleCup, 0, 0);
        var v2 = d.AddNode(DemoNodeKinds.ThreeWayValve, 0, 0);
        var c3 = d.AddNode(DemoNodeKinds.SampleCup, 0, 0);

        // 同类型按集合顺序 1,2,3；不同类型各自独立计数
        Assert.Equal(1, c1.KindNumber);
        Assert.Equal(2, c2.KindNumber);
        Assert.Equal(3, c3.KindNumber);
        Assert.Equal(1, v1.KindNumber);
        Assert.Equal(2, v2.KindNumber);
    }

    [Fact]
    public void KindNumber_RenumbersAfterRemoval()
    {
        var d = new DiagramViewModel(800, 600);
        var a = d.AddNode(DemoNodeKinds.SampleCup, 0, 0);
        var b = d.AddNode(DemoNodeKinds.SampleCup, 0, 0);
        var c = d.AddNode(DemoNodeKinds.SampleCup, 0, 0);

        d.RemoveNode(b);

        Assert.Equal(1, a.KindNumber);
        Assert.Equal(2, c.KindNumber);
    }

    [Fact]
    public void KindNumber_RestoredFromLoadOrder()
    {
        var d = new DiagramViewModel(800, 600);
        d.AddNode(DemoNodeKinds.SampleCup, 0, 0);
        d.AddNode(DemoNodeKinds.SampleCup, 50, 0);

        // 保存时写入当前序号，载入复原
        var restored = DiagramViewModel.FromData(d.ToData());

        Assert.Equal(new[] { 1, 2 }, restored.Nodes.Select(n => n.KindNumber));
    }

    [Fact]
    public void KindNumber_LoadPrefersStoredNumber_FallsBackToDerived()
    {
        var data = new DiagramData();
        data.Nodes.Add(new NodeData { Id = "a", Kind = DemoNodeKinds.SampleCup, X = 0, Y = 0, Number = 5 });
        data.Nodes.Add(new NodeData { Id = "b", Kind = DemoNodeKinds.SampleCup, X = 50, Y = 0 }); // 旧存档无编号字段

        var d = DiagramViewModel.FromData(data);

        Assert.Equal(5, d.Nodes[0].KindNumber); // 存档值优先
        Assert.Equal(2, d.Nodes[1].KindNumber); // 缺失时回退为按集合顺序推导
    }

    [Fact]
    public void FindNode_MatchesKindAndKindNumber()
    {
        var d = new DiagramViewModel(800, 600);
        var c1 = d.AddNode(DemoNodeKinds.SampleCup, 0, 0);
        var c2 = d.AddNode(DemoNodeKinds.SampleCup, 50, 0);
        d.AddNode(DemoNodeKinds.ThreeWayValve, 100, 0);

        Assert.Same(c1, d.FindNode(DemoNodeKinds.SampleCup, 1));
        Assert.Same(c2, d.FindNode(DemoNodeKinds.SampleCup, 2));
        Assert.Null(d.FindNode(DemoNodeKinds.SampleCup, 3));   // 序号不存在
        Assert.Null(d.FindNode(DemoNodeKinds.PumpModule, 1));  // 类型不存在
    }

    [Fact]
    public void NodeState_SetRaisesPropertyChanged_DuplicateSuppressed()
    {
        var node = new NodeViewModel(DemoNodeKinds.SampleCup);
        int count = 0;
        node.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NodeViewModel.State)) count++;
        };

        node.State = "运行";
        node.State = "运行"; // 相同值不重复通知

        Assert.Equal(1, count);
        Assert.Equal("运行", node.State);
    }

    [Fact]
    public void Load_AssignsMissingConnectionNumbers_AfterStoredMax()
    {
        var data = new DiagramData();
        data.Connections.Add(new ConnectionData { Id = "c1", SourceFreeX = 10, SourceFreeY = 10, TargetFreeX = 100, TargetFreeY = 10, Number = 3 });
        data.Connections.Add(new ConnectionData { Id = "c2", SourceFreeX = 10, SourceFreeY = 50, TargetFreeX = 100, TargetFreeY = 50 }); // 旧存档缺编号
        data.Connections.Add(new ConnectionData { Id = "c3", SourceFreeX = 10, SourceFreeY = 90, TargetFreeX = 100, TargetFreeY = 90, Number = 5 });

        var d = DiagramViewModel.FromData(data);

        Assert.Equal(3, d.Connections.Single(c => c.Id == "c1").Number);
        Assert.Equal(6, d.Connections.Single(c => c.Id == "c2").Number); // 存档最大 5 之后递增
        Assert.Equal(5, d.Connections.Single(c => c.Id == "c3").Number);
    }

    [Fact]
    public void Load_AssignsSequentialNumbers_WhenAllMissing()
    {
        var data = new DiagramData();
        for (int i = 0; i < 3; i++)
            data.Connections.Add(new ConnectionData { Id = $"c{i}", SourceFreeX = 10, SourceFreeY = 10 + i * 40, TargetFreeX = 100, TargetFreeY = 10 + i * 40 });

        var d = DiagramViewModel.FromData(data);

        // 全部缺编号时按存档顺序依次 1,2,3
        Assert.Equal(new[] { 1, 2, 3 }, d.Connections.OrderBy(c => c.Id).Select(c => c.Number));
    }

    [Fact]
    public void AddConnection_AfterLoad_ContinuesFromStoredMax()
    {
        var data = new DiagramData();
        data.Connections.Add(new ConnectionData { Id = "c1", SourceFreeX = 10, SourceFreeY = 10, TargetFreeX = 100, TargetFreeY = 10, Number = 7 });

        var d = DiagramViewModel.FromData(data);
        var added = d.AddFreeConnection(new Point(0, 0), new Point(50, 50));

        // 载入后运行期新增：编号从存档最大值之后继续，与载入分配机制衔接
        Assert.Equal(8, added.Number);
    }

    // ---- Load 预校验（半载入防护下沉到公共入口） ----

    [Fact]
    public void Load_UnknownNodeKind_ThrowsWithoutPartialLoad()
    {
        var d = new DiagramViewModel(800, 600);
        d.AddNode(DemoNodeKinds.SampleCup, 10, 10);
        d.AddFreeConnection(new Point(0, 0), new Point(50, 50));

        var data = new DiagramData();
        data.Nodes.Add(new NodeData { Id = "ok", Kind = DemoNodeKinds.SampleCup, X = 0, Y = 0 });
        data.Nodes.Add(new NodeData { Id = "bad", Kind = (NodeKind)9999, X = 0, Y = 0 });

        Assert.Throws<InvalidOperationException>(() => d.Load(data));

        // 半载入被拒：尚未 Clear，载入前的部件与管路原样保留
        Assert.Single(d.Nodes);
        Assert.Single(d.Connections);
        Assert.Equal(10, d.Nodes[0].X);
    }

    // ---- 管路索引（部件命中让位判定复用） ----

    [Fact]
    public void GetConnectionsFor_ReturnsOnlyAttachedConnections()
    {
        var (d, a, b) = NewDiagramWithTwoNodes();
        var attached = d.AddConnection(a, a.Ports[0], b, b.Ports[0]);
        var free = d.AddFreeConnection(new Point(0, 0), new Point(50, 50));

        Assert.Contains(attached, d.GetConnectionsFor(a));
        Assert.DoesNotContain(free, d.GetConnectionsFor(a));
        Assert.Empty(d.GetConnectionsFor(new NodeViewModel(DemoNodeKinds.SampleCup, "orphan")));
    }

    [Fact]
    public void FreeEndIndex_TracksEndpointChanges()
    {
        var (d, a, _) = NewDiagramWithTwoNodes();
        var conn = d.AddFreeConnection(new Point(0, 0), new Point(50, 50));
        Assert.Contains(conn, d.FreeEndConnections);

        conn.SetEnd(true, a, a.Ports[0]); // 一端仍自由
        Assert.Contains(conn, d.FreeEndConnections);

        conn.SetEnd(false, a, a.Ports[0]); // 两端都接上
        Assert.DoesNotContain(conn, d.FreeEndConnections);

        conn.SetFreeEnd(true, new Point(10, 10)); // 再次放开一端
        Assert.Contains(conn, d.FreeEndConnections);
    }

    [Fact]
    public void FreeEndIndex_CleanedOnRemoveAndClear()
    {
        var (d, _, _) = NewDiagramWithTwoNodes();
        var conn = d.AddFreeConnection(new Point(0, 0), new Point(50, 50));

        d.RemoveConnection(conn);
        Assert.DoesNotContain(conn, d.FreeEndConnections);

        d.AddFreeConnection(new Point(0, 0), new Point(50, 50));
        d.Clear();
        Assert.Empty(d.FreeEndConnections);
    }

    // ---- 端点拖拽 Esc 取消 ----

    [Fact]
    public void CancelConnectionEndpointDrag_RevertsToPreDragAnchor()
    {
        var (d, a, _) = NewDiagramWithTwoNodes();
        var port = a.Ports[0];
        var conn = d.AddFreeConnection(new Point(0, 0), new Point(50, 50));
        conn.SetEnd(false, a, port);
        var pointsBefore = conn.Points.ToArray();

        d.BeginConnectionEndpointDrag(conn, isStart: false, new Point(50, 50));
        d.UpdateConnectionEndpointDrag(conn, isStart: false, new Point(700, 500)); // 拖远
        Assert.NotEqual(pointsBefore, conn.Points);

        d.SetSnapHighlight(port); // 模拟拖拽中的吸附高亮
        d.CancelConnectionEndpointDrag(conn);

        Assert.Same(a, conn.TargetNode);  // 回退到拖拽前的端口锚点
        Assert.Same(port, conn.TargetPort);
        Assert.Null(conn.FreeTarget);
        Assert.Equal(pointsBefore, conn.Points);
        Assert.False(port.IsHighlighted); // 吸附高亮已清除
    }
}
