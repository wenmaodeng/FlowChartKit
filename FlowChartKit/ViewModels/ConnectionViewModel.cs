using System.Windows;
using System.Windows.Input;
using FlowChartKit.Core;
using FlowChartKit.Routing;

namespace FlowChartKit.ViewModels;

/// <summary>管路连线。两端可以接到部件端口，也可以是自由点（可再拖动吸附到端口）。</summary>
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

    public ConnectionViewModel(DiagramViewModel diagram, string? id = null)
    {
        _diagram = diagram;
        Id = id ?? Guid.NewGuid().ToString("N");

        SetDirectionCommand = new RelayCommand(p =>
        {
            Direction = p switch
            {
                FlowMode f => f,
                string s => Enum.TryParse<FlowMode>(s, true, out var fm) ? fm : Direction,
                _ => Direction
            };
        });

        DeleteCommand = new RelayCommand(_ => _diagram.RemoveConnection(this));
    }

    public string Id { get; }

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

    public DiagramViewModel Diagram => _diagram;

    public FlowMode Direction { get => _direction; set { if (Set(ref _direction, value)) Recompute(); } }

    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

    /// <summary>正交布线后的折线顶点（画布绝对坐标）。</summary>
    public Point[] Points { get => _points; private set => Set(ref _points, value); }

    public ICommand SetDirectionCommand { get; }
    public ICommand DeleteCommand { get; }

    /// <summary>外部（如部件改名）通知 <see cref="DisplayName"/> 需要重新求值。</summary>
    internal void RaiseDisplayName() => Raise(nameof(DisplayName));

    public void SetEnd(bool isStart, NodeViewModel node, PortViewModel port)
    {
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
        Raise(nameof(DisplayName));
        Recompute();
    }

    public void SetFreeEnd(bool isStart, Point p)
    {
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
        Raise(nameof(DisplayName));
        Recompute();
    }

    // ---- 端点拖拽（由编辑器控件驱动） ----

    internal void BeginEndpointDrag(bool isStart, Point mousePos)
    {
        _dragActive = true;
        _dragIsStart = isStart;
        _dragPoint = mousePos;
        _snapTarget = null;
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

        if (snap != null)
            SetEnd(isStart, snap.Owner, snap);
        else
            SetFreeEnd(isStart, _dragPoint);
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
