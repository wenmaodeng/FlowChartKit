using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using FlowChartKit;
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
    private static readonly Brush PipeBrush = CreateBrush(0x6C, 0x9B, 0xD2);
    private static readonly Brush SelectedBrush = CreateBrush(0xE6, 0x7E, 0x22);
    private static readonly Brush HandleStroke = CreateBrush(0x2F, 0x6F, 0xAF);

    // 实线 Pen 冻结复用；虚线 Pen 每控件一对，共享可变的 DashStyle（DashOffset 动画驱动）
    private static readonly Pen PipePen = MakePen(PipeBrush, 2.0, dash: null, freeze: true);
    private static readonly Pen PipePenSelected = MakePen(SelectedBrush, 2.6, dash: null, freeze: true);
    private static readonly Pen HandlePen = MakePen(HandleStroke, 1.5, dash: null, freeze: true);
    private static readonly Pen BadgePen = MakePen(PipeBrush, 1.6, dash: null, freeze: true);
    private static readonly Pen BadgePenSelected = MakePen(SelectedBrush, 1.6, dash: null, freeze: true);

    private readonly DashStyle _dashStyle = new(new double[] { 4, 4 }, 0);
    private readonly Pen _dashPen;
    private readonly Pen _dashPenSelected;

    public static readonly DependencyProperty DashOffsetProperty = DependencyProperty.Register(
        nameof(DashOffset), typeof(double), typeof(PipeControl),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private ConnectionViewModel? _vm;
    private DiagramViewModel? _diagram;
    private bool _draggingEndpoint;
    private bool _dragIsStart;

    // 渲染缓存：折线几何、徽章中点、编号文本
    private StreamGeometry? _geometry;
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
        _dashPen = MakePen(PipeBrush, 2.0, _dashStyle, freeze: false);
        _dashPenSelected = MakePen(SelectedBrush, 2.6, _dashStyle, freeze: false);
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
            RebuildGeometry();
            UpdateFlowAnimation();
        }
        else
        {
            _geometry = null;
            _badgeText = null;
            // VM 脱离时停止 Forever 动画，避免空转并根住控件
            BeginAnimation(DashOffsetProperty, null);
        }
        InvalidateVisual();
    }

    /// <summary>
    /// 挂上图模型的属性通知（编号开关）并同步占位尺寸。
    /// DataContextChanged 时控件可能尚未插入可视树，找不到祖先 ItemsControl，
    /// 所以解析失败时在 OnRender 中幂等重试（与 FlowNodeControl.EnsureDiagramHook 同理）。
    /// </summary>
    private void EnsureDiagramHook()
    {
        if (_vm is null) return;
        var diagram = FindAncestorDiagram();
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

    /// <summary>沿可视树向上找 ItemsControl，取其 DataContext 作为所属图模型。</summary>
    private DiagramViewModel? FindAncestorDiagram()
    {
        DependencyObject? d = this;
        while (d != null)
        {
            if (d is ItemsControl ic) return ic.DataContext as DiagramViewModel;
            d = VisualTreeHelper.GetParent(d);
        }
        return null;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
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
        }
    }

    private void OnDiagramPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DiagramViewModel.ShowNumbers))
            InvalidateVisual();
    }

    private void UpdateFlowAnimation()
    {
        if (_vm is null) return;
        switch (_vm.Direction)
        {
            case FlowMode.Forward:
                var fwd = new DoubleAnimation(8, 0, TimeSpan.FromSeconds(0.6))
                {
                    RepeatBehavior = RepeatBehavior.Forever
                };
                BeginAnimation(DashOffsetProperty, fwd);
                break;
            case FlowMode.Reverse:
                var rev = new DoubleAnimation(0, 8, TimeSpan.FromSeconds(0.6))
                {
                    RepeatBehavior = RepeatBehavior.Forever
                };
                BeginAnimation(DashOffsetProperty, rev);
                break;
            default:
                BeginAnimation(DashOffsetProperty, null);
                break;
        }
    }

    /// <summary>折线变化时重建几何与徽章中点；OnRender 不再做几何构建与长度累计。</summary>
    private void RebuildGeometry()
    {
        _geometry = null;
        var pts = _vm?.Points;
        if (pts is null || pts.Length < 2) return;

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(pts[0], false, false);
            for (int i = 1; i < pts.Length; i++)
                ctx.LineTo(pts[i], true, false);
        }
        geo.Freeze();
        _geometry = geo;
        _badgeCenter = ComputeBadgeCenter(pts);
    }

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
                selected ? SelectedBrush : PipeBrush,
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

        bool selected = vm.IsSelected;
        bool dashed = vm.Direction != FlowMode.None;
        Pen pen;
        if (dashed)
        {
            _dashStyle.Offset = DashOffset;
            pen = selected ? _dashPenSelected : _dashPen;
        }
        else
        {
            pen = selected ? PipePenSelected : PipePen;
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
            dc.DrawEllipse(Brushes.White, selected ? BadgePenSelected : BadgePen, center, 11, 11);

            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var ft = GetBadgeText(vm.Number, selected, dpi);
            dc.DrawText(ft, new Point(center.X - ft.Width / 2, center.Y - ft.Height / 2));
        }
    }

    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
    {
        var p = hitTestParameters.HitPoint;
        var pts = _vm?.Points;
        if (pts is null || pts.Length < 2) return null;

        // 距离阈值 8 的平方，全程不开根号
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
        // 首次交互可能早于首次渲染，这里幂等补齐图模型挂钩
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

        e.Handled = true;
        if (_diagram is { } diagram)
            diagram.SelectedConnection = vm;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_draggingEndpoint || !IsMouseCaptured || _vm is null) return;
        var p = e.GetPosition(this);
        _diagram?.UpdateConnectionEndpointDrag(_vm, _dragIsStart, p);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_draggingEndpoint)
        {
            _draggingEndpoint = false;
            if (_vm is { } conn) _diagram?.EndConnectionEndpointDrag(conn);
            if (IsMouseCaptured) ReleaseMouseCapture();
        }
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
