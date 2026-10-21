using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using FlowChartKit.Core;
using FlowChartKit.Data;

namespace FlowChartKit.ViewModels;

/// <summary>流路图视图模型：部件、管路、选择、吸附、序列化。</summary>
public class DiagramViewModel : ObservableBase
{
    private bool _isEditable = true;
    private bool _showNumbers = true;
    private NodeViewModel? _selectedNode;
    private ConnectionViewModel? _selectedConnection;
    private PortViewModel? _highlightedPort;

    public const double SnapRadius = 26;

    public DiagramViewModel(double width = 1400, double height = 900)
    {
        CanvasWidth = width;
        CanvasHeight = height;

        SetDirectionCommand = new RelayCommand(p =>
        {
            if (SelectedConnection is null || p is null) return;
            SelectedConnection.Direction = p switch
            {
                FlowMode f => f,
                string s => Enum.TryParse<FlowMode>(s, true, out var fm) ? fm : SelectedConnection.Direction,
                _ => SelectedConnection.Direction
            };
        }, _ => SelectedConnection != null);

        DeleteSelectedConnectionCommand = new RelayCommand(_ =>
        {
            if (SelectedConnection is { } c) RemoveConnection(c);
        }, _ => SelectedConnection != null);

        DeleteSelectedCommand = new RelayCommand(_ =>
        {
            if (SelectedConnection is { } c) RemoveConnection(c);
            else if (SelectedNode is { } n) RemoveNode(n);
        }, _ => SelectedConnection != null || SelectedNode != null);
    }

    private double _canvasWidth;
    private double _canvasHeight;

    /// <summary>画布宽度；载入存档时按存档值更新并发通知。</summary>
    public double CanvasWidth { get => _canvasWidth; private set => Set(ref _canvasWidth, value); }

    /// <summary>画布高度；载入存档时按存档值更新并发通知。</summary>
    public double CanvasHeight { get => _canvasHeight; private set => Set(ref _canvasHeight, value); }

    public ObservableCollection<NodeViewModel> Nodes { get; } = [];
    public ObservableCollection<ConnectionViewModel> Connections { get; } = [];

    /// <summary>部件 → 相连管路 的索引：节点移动/缩放时只重算自己的管路，避免全图扫描。</summary>
    private readonly Dictionary<NodeViewModel, List<ConnectionViewModel>> _connectionsByNode = new();

    /// <summary>编辑模式：true 允许拖动部件/端点并显示端点手柄；关闭时管路编号一并隐藏。</summary>
    public bool IsEditable
    {
        get => _isEditable;
        set
        {
            if (Set(ref _isEditable, value))
                Raise(nameof(ShowNumbers));
        }
    }

    /// <summary>是否在画布上显示管路编号（编号属编辑辅助信息，编辑模式关闭时恒为 false）。</summary>
    public bool ShowNumbers
    {
        get => _showNumbers && _isEditable;
        set => Set(ref _showNumbers, value);
    }

    public NodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (_selectedNode == value) return;
            if (_selectedNode != null) _selectedNode.IsSelected = false;
            _selectedNode = value;
            if (_selectedNode != null)
            {
                _selectedNode.IsSelected = true;
                if (_selectedConnection != null)
                {
                    _selectedConnection.IsSelected = false;
                    _selectedConnection = null;
                }
            }
            Raise();
            Raise(nameof(SelectedConnection));
        }
    }

    public ConnectionViewModel? SelectedConnection
    {
        get => _selectedConnection;
        set
        {
            if (_selectedConnection == value) return;
            if (_selectedConnection != null) _selectedConnection.IsSelected = false;
            _selectedConnection = value;
            if (_selectedConnection != null)
            {
                _selectedConnection.IsSelected = true;
                if (_selectedNode != null)
                {
                    _selectedNode.IsSelected = false;
                    _selectedNode = null;
                }
            }
            Raise();
            Raise(nameof(SelectedNode));
        }
    }

    /// <summary>清除管路选中状态（部件选中时调用）。</summary>
    public void ClearConnectionSelection() => SelectedConnection = null;

    public ICommand SetDirectionCommand { get; }
    public ICommand DeleteSelectedConnectionCommand { get; }
    public ICommand DeleteSelectedCommand { get; }

    // ---- 构建 ----

    /// <summary>分配下一个管路编号：现有最大编号 + 1（无则 1）。</summary>
    private int NextNumber()
    {
        int max = 0;
        foreach (var c in Connections)
            if (c.Number > max) max = c.Number;
        return max + 1;
    }

    public NodeViewModel AddNode(NodeKind kind, double x, double y, string? label = null, string? id = null)
    {
        var node = new NodeViewModel(kind, label, x, y, id);
        node.Diagram = this;
        node.PropertyChanged += OnNodePropertyChanged;
        node.GeometryChanged += OnNodeGeometryChanged;
        Nodes.Add(node);
        RenumberNodes();
        return node;
    }

    public ConnectionViewModel AddConnection(
        NodeViewModel sourceNode, PortViewModel sourcePort,
        NodeViewModel targetNode, PortViewModel targetPort,
        FlowMode direction = FlowMode.Forward, string? id = null)
    {
        var conn = new ConnectionViewModel(this, id)
        {
            SourceNode = sourceNode,
            SourcePort = sourcePort,
            TargetNode = targetNode,
            TargetPort = targetPort,
            Direction = direction
        };
        conn.Number = NextNumber();
        IndexConnection(conn, null, null);
        Connections.Add(conn);
        conn.Recompute();
        return conn;
    }

    public ConnectionViewModel AddConnection(ConnectionViewModel conn)
    {
        if (conn.Number == 0)
            conn.Number = NextNumber();
        IndexConnection(conn, null, null);
        Connections.Add(conn);
        conn.Recompute();
        return conn;
    }

    /// <summary>添加一条两端均为自由点的管路，拖动端点即可吸附到部件端口。</summary>
    public ConnectionViewModel AddFreeConnection(Point start, Point end, FlowMode direction = FlowMode.Forward)
    {
        var conn = new ConnectionViewModel(this) { Direction = direction };
        conn.SetFreeEnd(true, start);
        conn.SetFreeEnd(false, end);
        conn.Number = NextNumber();
        Connections.Add(conn);
        conn.Recompute();
        return conn;
    }

    public void RemoveConnection(ConnectionViewModel conn)
    {
        if (!Connections.Contains(conn)) return;
        if (SelectedConnection == conn) SelectedConnection = null;
        UnindexConnection(conn);
        Connections.Remove(conn);
    }

    /// <summary>移除部件及其所有相连管路。</summary>
    public void RemoveNode(NodeViewModel node)
    {
        if (!Nodes.Contains(node)) return;
        if (_connectionsByNode.TryGetValue(node, out var attached))
        {
            foreach (var c in attached.ToArray())
            {
                if (SelectedConnection == c) SelectedConnection = null;
                UnindexConnection(c);
                Connections.Remove(c);
            }
        }
        if (SelectedNode == node) SelectedNode = null;
        node.PropertyChanged -= OnNodePropertyChanged;
        node.GeometryChanged -= OnNodeGeometryChanged;
        node.Diagram = null; // 解除反向引用：被移除的节点不得再操作本图
        Nodes.Remove(node);
        RenumberNodes();
    }

    private void OnNodePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not NodeViewModel node) return;

        // 改名后刷新相关管路的显示名（#编号 源 → 目标）；位置/尺寸变化走 GeometryChanged
        if (e.PropertyName == nameof(NodeViewModel.Label))
        {
            if (_connectionsByNode.TryGetValue(node, out var list))
                foreach (var conn in list)
                    conn.RaiseDisplayName();
        }
    }

    /// <summary>节点位置/尺寸的一次逻辑变化：只重算与该节点相连的管路。</summary>
    private void OnNodeGeometryChanged(NodeViewModel node)
    {
        if (_connectionsByNode.TryGetValue(node, out var list))
            for (int i = 0; i < list.Count; i++)
                list[i].Recompute();
    }

    /// <summary>同类部件按 Nodes 集合顺序从 1 重排序号（添加/移除后调用；非拖动热路径，整表一遍遍历即可）。</summary>
    private void RenumberNodes()
    {
        var counters = new Dictionary<NodeKind, int>();
        foreach (var n in Nodes)
        {
            counters.TryGetValue(n.Kind, out var next);
            n.KindNumber = ++next;
            counters[n.Kind] = next;
        }
    }

    // ---- 部件 → 管路 索引 ----

    /// <summary>按管路当前端点重建索引项；oldSource/oldTarget 为端点变更前的节点（无则传 null）。</summary>
    internal void IndexConnection(ConnectionViewModel conn, NodeViewModel? oldSource, NodeViewModel? oldTarget)
    {
        if (!ReferenceEquals(oldSource, conn.SourceNode))
        {
            if (oldSource != null) RemoveFromIndex(oldSource, conn);
            if (conn.SourceNode != null) AddToIndex(conn.SourceNode, conn);
        }
        if (!ReferenceEquals(oldTarget, conn.TargetNode))
        {
            if (oldTarget != null) RemoveFromIndex(oldTarget, conn);
            if (conn.TargetNode != null) AddToIndex(conn.TargetNode, conn);
        }
    }

    private void UnindexConnection(ConnectionViewModel conn)
    {
        if (conn.SourceNode != null) RemoveFromIndex(conn.SourceNode, conn);
        if (conn.TargetNode != null) RemoveFromIndex(conn.TargetNode, conn);
    }

    private void AddToIndex(NodeViewModel node, ConnectionViewModel conn)
    {
        if (!_connectionsByNode.TryGetValue(node, out var list))
            _connectionsByNode[node] = list = new List<ConnectionViewModel>();
        if (!list.Contains(conn)) list.Add(conn);
    }

    private void RemoveFromIndex(NodeViewModel node, ConnectionViewModel conn)
    {
        if (_connectionsByNode.TryGetValue(node, out var list))
        {
            list.Remove(conn);
            if (list.Count == 0) _connectionsByNode.Remove(node);
        }
    }

    // ---- 端口吸附 ----

    public (PortViewModel? Port, Point Position) FindNearestPort(Point p, PortViewModel? exclude)
    {
        PortViewModel? best = null;
        double bestDist = SnapRadius * SnapRadius;

        foreach (var node in Nodes)
        {
            foreach (var port in node.Ports)
            {
                if (port == exclude) continue;
                var pos = port.AbsolutePoint;
                double d = (pos.X - p.X) * (pos.X - p.X) + (pos.Y - p.Y) * (pos.Y - p.Y);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = port;
                }
            }
        }
        return (best, best?.AbsolutePoint ?? p);
    }

    public void SetSnapHighlight(PortViewModel? port)
    {
        if (_highlightedPort == port) return;
        if (_highlightedPort != null) _highlightedPort.IsHighlighted = false;
        _highlightedPort = port;
        if (_highlightedPort != null) _highlightedPort.IsHighlighted = true;
    }

    public PortViewModel? FindPort(NodeViewModel node, string portId)
        => node.Ports.FirstOrDefault(p => p.Id == portId);

    /// <summary>按类型与同类序号查找部件（序号与画布上的序号徽章一致，从 1 开始）；未找到返回 null。</summary>
    public NodeViewModel? FindNode(NodeKind kind, int number)
        => Nodes.FirstOrDefault(n => n.Kind == kind && n.KindNumber == number);

    // ---- 端点拖拽编排（由管路控件驱动，吸附/高亮/重算集中在这里） ----

    /// <summary>开始拖动管路某端端点。</summary>
    public void BeginConnectionEndpointDrag(ConnectionViewModel conn, bool isStart, Point mousePos)
        => conn.BeginEndpointDrag(isStart, mousePos);

    /// <summary>拖动中：查找最近端口、更新吸附高亮并刷新折线。坐标夹取到画布内，防止自由端落到画布外无法拾取。</summary>
    public void UpdateConnectionEndpointDrag(ConnectionViewModel conn, bool isStart, Point pos)
    {
        pos = new Point(
            Math.Clamp(pos.X, 0, CanvasWidth),
            Math.Clamp(pos.Y, 0, CanvasHeight));
        var opposite = isStart ? conn.TargetPort : conn.SourcePort;
        var (snap, _) = FindNearestPort(pos, opposite);
        SetSnapHighlight(snap);
        conn.UpdateDragPoint(pos, snap);
    }

    /// <summary>结束拖动：落点提交（吸附到端口或成为自由端）并清除吸附高亮。</summary>
    public void EndConnectionEndpointDrag(ConnectionViewModel conn)
    {
        conn.EndEndpointDrag();
        SetSnapHighlight(null);
    }

    // ---- 序列化 ----

    public DiagramData ToData()
    {
        var data = new DiagramData { Width = CanvasWidth, Height = CanvasHeight };

        foreach (var n in Nodes)
        {
            data.Nodes.Add(new NodeData
            {
                Id = n.Id,
                Kind = n.Kind,
                Label = n.Label,
                X = n.X,
                Y = n.Y,
                Scale = n.Scale,
                Number = n.KindNumber
            });
        }

        foreach (var c in Connections)
        {
            var cd = new ConnectionData
            {
                Id = c.Id,
                Number = c.Number,
                Direction = c.Direction,
                SourceNodeId = c.SourceNode?.Id,
                SourcePortId = c.SourcePort?.Id,
                TargetNodeId = c.TargetNode?.Id,
                TargetPortId = c.TargetPort?.Id
            };
            if (c.FreeSource is { } fs) { cd.SourceFreeX = fs.X; cd.SourceFreeY = fs.Y; }
            if (c.FreeTarget is { } ft) { cd.TargetFreeX = ft.X; cd.TargetFreeY = ft.Y; }
            data.Connections.Add(cd);
        }
        return data;
    }

    public void Clear()
    {
        SelectedConnection = null;
        SelectedNode = null;
        SetSnapHighlight(null);
        Connections.Clear();
        _connectionsByNode.Clear();
        foreach (var n in Nodes)
        {
            n.PropertyChanged -= OnNodePropertyChanged;
            n.GeometryChanged -= OnNodeGeometryChanged;
            n.Diagram = null;
        }
        Nodes.Clear();
    }

    public static DiagramViewModel FromData(DiagramData data)
    {
        var diagram = new DiagramViewModel(data.Width, data.Height);
        diagram.Load(data);
        return diagram;
    }

    public void Load(DiagramData data)
    {
        Clear();
        if (data.Width > 0) CanvasWidth = data.Width;
        if (data.Height > 0) CanvasHeight = data.Height;
        var map = new Dictionary<string, NodeViewModel>();

        foreach (var nd in data.Nodes)
        {
            var node = AddNode(nd.Kind, nd.X, nd.Y, nd.Label, nd.Id);
            node.Scale = nd.Scale > 0 ? nd.Scale : 1;
            map[nd.Id] = node;
        }

        // 存档编号在全部部件就位后统一回填：AddNode 的顺序重排会覆盖逐个赋值；
        // 旧存档无编号（0）的部件保留推导值
        foreach (var nd in data.Nodes)
        {
            if (nd.Number > 0 && map.TryGetValue(nd.Id, out var node))
                node.KindNumber = nd.Number;
        }

        foreach (var cd in data.Connections)
        {
            var conn = new ConnectionViewModel(this, cd.Id) { Direction = cd.Direction };
            conn.Number = cd.Number > 0 ? cd.Number : NextNumber();

            if (cd.SourceNodeId is { } snId && map.TryGetValue(snId, out var sn))
            {
                var sp = FindPort(sn, cd.SourcePortId ?? "R");
                if (sp != null) conn.SetEnd(true, sn, sp);
                else if (cd.SourceFreeX is { } sx && cd.SourceFreeY is { } sy)
                    conn.SetFreeEnd(true, new Point(sx, sy));
            }
            else if (cd.SourceFreeX is { } sfx && cd.SourceFreeY is { } sfy)
            {
                conn.SetFreeEnd(true, new Point(sfx, sfy));
            }

            if (cd.TargetNodeId is { } tnId && map.TryGetValue(tnId, out var tn))
            {
                var tp = FindPort(tn, cd.TargetPortId ?? "L");
                if (tp != null) conn.SetEnd(false, tn, tp);
                else if (cd.TargetFreeX is { } tx && cd.TargetFreeY is { } ty)
                    conn.SetFreeEnd(false, new Point(tx, ty));
            }
            else if (cd.TargetFreeX is { } tfx && cd.TargetFreeY is { } tfy)
            {
                conn.SetFreeEnd(false, new Point(tfx, tfy));
            }

            Connections.Add(conn);
            conn.Recompute();
        }
    }
}
