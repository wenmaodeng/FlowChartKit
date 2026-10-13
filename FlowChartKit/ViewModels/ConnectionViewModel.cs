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
    private Point _arrowTip;
    private Point _arrowBase1;
    private Point _arrowBase2;
    private bool _hasArrow;

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

    /// <summary>下拉列表显示名：源部件 → 目标部件（任一端未接时显示"自由端"）。</summary>
    public string DisplayName
    {
        get
        {
            var src = SourceNode?.Label ?? "自由端";
            var tgt = TargetNode?.Label ?? "自由端";
            return $"{src} → {tgt}";
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

    public Point ArrowTip { get => _arrowTip; private set => Set(ref _arrowTip, value); }
    public Point ArrowBase1 { get => _arrowBase1; private set => Set(ref _arrowBase1, value); }
    public Point ArrowBase2 { get => _arrowBase2; private set => Set(ref _arrowBase2, value); }
    public bool HasArrow { get => _hasArrow; private set => Set(ref _hasArrow, value); }

    public ICommand SetDirectionCommand { get; }
    public ICommand DeleteCommand { get; }

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

        ComputeArrow(pts);
    }

    private void ComputeArrow(List<Point> pts)
    {
        if (pts.Count < 2 || Direction == FlowMode.None)
        {
            HasArrow = false;
            return;
        }

        Point tip, prev;
        if (Direction == FlowMode.Forward)
        {
            tip = pts[^1];
            prev = pts[^2];
        }
        else
        {
            tip = pts[0];
            prev = pts[1];
        }

        var v = tip - prev;
        if (v.LengthSquared < 0.01)
        {
            HasArrow = false;
            return;
        }
        v.Normalize();
        var back = tip - v * 10;
        var n = new Vector(-v.Y, v.X);

        ArrowTip = tip;
        ArrowBase1 = back + n * 4.5;
        ArrowBase2 = back - n * 4.5;
        HasArrow = true;
    }
}
