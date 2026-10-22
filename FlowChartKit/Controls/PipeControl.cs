using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using FlowChartKit;
using FlowChartKit.Rendering;
using FlowChartKit.ViewModels;

namespace FlowChartKit.Controls;

/// <summary>
/// 管路控件（继承 Control）：不使用 ControlTemplate，通过 OnRender 自绘。
/// DataContext 应为 ConnectionViewModel；控件覆盖整个画布（位于 0,0），
/// 通过 HitTestCore 仅在靠近折线时接收点击。
/// 支持端点拖拽吸附、流向动画、选中态。
/// 渲染资源全部缓存：Pen 预创建、StreamGeometry 仅在折线变化时重建、编号文本按编号/选中态缓存。
/// </summary>
public class PipeControl : Control
{
    // 端点手柄色为库内固定的交互元素配色，不随管路类型变化
    private static readonly Brush HandleStroke = CreateBrush(0x2F, 0x6F, 0xAF);
    private static readonly Pen HandlePen = MakePen(HandleStroke, 1.5, dash: null, freeze: true);

    // 当前管路类型对应的画笔：实线（普通/选中）冻结复用；虚线笔每控件一对，共享可变 DashStyle（DashOffset 动画驱动）
    private readonly DashStyle _dashStyle = new(new double[] { 4, 4 }, 0);
    // 由 ApplyStyle 在构造函数中填充（= null! 仅为满足可空性分析）
    private Pen _solidPen = null!;
    private Pen _solidPenSelected = null!;
    private Pen _badgePen = null!;
    private Pen _badgePenSelected = null!;
    private Pen _dashPen = null!;
    private Pen _dashPenSelected = null!;

    // 已应用样式的管路类型：类型未变则不重建画笔（宿主重新注册同类型定义时由 Resolve 取新定义）
    private PipeKind _styleKind = PipeKind.Standard;
    private IPipeDefinition _def = new StandardPipeDefinition();

    public static readonly DependencyProperty DashOffsetProperty = DependencyProperty.Register(
        nameof(DashOffset), typeof(double), typeof(PipeControl),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private ConnectionViewModel? _vm;
    private DiagramViewModel? _diagram;
    private bool _draggingEndpoint;
    private bool _dragIsStart;

    // 正交布局：拖动中间线段
    private bool _draggingSegment;
    private bool _dragSegmentHorizontal;
    private bool _segmentDragMoved;
    private Point _segmentDragStart;

    // 渲染缓存：路径几何、命中折线、徽章中点、编号文本
    private StreamGeometry? _geometry;
    private Point[]? _hitPoints;
    private Point _badgeCenter;
    private FormattedText? _badgeText;
    private int _badgeTextNumber = -1;
    private bool _badgeTextSelected;
    private double _badgeTextDpi = -1;

    static PipeControl()
    {
        FocusableProperty.OverrideMetadata(typeof(PipeControl),
            new FrameworkPropertyMetadata(true));
    }

    public PipeControl()
    {
        ApplyStyle(PipeKind.Standard);
        DataContextChanged += OnDataContextChanged;
        Background = Brushes.Transparent;
        // Forever 动画的时钟会强引用本控件：卸载时必须停止，否则控件被计时系统根住无法回收
        Loaded += (_, _) => UpdateFlowAnimation();
        Unloaded += (_, _) => BeginAnimation(DashOffsetProperty, null);
    }

    public double DashOffset
    {
        get => (double)GetValue(DashOffsetProperty);
        set => SetValue(DashOffsetProperty, value);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // 新旧 VM/图模型的订阅严格对称，避免容器复用或 DataContext 清空后仍挂着旧订阅
        if (_vm != null)
            _vm.PropertyChanged -= OnVmPropertyChanged;
        if (_diagram != null)
        {
            _diagram.PropertyChanged -= OnDiagramPropertyChanged;
            _diagram = null;
        }

        _vm = e.NewValue as ConnectionViewModel;

        if (_vm != null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            EnsureDiagramHook();
            // force：宿主可能在两次复用之间重新注册过该类型的定义
            ApplyStyle(_vm.PipeKind, force: true);
            RebuildGeometry();
            UpdateFlowAnimation();
        }
        else
        {
            _draggingSegment = false;
            _geometry = null;
            _hitPoints = null;
            _badgeText = null;
            // VM 脱离时停止 Forever 动画，避免空转并根住控件
            BeginAnimation(DashOffsetProperty, null);
        }
        InvalidateVisual();
    }

    /// <summary>
    /// 挂上图模型的属性通知（编号开关/画布尺寸）并同步占位尺寸。
    /// 图模型取自 <see cref="ConnectionViewModel.Diagram"/>（构造期注入，恒可用），
    /// 与 FlowNodeControl 读 NodeViewModel.Diagram 同一模式；OnRender 中的重复调用是幂等空转。
    /// </summary>
    private void EnsureDiagramHook()
    {
        var diagram = _vm?.Diagram;
        if (diagram == _diagram) return;

        if (_diagram != null)
            _diagram.PropertyChanged -= OnDiagramPropertyChanged;

        _diagram = diagram;

        if (_diagram != null)
        {
            _diagram.PropertyChanged += OnDiagramPropertyChanged;
            Width = _diagram.CanvasWidth;
            Height = _diagram.CanvasHeight;
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm is null) return;
        switch (e.PropertyName)
        {
            case nameof(ConnectionViewModel.Points):
                RebuildGeometry();
                InvalidateVisual();
                break;
            case nameof(ConnectionViewModel.IsSelected):
                InvalidateVisual();
                break;
            case nameof(ConnectionViewModel.Direction):
                UpdateFlowAnimation();
                InvalidateVisual();
                break;
            case nameof(ConnectionViewModel.PipeKind):
                // 类型变了：颜色/粗细/流动画规格都随之改变
                ApplyStyle(_vm.PipeKind);
                UpdateFlowAnimation();
                InvalidateVisual();
                break;
            case nameof(ConnectionViewModel.Number):
                InvalidateVisual();
                break;
        }
    }

    /// <summary>按管路类型重建画笔：实线笔冻结共享；虚线笔每控件一对，共享可变的 DashStyle（DashOffset 动画驱动）。</summary>
    private void ApplyStyle(PipeKind kind, bool force = false)
    {
        if (!force && _styleKind == kind && _def is not null) return;

        var def = PipeDefinitionRegistry.Resolve(kind);
        _def = def;
        _styleKind = kind;

        _solidPen = MakePen(def.Stroke, def.Thickness, dash: null, freeze: true);
        _solidPenSelected = MakePen(def.SelectedStroke, def.SelectedThickness, dash: null, freeze: true);
        _badgePen = MakePen(def.Stroke, 1.6, dash: null, freeze: true);
        _badgePenSelected = MakePen(def.SelectedStroke, 1.6, dash: null, freeze: true);

        _dashStyle.Dashes = new DoubleCollection(def.Flow.Dash);
        _dashPen = MakePen(def.Stroke, def.Thickness, _dashStyle, freeze: false);
        _dashPenSelected = MakePen(def.SelectedStroke, def.SelectedThickness, _dashStyle, freeze: false);
    }

    private void OnDiagramPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DiagramViewModel.ShowNumbers))
        {
            InvalidateVisual();
        }
        else if (_diagram != null
            && (e.PropertyName == nameof(DiagramViewModel.CanvasWidth)
                || e.PropertyName == nameof(DiagramViewModel.CanvasHeight)))
        {
            // 画布尺寸可随载入存档变化（见 DiagramViewModel.Load），同步占位尺寸
            Width = _diagram.CanvasWidth;
            Height = _diagram.CanvasHeight;
        }
    }

    private void UpdateFlowAnimation()
    {
        if (_vm is null) return;

        // 取当前类型的流动规格：不流动的类型或流向为 None 时恒为实线（不启动动画）
        ApplyStyle(_vm.PipeKind);
        var flow = _def.Flow;
        if (!flow.Enabled || _vm.Direction == FlowMode.None)
        {
            BeginAnimation(DashOffsetProperty, null);
            return;
        }

        // 动画在"一个 dash 循环"的位移量之间往复：正向源→目标，反向反之
        double cycle = flow.Cycle;
        var duration = flow.Duration;
        DoubleAnimation animation = _vm.Direction == FlowMode.Forward
            ? new DoubleAnimation(cycle, 0, duration)
            : new DoubleAnimation(0, cycle, duration);
        animation.RepeatBehavior = RepeatBehavior.Forever;
        BeginAnimation(DashOffsetProperty, animation);
    }

    /// <summary>路径变化时重建几何与徽章中点；OnRender 不再做几何构建与长度累计。</summary>
    private void RebuildGeometry()
    {
        _geometry = null;
        _hitPoints = null;
        var vm = _vm;
        var pts = vm?.Points;
        if (vm is null || pts is null || pts.Length < 2) return;

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(pts[0], false, false);
            if (vm.Routing == PipeRouting.Curved)
            {
                // 弧线：三次贝塞尔，控制点沿两端端口出线方向（切线方向与端口朝向一致）
                ctx.BezierTo(vm.CurveControl1, vm.CurveControl2, pts[^1], true, false);
            }
            else
            {
                for (int i = 1; i < pts.Length; i++)
                    ctx.LineTo(pts[i], true, false);
            }
        }
        geo.Freeze();
        _geometry = geo;
        _badgeCenter = vm.Routing == PipeRouting.Curved
            ? BezierMidpoint(pts[0], vm.CurveControl1, vm.CurveControl2, pts[^1])
            : ComputeBadgeCenter(pts);
        // 命中用折线：弧线按贝塞尔采样成折线，命中判定与折线统一走"点到线段距离"（比
        // Geometry.StrokeContains 的加宽计算便宜得多，且两端重合的退化管路也能点中）
        _hitPoints = vm.Routing == PipeRouting.Curved
            ? SampleBezier(pts[0], vm.CurveControl1, vm.CurveControl2, pts[^1], CurveHitSamples)
            : pts;
    }

    /// <summary>命中折线的弧线采样段数：16 段时与曲线的最大偏差已远小于 1px。</summary>
    private const int CurveHitSamples = 16;

    /// <summary>把三次贝塞尔采样成折线（含首尾点，共 samples + 1 个顶点）。</summary>
    private static Point[] SampleBezier(Point p0, Point c1, Point c2, Point p3, int samples)
    {
        var arr = new Point[samples + 1];
        for (int i = 0; i <= samples; i++)
        {
            double t = (double)i / samples;
            double u = 1 - t;
            double b0 = u * u * u;
            double b1 = 3 * u * u * t;
            double b2 = 3 * u * t * t;
            double b3 = t * t * t;
            arr[i] = new Point(
                b0 * p0.X + b1 * c1.X + b2 * c2.X + b3 * p3.X,
                b0 * p0.Y + b1 * c1.Y + b2 * c2.Y + b3 * p3.Y);
        }
        return arr;
    }

    /// <summary>三次贝塞尔 t=0.5 处的点：P = (P0 + 3C1 + 3C2 + P3) / 8。</summary>
    private static Point BezierMidpoint(Point p0, Point c1, Point c2, Point p3)
        => new((p0.X + 3 * c1.X + 3 * c2.X + p3.X) / 8,
               (p0.Y + 3 * c1.Y + 3 * c2.Y + p3.Y) / 8);

    /// <summary>折线长度中点：作为编号徽章位置。</summary>
    private static Point ComputeBadgeCenter(Point[] pts)
    {
        double totalLen = 0;
        for (int i = 1; i < pts.Length; i++)
            totalLen += (pts[i] - pts[i - 1]).Length;
        double half = totalLen / 2;
        double acc = 0;
        for (int i = 1; i < pts.Length; i++)
        {
            var seg = pts[i] - pts[i - 1];
            double segLen = seg.Length;
            if (acc + segLen >= half)
            {
                double t = segLen > 0 ? (half - acc) / segLen : 0;
                return new Point(pts[i - 1].X + seg.X * t, pts[i - 1].Y + seg.Y * t);
            }
            acc += segLen;
        }
        return pts[0];
    }

    /// <summary>编号文本按 编号/选中态/DPI 缓存，避免每帧构造 FormattedText。</summary>
    private FormattedText GetBadgeText(int number, bool selected, double dpi)
    {
        if (_badgeText is null || _badgeTextNumber != number
            || _badgeTextSelected != selected || _badgeTextDpi != dpi)
        {
            _badgeText = new FormattedText(
                number.ToString(),
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Arial"),
                11.0,
                selected ? _def.SelectedStroke : _def.Stroke,
                dpi);
            _badgeTextNumber = number;
            _badgeTextSelected = selected;
            _badgeTextDpi = dpi;
        }
        return _badgeText;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var vm = _vm;
        var geo = _geometry;
        if (vm is null || geo is null) return;
        EnsureDiagramHook();
        var pts = vm.Points;

        // 画笔按管路类型构建（宿主可在运行中更换类型或重新注册定义）
        if (_styleKind != vm.PipeKind) ApplyStyle(vm.PipeKind);

        bool selected = vm.IsSelected;
        // 仅"允许流动的类型 + 有流向"时才走虚线动画；否则恒为实线
        bool dashed = _def.Flow.Enabled && vm.Direction != FlowMode.None;
        Pen pen;
        if (dashed)
        {
            _dashStyle.Offset = DashOffset;
            pen = selected ? _dashPenSelected : _dashPen;
        }
        else
        {
            pen = selected ? _solidPenSelected : _solidPen;
        }

        // 管路折线
        dc.DrawGeometry(null, pen, geo);

        // 端点手柄（仅编辑模式可见）
        if (_diagram?.IsEditable == true)
        {
            dc.DrawEllipse(Brushes.White, HandlePen, pts[0], 5, 5);
            dc.DrawEllipse(Brushes.White, HandlePen, pts[^1], 5, 5);
        }

        // 编号徽章（按 ShowNumbers 开关）—— 位于折线长度中点处（中点在重建几何时算好）
        if (_diagram?.ShowNumbers == true)
        {
            var center = _badgeCenter;
            dc.DrawEllipse(Brushes.White, selected ? _badgePenSelected : _badgePen, center, 11, 11);

            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var ft = GetBadgeText(vm.Number, selected, dpi);
            dc.DrawText(ft, new Point(center.X - ft.Width / 2, center.Y - ft.Height / 2));
        }
    }

    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
    {
        var p = hitTestParameters.HitPoint;
        var pts = _hitPoints;
        if (pts is null || pts.Length < 2) return null;

        // 折线 / 直线 / 弧线统一按"点到线段距离"命中（弧线用采样折线），容差 8 的平方，全程不开根号
        const double thresholdSquared = 8.0 * 8.0;
        for (int i = 0; i < pts.Length - 1; i++)
        {
            if (DistanceToSegmentSquared(p, pts[i], pts[i + 1]) <= thresholdSquared)
                return new PointHitTestResult(this, p);
        }
        return null;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var vm = _vm;
        if (vm is null) return;
        EnsureDiagramHook();

        var p = e.GetPosition(this);
        bool nearStart = EndpointNear(p, true);
        bool nearEnd = EndpointNear(p, false);

        if (_diagram?.IsEditable == true && (nearStart || nearEnd))
        {
            e.Handled = true;
            _draggingEndpoint = true;
            _dragIsStart = nearStart;
            CaptureMouse();
            Focus();
            _diagram.BeginConnectionEndpointDrag(vm, _dragIsStart, p);
            return;
        }

        // 正交布局：按住中间线段可直接拖动（水平段上下移、竖直段左右移）
        if (_diagram is { IsEditable: true } diagram
            && vm.Routing == PipeRouting.Orthogonal
            && TryHitMiddleSegment(vm, p, out bool horizontal))
        {
            e.Handled = true;
            _draggingSegment = true;
            _dragSegmentHorizontal = horizontal;
            _segmentDragMoved = false;
            _segmentDragStart = p;
            CaptureMouse();
            Focus();
            diagram.SelectedConnection = vm;
            diagram.BeginConnectionSegmentDrag(vm);
            return;
        }

        e.Handled = true;
        if (_diagram is { } selectedDiagram)
        {
            selectedDiagram.SelectedConnection = vm;
            Focus(); // 选中即落焦点：Delete/Esc 等键盘事件经冒泡可达画布
        }
    }

    /// <summary>
    /// 命中"中间线段"：首段与末段是端口出线段（端点固定），不可拖动；
    /// 命中容差 ±8px，输出该段是否水平（水平段按 Y 平移，竖直段按 X 平移）。
    /// </summary>
    private static bool TryHitMiddleSegment(ConnectionViewModel vm, Point p, out bool horizontal)
    {
        horizontal = false;
        var pts = vm.Points;
        // 首末段各占一段，至少 4 点才可能存在中间段
        if (pts.Length < 4) return false;

        const double thresholdSquared = 8.0 * 8.0;
        double best = double.MaxValue;
        int bestIndex = -1;
        for (int i = 1; i < pts.Length - 2; i++)
        {
            double d = DistanceToSegmentSquared(p, pts[i], pts[i + 1]);
            if (d <= thresholdSquared && d < best)
            {
                best = d;
                bestIndex = i;
            }
        }

        if (bestIndex < 0) return false;
        horizontal = Math.Abs(pts[bestIndex].Y - pts[bestIndex + 1].Y) < 0.01;
        return true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        // 拖动中间线段（正交布局）
        if (_draggingSegment)
        {
            if (!IsMouseCaptured || _vm is null) return;

            // 拖拽中途切出编辑模式：立即终止，避免运行模式下继续改写图模型
            if (_diagram is not { IsEditable: true } segDiagram)
            {
                FinishSegmentDrag(cancel: true);
                return;
            }

            var sp = e.GetPosition(this);
            // 超过系统拖拽阈值前不视为拖动：避免误吞点击
            if (!_segmentDragMoved)
            {
                if (Math.Abs(sp.X - _segmentDragStart.X) < SystemParameters.MinimumHorizontalDragDistance
                    && Math.Abs(sp.Y - _segmentDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
                    return;
                _segmentDragMoved = true;
            }

            // 水平段按 Y 平移（垂直移动），竖直段按 X 平移（水平移动）
            segDiagram.UpdateConnectionSegmentDrag(
                _vm, _dragSegmentHorizontal, _dragSegmentHorizontal ? sp.Y : sp.X);
            return;
        }

        if (!_draggingEndpoint || !IsMouseCaptured || _vm is null) return;

        // 拖拽中途切出编辑模式：立即终止本次端点拖拽，避免运行模式下继续改写图模型
        if (_diagram is not { IsEditable: true })
        {
            FinishEndpointDrag();
            return;
        }

        var p = e.GetPosition(this);
        _diagram?.UpdateConnectionEndpointDrag(_vm, _dragIsStart, p);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_draggingSegment)
            FinishSegmentDrag(cancel: false);
        if (_draggingEndpoint)
            FinishEndpointDrag();
    }

    /// <summary>捕获被外部夺走（如模态对话框弹出）：按收尾路径复位，避免拖拽标志与吸附高亮残留。</summary>
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_draggingSegment)
            FinishSegmentDrag(cancel: false);
        if (_draggingEndpoint)
            FinishEndpointDrag();
    }

    /// <summary>Esc 取消拖拽：端点回退到拖拽前锚点；中间线段回退到拖动前的绕行位置。
    /// 拖拽开始时已落焦点，键盘事件由此处接收后交图模型编排回退。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Escape) return;

        if (_draggingSegment)
        {
            e.Handled = true;
            FinishSegmentDrag(cancel: true);
            if (IsMouseCaptured) ReleaseMouseCapture();
            return;
        }

        if (!_draggingEndpoint) return;

        e.Handled = true;
        _draggingEndpoint = false;
        if (_vm is { } conn) _diagram?.CancelConnectionEndpointDrag(conn);
        if (IsMouseCaptured) ReleaseMouseCapture();
    }

    /// <summary>收尾端点拖拽：提交落点（吸附端口或自由端）、清除吸附高亮并复位状态。
    /// 须先清 _draggingEndpoint 再释放捕获，使释放捕获触发的 LostMouseCapture 成为空操作。</summary>
    private void FinishEndpointDrag()
    {
        _draggingEndpoint = false;
        if (_vm is { } conn) _diagram?.EndConnectionEndpointDrag(conn);
        if (IsMouseCaptured) ReleaseMouseCapture();
    }

    /// <summary>收尾中间线段拖拽：提交（或 Esc 回退）绕行位置。
    /// 须先清 _draggingSegment 再释放捕获，使释放捕获触发的 LostMouseCapture 成为空操作。</summary>
    private void FinishSegmentDrag(bool cancel)
    {
        _draggingSegment = false;
        if (_vm is { } conn)
        {
            if (cancel) _diagram?.CancelConnectionSegmentDrag(conn);
            else _diagram?.EndConnectionSegmentDrag(conn);
        }
        if (IsMouseCaptured) ReleaseMouseCapture();
    }

    /// <summary>是否靠近某一端点（半径 12 的平方比较，不开根号）。</summary>
    private bool EndpointNear(Point p, bool isStart)
    {
        var pts = _vm?.Points;
        if (pts is null || pts.Length < 2) return false;
        var endpoint = isStart ? pts[0] : pts[^1];
        return (p - endpoint).LengthSquared < 12.0 * 12.0;
    }

    private static double DistanceToSegmentSquared(Point p, Point a, Point b)
    {
        var ab = b - a;
        double len2 = ab.LengthSquared;
        if (len2 < 0.0001) return (p - a).LengthSquared;
        double t = Math.Clamp(((p - a) * ab) / len2, 0, 1);
        var proj = a + t * ab;
        return (p - proj).LengthSquared;
    }

    private static Brush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static Pen MakePen(Brush brush, double thickness, DashStyle? dash, bool freeze)
    {
        var pen = new Pen(brush, thickness)
        {
            LineJoin = PenLineJoin.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        if (dash != null) pen.DashStyle = dash;
        if (freeze) pen.Freeze();
        return pen;
    }
}
