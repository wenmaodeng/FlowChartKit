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

    // ---- 对齐捕捉（拖动部件时与其它部件对齐） ----

    private bool _alignmentSnapEnabled = true;
    private double _alignmentSnapDistance = 6.0;
    private IReadOnlyList<AlignmentGuide> _alignmentGuides = Array.Empty<AlignmentGuide>();

    /// <summary>拖动部件时是否启用与其它部件的对齐吸附（默认 true）。关闭后不再生成捕捉线。</summary>
    public bool AlignmentSnapEnabled
    {
        get => _alignmentSnapEnabled;
        set => Set(ref _alignmentSnapEnabled, value);
    }

    /// <summary>对齐吸附的触发距离（px，默认 6）：部件边/中线进入该距离内即吸附对齐。</summary>
    public double AlignmentSnapDistance
    {
        get => _alignmentSnapDistance;
        set => Set(ref _alignmentSnapDistance, Math.Max(0, value));
    }

    /// <summary>当前需要绘制的对齐参考线；拖动结束时清空。由画布的对齐线图层绘制。</summary>
    public IReadOnlyList<AlignmentGuide> AlignmentGuides
    {
        get => _alignmentGuides;
        private set => Set(ref _alignmentGuides, value);
    }

    /// <summary>更新参考线集合（传 null 或空集合表示清空）。</summary>
    public void SetAlignmentGuides(IReadOnlyList<AlignmentGuide>? guides)
    {
        // 拖动中每帧都会调用：无参考线且当前已为空时不重复发通知（避免每帧触发图层重绘）
        if (guides is null || guides.Count == 0)
        {
            if (_alignmentGuides.Count == 0) return;
            AlignmentGuides = Array.Empty<AlignmentGuide>();
            return;
        }
        AlignmentGuides = guides;
    }

    /// <summary>清空参考线（拖动结束/取消、或退出编辑模式时调用）。</summary>
    public void ClearAlignmentGuides() => SetAlignmentGuides(null);

    /// <summary>
    /// 计算部件落到 (x, y) 时与其它部件的对齐吸附：X 方向按左 / 居中 / 右，Y 方向按上 / 居中 / 下，
    /// 每轴取最近的一个候选（阈值内）并生成对应参考线。未启用或无候选时原样返回。
    /// </summary>
    public NodeSnapResult SnapNode(NodeViewModel node, double x, double y)
    {
        List<AlignmentGuide>? guides = null;
        if (!_alignmentSnapEnabled || _alignmentSnapDistance <= 0)
            return new NodeSnapResult(x, y, Array.Empty<AlignmentGuide>());

        double w = node.ShapeWidth;
        double h = node.ShapeHeight;
        double threshold = _alignmentSnapDistance;

        double? snapX = null, snapY = null;
        double bestX = threshold, bestY = threshold;   // 记录最小的绝对偏差
        AlignmentGuide? guideX = null, guideY = null;

        foreach (var other in Nodes)
        {
            if (ReferenceEquals(other, node)) continue;
            double ow = other.ShapeWidth;
            double oh = other.ShapeHeight;

            // 竖线（X 方向）：被拖部件的左/中/右 对齐到 参考部件的左/中/右
            TryMatch(x, w, other.X, ow, ref bestX, ref snapX, ref guideX,
                target => new AlignmentGuide(true, target,
                    Math.Min(y, other.Y) - GuidePad,
                    Math.Max(y + h, other.Y + oh) + GuidePad));

            // 横线（Y 方向）：被拖部件的上/中/下 对齐到 参考部件的上/中/下
            TryMatch(y, h, other.Y, oh, ref bestY, ref snapY, ref guideY,
                target => new AlignmentGuide(false, target,
                    Math.Min(x, other.X) - GuidePad,
                    Math.Max(x + w, other.X + ow) + GuidePad));
        }

        if (guideX != null) (guides ??= new List<AlignmentGuide>(2)).Add(guideX.Value);
        if (guideY != null) (guides ??= new List<AlignmentGuide>(2)).Add(guideY.Value);

        return new NodeSnapResult(snapX ?? x, snapY ?? y, guides ?? (IReadOnlyList<AlignmentGuide>)Array.Empty<AlignmentGuide>());
    }

    /// <summary>参考线在两端各延伸的长度（px），使线略微超出部件边缘便于观察。</summary>
    private const double GuidePad = 10;

    /// <summary>管路所有折点在 X 方向的范围（捕捉线跨度用）。</summary>
    private static (double Min, double Max) SpanX(ConnectionViewModel conn)
    {
        double min = double.MaxValue, max = double.MinValue;
        foreach (var p in conn.Points)
        {
            if (p.X < min) min = p.X;
            if (p.X > max) max = p.X;
        }
        return min > max ? (0, 0) : (min, max);
    }

    /// <summary>管路所有折点在 Y 方向的范围（捕捉线跨度用）。</summary>
    private static (double Min, double Max) SpanY(ConnectionViewModel conn)
    {
        double min = double.MaxValue, max = double.MinValue;
        foreach (var p in conn.Points)
        {
            if (p.Y < min) min = p.Y;
            if (p.Y > max) max = p.Y;
        }
        return min > max ? (0, 0) : (min, max);
    }

    /// <summary>
    /// 在"起点/中线/终点"三类对齐中找最近的候选：被拖部件位置 p（尺寸 s）对齐到参考部件位置 o（尺寸 os）。
    /// 找到更近的候选即更新偏差、吸附坐标与参考线。
    /// </summary>
    private static void TryMatch(double p, double s, double o, double os,
        ref double bestDelta, ref double? snap, ref AlignmentGuide? guide,
        Func<double, AlignmentGuide> makeGuide)
    {
        // 三条候选线：前沿对齐、中线对齐、后沿对齐
        Span<double> deltas = stackalloc double[3];
        deltas[0] = o - p;                              // 左/上 对齐
        deltas[1] = (o + os / 2) - (p + s / 2);         // 居中 对齐
        deltas[2] = (o + os) - (p + s);                 // 右/下 对齐

        for (int i = 0; i < 3; i++)
        {
            double d = Math.Abs(deltas[i]);
            if (d > bestDelta) continue;

            bestDelta = d;
            snap = p + deltas[i];
            double target = i switch { 0 => o, 1 => o + os / 2, _ => o + os };
            guide = makeGuide(target);
        }
    }

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
        PipeKind pipeKind = PipeKind.Standard,
        PipeRouting routing = PipeRouting.Orthogonal)
    {
        var conn = new ConnectionViewModel(this, id)
        {
            SourceNode = sourceNode,
            SourcePort = sourcePort,
            TargetNode = targetNode,
            TargetPort = targetPort,
            Direction = direction,
            PipeKind = pipeKind,
            Routing = routing
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
        PipeKind pipeKind = PipeKind.Standard,
        PipeRouting routing = PipeRouting.Orthogonal)
    {
        var conn = new ConnectionViewModel(this) { Direction = direction, PipeKind = pipeKind, Routing = routing };
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

    // ---- 正交管路：拖动中间线段（水平段上下移 / 竖直段左右移） ----

    /// <summary>开始拖动管路中间线段：记录拖动前的绕行位置，供 Esc 回退。</summary>
    public void BeginConnectionSegmentDrag(ConnectionViewModel conn)
    {
        ClearAlignmentGuides();
        conn.BeginSegmentDrag();
    }

    /// <summary>
    /// 拖动中间线段到指定坐标：<paramref name="horizontal"/> 为 true 表示水平段（按 Y 移动），
    /// 否则为竖直段（按 X 移动）。坐标先经对齐吸附（见 <see cref="SnapConnectionSegment"/>）再下发，
    /// 并同步刷新捕捉线；仅正交布局生效。
    /// </summary>
    public void UpdateConnectionSegmentDrag(ConnectionViewModel conn, bool horizontal, double pos)
    {
        var snap = SnapConnectionSegment(conn, horizontal, pos);
        SetAlignmentGuides(snap.Guides);
        conn.UpdateSegmentDrag(horizontal, snap.Pos);
    }

    /// <summary>结束段拖拽：提交当前绕行位置并清除捕捉线。</summary>
    public void EndConnectionSegmentDrag(ConnectionViewModel conn)
    {
        ClearAlignmentGuides();
        conn.EndSegmentDrag();
    }

    /// <summary>取消段拖拽（Esc）：绕行位置回退到拖动前并清除捕捉线。</summary>
    public void CancelConnectionSegmentDrag(ConnectionViewModel conn)
    {
        ClearAlignmentGuides();
        conn.CancelSegmentDrag();
    }

    /// <summary>
    /// 拖动正交管路线段时的**对齐吸附**：与**其它管路**的水平 / 竖直段共线对齐（不考虑部件）——
    /// <paramref name="horizontal"/> 为 true 表示拖水平段，按 Y 吸附到其它管路的水平段；
    /// 否则为拖竖直段，按 X 吸附到其它管路的竖直段（被拖管路自身不参与）。
    /// 取最近的一个候选（阈值内）并生成对应捕捉线；未启用或无候选时原样返回。
    /// 开关与阈值沿用部件吸附的 <see cref="AlignmentSnapEnabled"/> / <see cref="AlignmentSnapDistance"/>。
    /// </summary>
    public SegmentSnapResult SnapConnectionSegment(ConnectionViewModel conn, bool horizontal, double pos)
    {
        if (!_alignmentSnapEnabled || _alignmentSnapDistance <= 0)
            return new SegmentSnapResult(pos, Array.Empty<AlignmentGuide>());

        double best = _alignmentSnapDistance;
        double snapped = pos;
        double guideMin = 0, guideMax = 0;
        bool found = false;

        foreach (var other in Connections)
        {
            if (ReferenceEquals(other, conn)) continue;
            var pts = other.Points;
            for (int i = 1; i < pts.Length; i++)
            {
                var a = pts[i - 1];
                var b = pts[i];

                // 只与同走向的段对齐：水平段对 Y，竖直段对 X
                bool segHorizontal = Math.Abs(a.Y - b.Y) < 0.01;
                if (segHorizontal != horizontal) continue;

                double delta = Math.Abs((horizontal ? a.Y : a.X) - pos);
                if (delta > best) continue;
                best = delta;
                snapped = horizontal ? a.Y : a.X;
                found = true;

                // 捕捉线跨度：被拖管路与参考段的并集范围，便于观察
                var (spanMin, spanMax) = horizontal ? SpanX(conn) : SpanY(conn);
                double objMin = horizontal ? Math.Min(a.X, b.X) : Math.Min(a.Y, b.Y);
                double objMax = horizontal ? Math.Max(a.X, b.X) : Math.Max(a.Y, b.Y);
                guideMin = Math.Min(spanMin, objMin) - GuidePad;
                guideMax = Math.Max(spanMax, objMax) + GuidePad;
            }
        }

        if (!found) return new SegmentSnapResult(pos, Array.Empty<AlignmentGuide>());

        // 水平段生成横线（Y 对齐），竖直段生成竖线（X 对齐）
        var guide = new AlignmentGuide(!horizontal, snapped, guideMin, guideMax);
        return new SegmentSnapResult(snapped, new[] { guide });
    }

    /// <summary>
    /// 按鼠标位置（画布坐标）**等比**缩放部件，供编辑模式下拖动部件右下角的手柄使用：
    /// 以部件左上角为锚点（X / Y 不变），取宽、高两维中较大的比例，使缩放后的部件至少覆盖到鼠标位置；
    /// 再钳到画布右下边界内（最终仍受 <see cref="NodeViewModel.MinScale"/> / <see cref="NodeViewModel.MaxScale"/> 约束）。
    /// </summary>
    public void ResizeNodeTo(NodeViewModel node, double canvasX, double canvasY)
    {
        // 两维各自需要的比例，取较大者（跟随鼠标较远的一维，手感与"拖角"一致）
        double byWidth = ScaleFor(node.BaseWidth, canvasX - node.X);
        double byHeight = ScaleFor(node.BaseHeight, canvasY - node.Y);

        node.Scale = Math.Min(Math.Max(byWidth, byHeight), MaxScaleFittingCanvas(node));
    }

    private static double ScaleFor(double baseSize, double size)
        => baseSize > 0 ? size / baseSize : 1;

    /// <summary>部件左上角固定时，不越出画布右下边界所能达到的最大缩放系数（至少保留最小缩放）。</summary>
    private double MaxScaleFittingCanvas(NodeViewModel node)
    {
        double byWidth = node.BaseWidth > 0 ? (CanvasWidth - node.X) / node.BaseWidth : NodeViewModel.MaxScale;
        double byHeight = node.BaseHeight > 0 ? (CanvasHeight - node.Y) / node.BaseHeight : NodeViewModel.MaxScale;
        return Math.Max(NodeViewModel.MinScale, Math.Min(byWidth, byHeight));
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
                LabelFontSize = n.LabelFontSize,
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
                Routing = c.Routing,
                DetourX = c.DetourX,
                DetourY = c.DetourY,
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
                // 旧存档无字号字段时为 0，回退默认值（属性自身还会再钳一次上下限）
                if (nd.LabelFontSize > 0) node.LabelFontSize = nd.LabelFontSize;
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
            var conn = new ConnectionViewModel(this, cd.Id)
            {
                Direction = cd.Direction,
                PipeKind = pipeKind,
                Routing = cd.Routing,
                DetourX = cd.DetourX,
                DetourY = cd.DetourY
            };
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
