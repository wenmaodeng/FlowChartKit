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
    private Point _curveC1;
    private Point _curveC2;

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

    private PipeRouting _routing = PipeRouting.Orthogonal;
    /// <summary>
    /// 连线布局：垂直水平（正交折线，默认）/ 直线 / 弧线。与 <see cref="PipeKind"/>（样式）正交，
    /// 变更即时重算路径（<see cref="Points"/> 随之更新，控件据此重建几何）。
    /// </summary>
    public PipeRouting Routing
    {
        get => _routing;
        set
        {
            if (Set(ref _routing, value)) Recompute();
        }
    }

    private double? _detourX;
    /// <summary>
    /// 正交布局下中间竖直段的 X 坐标（拖到该段后由交互写入；null 表示按端口方向自动取位）。
    /// 直线 / 弧线布局不使用该值。
    /// </summary>
    public double? DetourX
    {
        get => _detourX;
        internal set
        {
            if (Set(ref _detourX, value)) Recompute();
        }
    }

    private double? _detourY;
    /// <summary>
    /// 正交布局下中间水平段的 Y 坐标（拖到该段后由交互写入；null 表示按端口方向自动取位）。
    /// </summary>
    public double? DetourY
    {
        get => _detourY;
        internal set
        {
            if (Set(ref _detourY, value)) Recompute();
        }
    }

    /// <summary>弧线布局的控制点 1（起点侧，沿端口出线方向）。</summary>
    public Point CurveControl1 { get => _curveC1; private set => Set(ref _curveC1, value); }

    /// <summary>弧线布局的控制点 2（终点侧，沿端口出线方向）。</summary>
    public Point CurveControl2 { get => _curveC2; private set => Set(ref _curveC2, value); }

    /// <summary>布线后的折线顶点（画布绝对坐标）。直线 / 弧线布局为首尾两点（弧线的曲率见控制点属性）。</summary>
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

        switch (_routing)
        {
            case PipeRouting.Straight:
                Points = new[] { sp, ep };
                break;

            case PipeRouting.Curved:
                UpdateCurveControls(sp, sDir, ep, eDir);
                // 端点仍记为首尾两点（端点手柄、吸附判定、命中兜底都用它），曲率由控制点表达
                Points = new[] { sp, ep };
                break;

            default:
                var pts = OrthogonalRouter.Route(sp, sDir, ep, eDir, _detourX, _detourY);
                Points = pts.Count >= 2 ? pts.ToArray() : Array.Empty<Point>();
                break;
        }
    }

    /// <summary>
    /// 弧线的两个控制点：沿两端端口出线方向外推（距离取两端连线长度的一半，夹取到 [24, 240]），
    /// 使曲线在端口处的切线方向与端口朝向一致（与正交布线的 stub 语义一致）。
    /// </summary>
    private void UpdateCurveControls(Point sp, PortDir sDir, Point ep, PortDir eDir)
    {
        double d = Math.Clamp((ep - sp).Length * 0.5, 24, 240);
        CurveControl1 = sp + sDir.Vec() * d;
        CurveControl2 = ep + eDir.Vec() * d;
    }

    // ---- 正交布局：拖动中间线段（水平段上下移动 / 竖直段左右移动） ----

    // 拖段前快照：Esc 取消时回退
    private double? _dragOrigDetourX;
    private double? _dragOrigDetourY;

    /// <summary>开始拖动中间线段：记录拖动前的绕行位置快照。</summary>
    internal void BeginSegmentDrag()
    {
        _dragOrigDetourX = _detourX;
        _dragOrigDetourY = _detourY;
    }

    /// <summary>
    /// 设置正交布局的绕行位置：<paramref name="x"/> 为中间竖直段的 X，<paramref name="y"/> 为中间水平段的 Y；
    /// 传 null 表示该项恢复"按端口方向自动取位"（如复位用户拖过的绕行路径）。
    /// 直线 / 弧线布局不使用这两个值。同时变更时只重算一次路径。
    /// </summary>
    public void SetDetour(double? x, double? y)
    {
        bool changed = Set(ref _detourX, x);
        changed |= Set(ref _detourY, y);
        if (changed) Recompute();
    }

    /// <summary>
    /// 拖动中间线段到指定坐标：<paramref name="horizontal"/> 为 true 表示水平段（按其 Y 移动），
    /// 否则为竖直段（按其 X 移动）。坐标钳位到画布范围内；仅正交布局生效。
    /// </summary>
    internal void UpdateSegmentDrag(bool horizontal, double pos)
    {
        if (_routing != PipeRouting.Orthogonal) return;

        // 钳到画布内：段不能被拖出画布边界
        double max = horizontal ? _diagram.CanvasHeight : _diagram.CanvasWidth;
        double p = Math.Clamp(pos, 0, Math.Max(0, max));

        if (horizontal)
        {
            if (_detourY.HasValue && Math.Abs(_detourY.Value - p) < 0.01) return;
            DetourY = p;
        }
        else
        {
            if (_detourX.HasValue && Math.Abs(_detourX.Value - p) < 0.01) return;
            DetourX = p;
        }
    }

    /// <summary>提交段拖拽（保留当前绕行位置）。</summary>
    internal void EndSegmentDrag()
    {
        _dragOrigDetourX = null;
        _dragOrigDetourY = null;
    }

    /// <summary>取消段拖拽（Esc）：绕行位置回退到拖动前。</summary>
    internal void CancelSegmentDrag()
    {
        if (_routing == PipeRouting.Orthogonal)
            SetDetour(_dragOrigDetourX, _dragOrigDetourY);
        EndSegmentDrag();
    }
}
