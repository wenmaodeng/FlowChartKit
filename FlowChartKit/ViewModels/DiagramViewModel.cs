using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Data;
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
            else if (SelectedTextBlock is { } t) RemoveTextItem(t);
        }, _ => SelectedConnection != null || SelectedNode != null || SelectedTextBlock != null);

        // 统一画布项：默认层级（管路 → 文本 → 部件）由容器声明顺序决定；Z 相同时以此顺序为准。
        // 三个集合实例不变（增删发生在集合内部），故只装配一次，变更由 CompositeCollection 自动冒泡。
        CanvasItems = new CompositeCollection
        {
            new CollectionContainer { Collection = Connections },
            new CollectionContainer { Collection = Texts },
            new CollectionContainer { Collection = Nodes },
        };
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

    /// <summary>文本模块（与部件 / 管路平级的第三种画布元素）。</summary>
    public ObservableCollection<TextBlockViewModel> Texts { get; } = [];

    /// <summary>
    /// 统一画布项集合（管路 → 文本 → 部件 的默认层级顺序）：画布控件以**单一 ItemsControl** 绑定它，
    /// 元素的前后关系由各自的 <c>Z</c> 决定（<c>Panel.ZIndex</c>），因此**管路 / 文本 / 部件可自由叠放**。
    /// 三个子集合实例不变（增删发生在集合内部），故只装配一次；变更由 <see cref="CompositeCollection"/> 自动冒泡。
    /// </summary>
    public CompositeCollection CanvasItems { get; }

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
                ClearOtherSelections(keepNode: _selectedNode, keepConnection: null, keepText: null);
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
                ClearOtherSelections(keepNode: null, keepConnection: _selectedConnection, keepText: null);
            }
            Raise();
            Raise(nameof(SelectedNode));
        }
    }

    private TextBlockViewModel? _selectedTextBlock;

    /// <summary>当前选中的文本模块；与部件 / 管路的选中互斥（同一时刻至多一个选中对象）。</summary>
    public TextBlockViewModel? SelectedTextBlock
    {
        get => _selectedTextBlock;
        set
        {
            if (_selectedTextBlock == value) return;
            if (_selectedTextBlock != null) _selectedTextBlock.IsSelected = false;
            _selectedTextBlock = value;
            if (_selectedTextBlock != null)
            {
                _selectedTextBlock.IsSelected = true;
                ClearOtherSelections(keepNode: null, keepConnection: null, keepText: _selectedTextBlock);
            }
            Raise();
        }
    }

    /// <summary>选中互斥：清掉三个选中对象中未被保留（keep*）的那些。</summary>
    private void ClearOtherSelections(NodeViewModel? keepNode, ConnectionViewModel? keepConnection, TextBlockViewModel? keepText)
    {
        if (_selectedConnection != null && _selectedConnection != keepConnection)
        {
            _selectedConnection.IsSelected = false;
            _selectedConnection = null;
            Raise(nameof(SelectedConnection));
        }
        if (_selectedNode != null && _selectedNode != keepNode)
        {
            _selectedNode.IsSelected = false;
            _selectedNode = null;
            Raise(nameof(SelectedNode));
        }
        if (_selectedTextBlock != null && _selectedTextBlock != keepText)
        {
            _selectedTextBlock.IsSelected = false;
            _selectedTextBlock = null;
            Raise(nameof(SelectedTextBlock));
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

    /// <summary>
    /// 临时关闭对齐吸附（拖动/移动过程中由控件在按住 Alt 时置位，用于精细调整）。
    /// 与 <see cref="AlignmentSnapEnabled"/> 的区别：它只作用于当前这一次拖动，
    /// 不改变宿主配置，拖动结束由控件复位；不吸附时同样不生成捕捉线。
    /// </summary>
    public bool SnapSuppressed { get; set; }

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
        if (!_alignmentSnapEnabled || _alignmentSnapDistance <= 0 || SnapSuppressed)
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

    // ---- 文本模块 ----

    /// <summary>分配下一个文本模块编号：现有最大编号 + 1（无则 1），与管路编号同一策略。</summary>
    private int NextTextNumber()
    {
        int max = 0;
        foreach (var t in Texts)
            if (t.Number > max) max = t.Number;
        return max + 1;
    }

    /// <summary>添加一个文本模块：编号自动分配，坐标钳到画布内。</summary>
    public TextBlockViewModel AddTextItem(double x, double y,
        string? content = null, double fontSize = TextBlockViewModel.DefaultFontSize, string? id = null)
    {
        var text = new TextBlockViewModel(content, x, y, fontSize, id)
        {
            X = Math.Clamp(x, 0, Math.Max(0, CanvasWidth)),
            Y = Math.Clamp(y, 0, Math.Max(0, CanvasHeight)),
            Diagram = this
        };
        text.Number = NextTextNumber();
        Texts.Add(text);
        return text;
    }

    /// <summary>移除文本模块。</summary>
    public void RemoveTextItem(TextBlockViewModel text)
    {
        if (!Texts.Contains(text)) return;
        if (SelectedTextBlock == text) SelectedTextBlock = null;
        text.Diagram = null;   // 解除反向引用：被移除的文本不得再操作本图
        Texts.Remove(text);
    }

    /// <summary>按编号查找文本模块；编号与添加顺序一致，从 1 开始。</summary>
    public TextBlockViewModel? FindTextBlock(int number)
        => Texts.FirstOrDefault(t => t.Number == number);

    /// <summary>
    /// 按编号修改文本内容。由画布控件的 SetTextByNumberCommand 命令桥调用
    /// （参照管路的 <see cref="SetDirectionCommand"/>），也可直接调用。
    /// </summary>
    /// <returns>找到并修改了该编号的文本模块时为 true；编号不存在时为 false。</returns>
    public bool SetTextContent(int number, string? content)
    {
        var text = FindTextBlock(number);
        if (text is null) return false;
        text.Content = content ?? "";
        return true;
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
    // 当前拖动会话：记住"正在拖动的是哪一段"（Points 索引），供自身段落对齐时排除
    private ConnectionViewModel? _segmentDragConn;
    private int _segmentDragIndex = -1;

    public void BeginConnectionSegmentDrag(ConnectionViewModel conn)
    {
        ClearAlignmentGuides();
        // 被拖段在第一次 Update 时确定（此时鼠标正压在该段上）
        _segmentDragConn = conn;
        _segmentDragIndex = -1;
        conn.BeginSegmentDrag();
    }

    /// <summary>
    /// 拖动中间线段到指定坐标：<paramref name="horizontal"/> 为 true 表示水平段（按 Y 移动），
    /// 否则为竖直段（按 X 移动）。坐标先经对齐吸附（见 <see cref="SnapConnectionSegment"/>）再下发，
    /// 并同步刷新捕捉线；仅正交布局生效。
    /// </summary>
    public void UpdateConnectionSegmentDrag(ConnectionViewModel conn, bool horizontal, double pos)
    {
        // 拖动开始的第一次更新：鼠标就压在被拖段上，此刻的"最近同走向段"即被拖段
        if (ReferenceEquals(_segmentDragConn, conn) && _segmentDragIndex < 0)
            _segmentDragIndex = DraggedSegmentIndex(conn, horizontal, pos);

        var snap = SnapConnectionSegment(conn, horizontal, pos);
        SetAlignmentGuides(snap.Guides);
        conn.UpdateSegmentDrag(horizontal, snap.Pos);
    }

    /// <summary>结束段拖拽：提交当前绕行位置并清除捕捉线。</summary>
    public void EndConnectionSegmentDrag(ConnectionViewModel conn)
    {
        ClearAlignmentGuides();
        ResetSegmentDragSession();
        conn.EndSegmentDrag();
    }

    /// <summary>取消段拖拽（Esc）：绕行位置回退到拖动前并清除捕捉线。</summary>
    public void CancelConnectionSegmentDrag(ConnectionViewModel conn)
    {
        ClearAlignmentGuides();
        ResetSegmentDragSession();
        conn.CancelSegmentDrag();
    }

    /// <summary>结束拖动会话：清空"被拖段"记录。</summary>
    private void ResetSegmentDragSession()
    {
        _segmentDragConn = null;
        _segmentDragIndex = -1;
    }

    /// <summary>
    /// 拖动正交管路线段时的**对齐吸附**：与**其它管路**的水平 / 竖直段共线对齐（不考虑部件）——
    /// <paramref name="horizontal"/> 为 true 表示拖水平段，按 Y 吸附到其它管路的水平段；
    /// 否则为拖竖直段，按 X 吸附到其它管路的竖直段。
    /// **被拖管路自身的段也参与对齐**（与自己的起始段/结束段齐平，可把 Z 形拉成 L 形），
    /// 但正在拖动的那一段除外——它跟随鼠标移动，参与的话会自吸附成零偏差。
    /// 取最近的一个候选（阈值内）并生成对应捕捉线；未启用或无候选时原样返回。
    /// 开关与阈值沿用部件吸附的 <see cref="AlignmentSnapEnabled"/> / <see cref="AlignmentSnapDistance"/>，
    /// 并受 <see cref="SnapSuppressed"/>（按住 Alt 临时关闭）影响。
    /// </summary>
    public SegmentSnapResult SnapConnectionSegment(ConnectionViewModel conn, bool horizontal, double pos)
    {
        if (!_alignmentSnapEnabled || _alignmentSnapDistance <= 0 || SnapSuppressed)
            return new SegmentSnapResult(pos, Array.Empty<AlignmentGuide>());

        double best = _alignmentSnapDistance;
        double snapped = pos;
        double guideMin = 0, guideMax = 0;
        bool found = false;

        // 自身"正在拖动的那一段"的索引（0 表示没有同走向的段，全部参与）
        int selfSkip = SelfSkipSegmentIndex(conn, horizontal, pos);

        foreach (var other in Connections)
        {
            bool self = ReferenceEquals(other, conn);
            var pts = other.Points;
            for (int i = 1; i < pts.Length; i++)
            {
                // 被拖段始终与鼠标同位置：跳过，否则零偏差会把其它候选全部挡掉
                if (self && i == selfSkip) continue;

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
    /// 取应被排除的自身段索引：优先用拖动会话中记录的被拖段（Points 重排后失效则回退"最近同走向段"）。
    /// </summary>
    private int SelfSkipSegmentIndex(ConnectionViewModel conn, bool horizontal, double pos)
    {
        if (ReferenceEquals(_segmentDragConn, conn) && _segmentDragIndex > 0)
        {
            var pts = conn.Points;
            int i = _segmentDragIndex;
            // 路径重算可能合并折点：索引仍指向同走向的段时才可信
            if (i < pts.Length && (Math.Abs(pts[i - 1].Y - pts[i].Y) < 0.01) == horizontal)
                return i;
        }
        return DraggedSegmentIndex(conn, horizontal, pos);
    }

    /// <summary>
    /// 被拖管路自身"正在拖动的那一段"的索引（1 表示 Points[0]→Points[1]；0 表示没有同走向的段）。
    /// 判定：同走向的段中离拖动坐标最近的那个——该段跟随鼠标移动，必然是最近的。
    /// </summary>
    private static int DraggedSegmentIndex(ConnectionViewModel conn, bool horizontal, double pos)
    {
        var pts = conn.Points;
        int index = 0;
        double best = double.MaxValue;
        for (int i = 1; i < pts.Length; i++)
        {
            var a = pts[i - 1];
            var b = pts[i];
            if ((Math.Abs(a.Y - b.Y) < 0.01) != horizontal) continue;

            double delta = Math.Abs((horizontal ? a.Y : a.X) - pos);
            if (delta >= best) continue;
            best = delta;
            index = i;
        }
        return index;
    }

    /// <summary>
    /// 部件到画布四边的剩余距离（px，贴边为 0）：左 / 上 / 右 / 下。
    /// 拖动部件时用它实时显示边距读数（见 FlowNodeControl 的拖动提示）。
    /// </summary>
    public (double Left, double Top, double Right, double Bottom) GetNodeMargins(NodeViewModel node)
        => (Math.Max(0, node.X),
            Math.Max(0, node.Y),
            Math.Max(0, CanvasWidth - (node.X + node.ShapeWidth)),
            Math.Max(0, CanvasHeight - (node.Y + node.ShapeHeight)));

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

    /// <summary>
    /// 只按鼠标位置（画布坐标）**改宽度**（<see cref="NodeViewModel.ScaleX"/>），供拖动部件**右侧手柄**使用：
    /// 以部件左边为锚（X 不变、高度不变），再钳到画布右边界内（仍受 MinScale / MaxScale 约束）。
    /// </summary>
    public void ResizeNodeWidthTo(NodeViewModel node, double canvasX)
        => node.ScaleX = Math.Min(ScaleFor(node.BaseWidth, canvasX - node.X), MaxScaleFittingCanvasX(node));

    /// <summary>
    /// 只按鼠标位置（画布坐标）**改高度**（<see cref="NodeViewModel.ScaleY"/>），供拖动部件**下方手柄**使用：
    /// 以部件上边为锚（Y 不变、宽度不变），再钳到画布下边界内（仍受 MinScale / MaxScale 约束）。
    /// </summary>
    public void ResizeNodeHeightTo(NodeViewModel node, double canvasY)
        => node.ScaleY = Math.Min(ScaleFor(node.BaseHeight, canvasY - node.Y), MaxScaleFittingCanvasY(node));

    private static double ScaleFor(double baseSize, double size)
        => baseSize > 0 ? size / baseSize : 1;

    /// <summary>部件左上角固定时，不越出画布右下边界所能达到的最大缩放系数（至少保留最小缩放）。</summary>
    private double MaxScaleFittingCanvas(NodeViewModel node)
    {
        double byWidth = node.BaseWidth > 0 ? (CanvasWidth - node.X) / node.BaseWidth : NodeViewModel.MaxScale;
        double byHeight = node.BaseHeight > 0 ? (CanvasHeight - node.Y) / node.BaseHeight : NodeViewModel.MaxScale;
        return Math.Max(NodeViewModel.MinScale, Math.Min(byWidth, byHeight));
    }

    /// <summary>只改宽度时，不越出画布右边界所能达到的最大横向缩放系数。</summary>
    private double MaxScaleFittingCanvasX(NodeViewModel node)
        => node.BaseWidth > 0
            ? Math.Max(NodeViewModel.MinScale, (CanvasWidth - node.X) / node.BaseWidth)
            : NodeViewModel.MaxScale;

    /// <summary>只改高度时，不越出画布下边界所能达到的最大纵向缩放系数。</summary>
    private double MaxScaleFittingCanvasY(NodeViewModel node)
        => node.BaseHeight > 0
            ? Math.Max(NodeViewModel.MinScale, (CanvasHeight - node.Y) / node.BaseHeight)
            : NodeViewModel.MaxScale;

    // ---- 叠放次序（Z 序）：管路 / 文本 / 部件统一排序 ----

    /// <summary>相邻叠放层之间的 Z 间隔：留足空隙便于在两者之间插入（由 ApplyOrder 统一重排，不会耗尽）。</summary>
    private const int ZStep = 100;

    /// <summary>读取画布元素（部件 / 文本 / 管路）的叠放次序；非画布元素返回 0。</summary>
    public static int GetZ(object? item) => item switch
    {
        NodeViewModel n => n.Z,
        TextBlockViewModel t => t.Z,
        ConnectionViewModel c => c.Z,
        _ => 0
    };

    private static void SetZ(object item, int z)
    {
        switch (item)
        {
            case NodeViewModel n: n.Z = z; break;
            case TextBlockViewModel t: t.Z = z; break;
            case ConnectionViewModel c: c.Z = z; break;
        }
    }

    private static bool IsCanvasItem(object? item)
        => item is NodeViewModel or TextBlockViewModel or ConnectionViewModel;

    /// <summary>当前叠放顺序（**从底到顶**）：按 Z 升序，同 Z 时按默认层级（管路 → 文本 → 部件）。</summary>
    private List<object> OrderedItems()
    {
        var items = new List<object>(Connections.Count + Texts.Count + Nodes.Count);
        items.AddRange(Connections);
        items.AddRange(Texts);
        items.AddRange(Nodes);
        // OrderBy 是稳定排序：同 Z 保持上面的基线顺序
        return items.OrderBy(GetZ).ToList();
    }

    /// <summary>按给定顺序（从底到顶）统一重写 Z：值始终规整为 100、200…，避免出现相同 / 耗尽间隔。</summary>
    private static void ApplyOrder(List<object> ordered)
    {
        int z = 0;
        foreach (var item in ordered)
            SetZ(item, z += ZStep);
    }

    /// <summary>置于顶层。</summary>
    public void BringToFront(object? item)
    {
        if (!IsCanvasItem(item)) return;
        var ordered = OrderedItems();
        ordered.Remove(item!);
        ordered.Add(item!);
        ApplyOrder(ordered);
    }

    /// <summary>置于底层。</summary>
    public void SendToBack(object? item)
    {
        if (!IsCanvasItem(item)) return;
        var ordered = OrderedItems();
        ordered.Remove(item!);
        ordered.Insert(0, item!);
        ApplyOrder(ordered);
    }

    /// <summary>上移一层（与紧邻其上的元素交换）。</summary>
    public void BringForward(object? item)
    {
        if (!IsCanvasItem(item)) return;
        var ordered = OrderedItems();
        int i = ordered.IndexOf(item!);
        if (i < 0 || i >= ordered.Count - 1) return;
        (ordered[i], ordered[i + 1]) = (ordered[i + 1], ordered[i]);
        ApplyOrder(ordered);
    }

    /// <summary>下移一层（与紧邻其下的元素交换）。</summary>
    public void SendBackward(object? item)
    {
        if (!IsCanvasItem(item)) return;
        var ordered = OrderedItems();
        int i = ordered.IndexOf(item!);
        if (i <= 0) return;
        (ordered[i], ordered[i - 1]) = (ordered[i - 1], ordered[i]);
        ApplyOrder(ordered);
    }

    /// <summary>
    /// 置于 <paramref name="target"/> 之上（紧邻）。两者可以是任意画布元素（部件 / 文本 / 管路）。
    /// </summary>
    /// <returns>两者均为本图的画布元素且成功插入时为 true。</returns>
    public bool MoveAbove(object? item, object? target)
    {
        if (!IsCanvasItem(item) || !IsCanvasItem(target) || ReferenceEquals(item, target)) return false;
        var ordered = OrderedItems();
        if (!ordered.Remove(item!)) return false;
        int idx = ordered.IndexOf(target!);
        if (idx < 0) return false;
        ordered.Insert(idx + 1, item!);
        ApplyOrder(ordered);
        return true;
    }

    /// <summary>置于 <paramref name="target"/> 之下（紧邻）。</summary>
    public bool MoveBelow(object? item, object? target)
    {
        if (!IsCanvasItem(item) || !IsCanvasItem(target) || ReferenceEquals(item, target)) return false;
        var ordered = OrderedItems();
        if (!ordered.Remove(item!)) return false;
        int idx = ordered.IndexOf(target!);
        if (idx < 0) return false;
        ordered.Insert(idx, item!);
        ApplyOrder(ordered);
        return true;
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
                // 等比存档字段保留（旧版库回读时取它）；两方向一致时即该值
                Scale = n.ScaleX,
                ScaleX = n.ScaleX,
                ScaleY = n.ScaleY,
                LabelFontSize = n.LabelFontSize,
                Number = n.KindNumber,
                Z = n.Z
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
                Z = c.Z,
                SourceNodeId = c.SourceNode?.Id,
                SourcePortId = c.SourcePort?.Id,
                TargetNodeId = c.TargetNode?.Id,
                TargetPortId = c.TargetPort?.Id
            };
            if (c.FreeSource is { } fs) { cd.SourceFreeX = fs.X; cd.SourceFreeY = fs.Y; }
            if (c.FreeTarget is { } ft) { cd.TargetFreeX = ft.X; cd.TargetFreeY = ft.Y; }
            data.Connections.Add(cd);
        }

        foreach (var t in Texts)
        {
            data.Texts.Add(new TextBlockData
            {
                Id = t.Id,
                Number = t.Number,
                X = t.X,
                Y = t.Y,
                Content = t.Content,
                FontSize = t.FontSize,
                Z = t.Z
            });
        }
        return data;
    }

    public void Clear()
    {
        SelectedConnection = null;
        SelectedNode = null;
        SelectedTextBlock = null;
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
        foreach (var t in Texts)
            t.Diagram = null;
        Texts.Clear();
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
                // 分方向缩放：旧存档只有 Scale（等比），缺失的方向回退到它
                node.SetScale(nd.ScaleX > 0 ? nd.ScaleX : (nd.Scale > 0 ? nd.Scale : 1),
                              nd.ScaleY > 0 ? nd.ScaleY : (nd.Scale > 0 ? nd.Scale : 1));
                // 旧存档无字号字段时为 0，回退默认值（属性自身还会再钳一次上下限）
                if (nd.LabelFontSize > 0) node.LabelFontSize = nd.LabelFontSize;
                node.Z = nd.Z;   // 旧存档无 Z 字段时为 0（按默认层级排列）
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
                DetourY = cd.DetourY,
                Z = cd.Z
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

        // 文本模块：编号>0 的以存档为准（0 表示未编号，保留 AddTextItem 的分配值）
        foreach (var td in data.Texts)
        {
            var text = AddTextItem(td.X, td.Y, td.Content,
                td.FontSize > 0 ? td.FontSize : TextBlockViewModel.DefaultFontSize, td.Id);
            text.Z = td.Z;
            if (td.Number > 0)
                text.Number = td.Number;
        }
    }
}
