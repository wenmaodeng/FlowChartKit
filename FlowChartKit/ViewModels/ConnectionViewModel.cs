using System.Windows;
using System.Windows.Input;
using FlowChartKit.Core;
using FlowChartKit.Routing;

namespace FlowChartKit.ViewModels;

/// <summary>
/// 管路连线。两端可以接到部件端口，也可以是自由点（可再拖动吸附到端口）。
/// 反向持有所属图模型仅为内部记账（索引维护、删除命令），不再对外暴露；
/// 端点拖拽编排由 <see cref="DiagramViewModel"/> 的 BeginConnectionEndpointDrag/
/// UpdateConnectionEndpointDrag/EndConnectionEndpointDrag 统一驱动。
/// </summary>
public class ConnectionViewModel : ObservableBase
{
    private readonly DiagramViewModel _diagram;

    private FlowMode _direction = FlowMode.Forward;
    private bool _isSelected;

    private Point[] _points = Array.Empty<Point>();

    // 端点拖拽状态
    private bool _dragActive;
    private bool _dragIsStart;
    private Point _dragPoint;
    private PortViewModel? _snapTarget;
    // 拖拽前锚点快照：Esc 取消时回退（抬起路径的提交落点不使用）
    private NodeViewModel? _dragOrigNode;
    private PortViewModel? _dragOrigPort;
    private Point? _dragOrigFree;

    public ConnectionViewModel(DiagramViewModel diagram, string? id = null)
    {
        _diagram = diagram;
        Id = id ?? Guid.NewGuid().ToString("N");

        DeleteCommand = new RelayCommand(_ => _diagram.RemoveConnection(this));
    }

    public string Id { get; }

    /// <summary>所属图模型（程序集内部使用：管路控件据此读取编辑态/编号开关/画布尺寸）。</summary>
    internal DiagramViewModel Diagram => _diagram;

    private int _number;
    /// <summary>管路编号（按集合顺序从 1 开始，持久化）。</summary>
    public int Number
    {
        get => _number;
        internal set
        {
            if (Set(ref _number, value))
                Raise(nameof(DisplayName));
        }
    }

    /// <summary>下拉列表显示名：#编号 源部件 → 目标部件（任一端未接时显示"自由端"）。</summary>
    public string DisplayName
    {
        get
        {
            var src = SourceNode?.Label ?? "自由端";
            var tgt = TargetNode?.Label ?? "自由端";
            return $"#{_number} {src} → {tgt}";
        }
    }

    public NodeViewModel? SourceNode { get; internal set; }
    public PortViewModel? SourcePort { get; internal set; }
    public Point? FreeSource { get; internal set; }

    public NodeViewModel? TargetNode { get; internal set; }
    public PortViewModel? TargetPort { get; internal set; }
    public Point? FreeTarget { get; internal set; }

    public FlowMode Direction { get => _direction; set { if (Set(ref _direction, value)) Recompute(); } }

    private PipeKind _pipeKind = PipeKind.Standard;
    /// <summary>
    /// 管路类型：决定颜色、粗细与流动画，由 <c>PipeDefinitionRegistry</c> 解析的 <c>IPipeDefinition</c> 提供；
    /// 宿主可用自定义类型常量扩展（如 <c>(PipeKind)1000</c>）。变更会即时重绘并重启流动画。
    /// </summary>
    public PipeKind PipeKind
    {
        get => _pipeKind;
        // Set 已发出属性通知：管路控件据此重建画笔并重启动画（类型决定颜色/粗细/是否流动）
        set => Set(ref _pipeKind, value);
    }

    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

    /// <summary>正交布线后的折线顶点（画布绝对坐标）。</summary>
    public Point[] Points { get => _points; private set => Set(ref _points, value); }

    /// <summary>外部（如部件改名）通知 <see cref="DisplayName"/> 需要重新求值。</summary>
    internal void RaiseDisplayName() => Raise(nameof(DisplayName));

    public ICommand DeleteCommand { get; }

    public void SetEnd(bool isStart, NodeViewModel node, PortViewModel port)
    {
        var oldSource = SourceNode;
        var oldTarget = TargetNode;
        if (isStart)
        {
            SourceNode = node;
            SourcePort = port;
            FreeSource = null;
        }
        else
        {
            TargetNode = node;
            TargetPort = port;
            FreeTarget = null;
        }
        _diagram.IndexConnection(this, oldSource, oldTarget);
        Raise(nameof(DisplayName));
        Recompute();
    }

    public void SetFreeEnd(bool isStart, Point p)
    {
        var oldSource = SourceNode;
        var oldTarget = TargetNode;
        if (isStart)
        {
            SourceNode = null;
            SourcePort = null;
            FreeSource = p;
        }
        else
        {
            TargetNode = null;
            TargetPort = null;
            FreeTarget = p;
        }
        _diagram.IndexConnection(this, oldSource, oldTarget);
        Raise(nameof(DisplayName));
        Recompute();
    }

    // ---- 端点拖拽状态操作（编排入口见 DiagramViewModel 的端点拖拽编排方法） ----

    internal void BeginEndpointDrag(bool isStart, Point mousePos)
    {
        _dragActive = true;
        _dragIsStart = isStart;
        _dragPoint = mousePos;
        _snapTarget = null;

        // 快照拖拽前锚点，供 Esc 取消回退
        if (isStart)
        {
            _dragOrigNode = SourceNode;
            _dragOrigPort = SourcePort;
            _dragOrigFree = FreeSource;
        }
        else
        {
            _dragOrigNode = TargetNode;
            _dragOrigPort = TargetPort;
            _dragOrigFree = FreeTarget;
        }

        Recompute();
    }

    internal void UpdateDragPoint(Point p, PortViewModel? snap)
    {
        _snapTarget = snap;
        // 自动对齐：靠近端口时端点直接落到端口位置
        _dragPoint = snap?.AbsolutePoint ?? p;
        Recompute();
    }

    internal void EndEndpointDrag()
    {
        var snap = _snapTarget;
        bool isStart = _dragIsStart;
        _dragActive = false;
        _dragIsStart = false;
        _snapTarget = null;
        ClearDragOrigins();

        if (snap != null)
            SetEnd(isStart, snap.Owner, snap);
        else
            SetFreeEnd(isStart, _dragPoint);
    }

    /// <summary>取消端点拖拽（Esc）：退出拖拽态并把该端回退到拖拽前锚点（区别于抬起路径的提交落点）。</summary>
    internal void CancelEndpointDrag()
    {
        if (!_dragActive) return;
        bool isStart = _dragIsStart;
        var node = _dragOrigNode;
        var port = _dragOrigPort;
        var free = _dragOrigFree;
        _dragActive = false;
        _dragIsStart = false;
        _snapTarget = null;
        ClearDragOrigins();

        if (port != null)
            SetEnd(isStart, node!, port);
        else if (free != null)
            SetFreeEnd(isStart, free.Value);
        else
            Recompute(); // 拖拽前该端无锚点（不应发生）：仅退出拖拽态并按现状重算
    }

    private void ClearDragOrigins()
    {
        _dragOrigNode = null;
        _dragOrigPort = null;
        _dragOrigFree = null;
    }

    // ---- 锚点解析与几何计算 ----

    private (Point P, PortDir Dir) ResolveAnchor(bool isStart)
    {
        if (_dragActive && _dragIsStart == isStart)
        {
            var other = ResolveStaticPoint(!isStart);
            return (_dragPoint, OrthogonalRouter.AutoDir(_dragPoint, other));
        }

        if (HasAnchor(isStart))
        {
            if (isStart)
            {
                if (SourcePort != null) return (SourcePort.AbsolutePoint, SourcePort.Dir);
                if (FreeSource != null)
                    return (FreeSource.Value, OrthogonalRouter.AutoDir(FreeSource.Value, ResolveStaticPoint(false)));
            }
            else
            {
                if (TargetPort != null) return (TargetPort.AbsolutePoint, TargetPort.Dir);
                if (FreeTarget != null)
                    return (FreeTarget.Value, OrthogonalRouter.AutoDir(FreeTarget.Value, ResolveStaticPoint(true)));
            }
        }

        // 未接线端点兜底
        var o = ResolveStaticPoint(!isStart);
        return (default, OrthogonalRouter.AutoDir(default, o));
    }

    private bool HasAnchor(bool isStart) => isStart
        ? SourcePort != null || FreeSource != null
        : TargetPort != null || FreeTarget != null;

    private Point ResolveStaticPoint(bool isStart)
    {
        if (isStart)
        {
            if (SourcePort != null) return SourcePort.AbsolutePoint;
            if (FreeSource != null) return FreeSource.Value;
        }
        else
        {
            if (TargetPort != null) return TargetPort.AbsolutePoint;
            if (FreeTarget != null) return FreeTarget.Value;
        }
        return default;
    }

    public void Recompute()
    {
        var (sp, sDir) = ResolveAnchor(true);
        var (ep, eDir) = ResolveAnchor(false);

        var pts = OrthogonalRouter.Route(sp, sDir, ep, eDir);
        Points = pts.Count >= 2 ? pts.ToArray() : Array.Empty<Point>();
    }
}
