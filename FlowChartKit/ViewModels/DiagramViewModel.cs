using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using FlowChartKit.Core;
using FlowChartKit.Data;
using FlowChartKit.Rendering;

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

    /// <summary>
    /// 运行时调整画布尺寸（控件"自适应宿主大小"模式用）：越界的部件与自由端同步收敛回新边界，
    /// 保证不出现滚动条。宽高 ≤ 0 时忽略。
    /// </summary>
    public void Resize(double width, double height)
    {
        if (width <= 0 || height <= 0) return;
        CanvasWidth = width;
        CanvasHeight = height;

        foreach (var n in Nodes)
        {
            var x = Math.Clamp(n.X, 0, Math.Max(0, width - n.ShapeWidth));
            var y = Math.Clamp(n.Y, 0, Math.Max(0, height - n.ShapeHeight));
            if (x != n.X || y != n.Y) n.MoveTo(x, y);
        }

        // 自由端同步钳位（复制集合再遍历，避免回调中修改索引集合）
        foreach (var c in _freeEndConnections.ToArray())
        {
            if (c.FreeSource is { } fs)
            {
                var p = new Point(Math.Clamp(fs.X, 0, width), Math.Clamp(fs.Y, 0, height));
                if (p != fs) c.SetFreeEnd(true, p);
            }
            if (c.FreeTarget is { } ft)
            {
                var p = new Point(Math.Clamp(ft.X, 0, width), Math.Clamp(ft.Y, 0, height));
                if (p != ft) c.SetFreeEnd(false, p);
            }
        }
    }

    public ObservableCollection<NodeViewModel> Nodes { get; } = [];
    public ObservableCollection<ConnectionViewModel> Connections { get; } = [];

    /// <summary>部件 → 相连管路 的索引：节点移动/缩放时只重算自己的管路，避免全图扫描。</summary>
    private readonly Dictionary<NodeViewModel, List<ConnectionViewModel>> _connectionsByNode = new();

    /// <summary>带自由端的管路集合：自由端不挂任何部件，部件命中让位判定需单独覆盖；
    /// 由 IndexConnection/UnindexConnection/Clear 统一维护。</summary>
    private readonly HashSet<ConnectionViewModel> _freeEndConnections = new();

    /// <summary>Load 批量添加部件期间抑制逐个序号重排，载入完毕后统一执行一次。</summary>
    private bool _suppressRenumber;

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
        if (!_suppressRenumber)
            RenumberNodes();
        return node;
    }

    public ConnectionViewModel AddConnection(
        NodeViewModel sourceNode, PortViewModel sourcePort,
        NodeViewModel targetNode, PortViewModel targetPort,
        FlowMode direction = FlowMode.Forward, string? id = null,
        PipeKind pipeKind = PipeKind.Standard)
    {
        var conn = new ConnectionViewModel(this, id)
        {
            SourceNode = sourceNode,
            SourcePort = sourcePort,
            TargetNode = targetNode,
            TargetPort = targetPort,
            Direction = direction,
            PipeKind = pipeKind
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
    public ConnectionViewModel AddFreeConnection(
        Point start, Point end, FlowMode direction = FlowMode.Forward,
        PipeKind pipeKind = PipeKind.Standard)
    {
        var conn = new ConnectionViewModel(this) { Direction = direction, PipeKind = pipeKind };
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

    /// <summary>同类部件按 Nodes 集合顺序从 1 重排序号（添加/移除后调用；非拖动热路径，整表一遍遍历即可）。
    /// 批量载入期间经 <see cref="_suppressRenumber"/> 抑制，完毕后统一执行。</summary>
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

    /// <summary>与本部件相连的管路（索引直达），无相连时返回空表。
    /// 供部件命中让位判定等高频场景复用，避免每次全图扫描。</summary>
    internal IReadOnlyList<ConnectionViewModel> GetConnectionsFor(NodeViewModel node)
        => _connectionsByNode.TryGetValue(node, out var list)
            ? list
            : (IReadOnlyList<ConnectionViewModel>)Array.Empty<ConnectionViewModel>();

    /// <summary>当前带自由端的管路（自由端不挂在任何部件上，命中让位判定需单独覆盖）。</summary>
    internal IEnumerable<ConnectionViewModel> FreeEndConnections => _freeEndConnections;

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

        // 自由端索引：SetEnd/SetFreeEnd 均在改值后调用本方法，此处读到的是最新端点状态
        if (conn.FreeSource is not null || conn.FreeTarget is not null)
            _freeEndConnections.Add(conn);
        else
            _freeEndConnections.Remove(conn);
    }

    private void UnindexConnection(ConnectionViewModel conn)
    {
        if (conn.SourceNode != null) RemoveFromIndex(conn.SourceNode, conn);
        if (conn.TargetNode != null) RemoveFromIndex(conn.TargetNode, conn);
        _freeEndConnections.Remove(conn);
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

    /// <summary>取消端点拖拽（Esc）：端点回退到拖拽前位置并清除吸附高亮。</summary>
    public void CancelConnectionEndpointDrag(ConnectionViewModel conn)
    {
        conn.CancelEndpointDrag();
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
                PipeKind = c.PipeKind,
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
        _freeEndConnections.Clear();
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

    /// <summary>整体重建图内容。载入前先校验全部部件类型可解析：存在未知 <see cref="NodeKind"/> 时直接抛
    /// <see cref="InvalidOperationException"/>，此时尚未 Clear，图内容原样保留，不会出现半载入状态。</summary>
    public void Load(DiagramData data)
    {
        foreach (var nd in data.Nodes)
            NodeDefinitionRegistry.Resolve(nd.Kind);

        Clear();
        if (data.Width > 0) CanvasWidth = data.Width;
        if (data.Height > 0) CanvasHeight = data.Height;
        var map = new Dictionary<string, NodeViewModel>();

        // 批量载入期间抑制逐个重排（避免 O(n²)），完毕后统一重排一次
        _suppressRenumber = true;
        try
        {
            foreach (var nd in data.Nodes)
            {
                var node = AddNode(nd.Kind, nd.X, nd.Y, nd.Label, nd.Id);
                node.Scale = nd.Scale > 0 ? nd.Scale : 1;
                map[nd.Id] = node;
            }
        }
        finally
        {
            _suppressRenumber = false;
        }
        RenumberNodes();

        // 存档编号在全部部件就位后统一回填：AddNode 的顺序重排会覆盖逐个赋值；
        // 旧存档无编号（0）的部件保留推导值
        foreach (var nd in data.Nodes)
        {
            if (nd.Number > 0 && map.TryGetValue(nd.Id, out var node))
                node.KindNumber = nd.Number;
        }

        // 缺编号管路按存档最大编号递增分配（原语义：始终取当前最大+1），预扫一次避免逐条全表扫描
        int nextConnNumber = 0;
        foreach (var cd in data.Connections)
            if (cd.Number > nextConnNumber) nextConnNumber = cd.Number;

        foreach (var cd in data.Connections)
        {
            // 宿主尚未注册该类型（如换了宿主程序）时回退到内置普通管路，避免解析失败
            var pipeKind = PipeDefinitionRegistry.IsAvailable(cd.PipeKind) ? cd.PipeKind : PipeKind.Standard;
            var conn = new ConnectionViewModel(this, cd.Id) { Direction = cd.Direction, PipeKind = pipeKind };
            conn.Number = cd.Number > 0 ? cd.Number : ++nextConnNumber;

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
