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
/// </summary>
public class PipeControl : Control
{
    private static readonly Brush PipeBrush = CreateBrush(0x6C, 0x9B, 0xD2);
    private static readonly Brush SelectedBrush = CreateBrush(0xE6, 0x7E, 0x22);
    private static readonly Brush HandleStroke = CreateBrush(0x2F, 0x6F, 0xAF);

    public static readonly DependencyProperty DashOffsetProperty = DependencyProperty.Register(
        nameof(DashOffset), typeof(double), typeof(PipeControl),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    private ConnectionViewModel? _vm;
    private DiagramViewModel? _diagram;
    private bool _draggingEndpoint;
    private bool _dragIsStart;

    static PipeControl()
    {
        FocusableProperty.OverrideMetadata(typeof(PipeControl),
            new FrameworkPropertyMetadata(true));
    }

    public PipeControl()
    {
        DataContextChanged += OnDataContextChanged;
        Background = Brushes.Transparent;
    }

    public double DashOffset
    {
        get => (double)GetValue(DashOffsetProperty);
        set => SetValue(DashOffsetProperty, value);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm != null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            if (_diagram != null)
                _diagram.PropertyChanged -= OnDiagramPropertyChanged;
        }

        _vm = e.NewValue as ConnectionViewModel;

        if (_vm != null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            var diagram = _vm.Diagram;
            _diagram = diagram;
            diagram.PropertyChanged += OnDiagramPropertyChanged;
            Width = diagram.CanvasWidth;
            Height = diagram.CanvasHeight;
            UpdateFlowAnimation();
        }
        else
        {
            _diagram = null;
        }
        InvalidateVisual();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ConnectionViewModel.Points):
            case nameof(ConnectionViewModel.IsSelected):
            case nameof(ConnectionViewModel.HasArrow):
            case nameof(ConnectionViewModel.ArrowTip):
            case nameof(ConnectionViewModel.ArrowBase1):
            case nameof(ConnectionViewModel.ArrowBase2):
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

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var vm = _vm;
        if (vm is null) return;
        var pts = vm.Points;
        if (pts is null || pts.Length < 2) return;

        bool selected = vm.IsSelected;
        var brush = selected ? SelectedBrush : PipeBrush;
        double thickness = selected ? 2.6 : 2.0;

        var pen = new Pen(brush, thickness);
        pen.LineJoin = PenLineJoin.Round;
        pen.StartLineCap = PenLineCap.Round;
        pen.EndLineCap = PenLineCap.Round;
        if (vm.Direction != FlowMode.None)
            pen.DashStyle = new DashStyle(new double[] { 4, 4 }, DashOffset);

        // 管路折线
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(pts[0], false, false);
            ctx.PolyLineTo(pts.Skip(1).ToList(), true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(null, pen, geo);

        // 流向箭头
        if (vm.HasArrow)
        {
            var arrow = new StreamGeometry();
            using (var ctx = arrow.Open())
            {
                ctx.BeginFigure(vm.ArrowTip, true, true);
                ctx.LineTo(vm.ArrowBase1, true, false);
                ctx.LineTo(vm.ArrowBase2, true, false);
            }
            arrow.Freeze();
            dc.DrawGeometry(brush, null, arrow);
        }

        // 端点手柄（仅编辑模式可见）
        if (vm.Diagram.IsEditable)
        {
            dc.DrawEllipse(Brushes.White, new Pen(HandleStroke, 1.5), pts[0], 5, 5);
            dc.DrawEllipse(Brushes.White, new Pen(HandleStroke, 1.5), pts[^1], 5, 5);
        }

        // 编号徽章（按 ShowNumbers 开关）—— 位于管路折线长度中点处
        if (vm.Diagram.ShowNumbers)
        {
            double totalLen = 0;
            for (int i = 1; i < pts.Length; i++)
                totalLen += (pts[i] - pts[i - 1]).Length;
            double half = totalLen / 2;
            double acc = 0;
            var center = pts[0];
            for (int i = 1; i < pts.Length; i++)
            {
                var seg = pts[i] - pts[i - 1];
                double segLen = seg.Length;
                if (acc + segLen >= half)
                {
                    double t = segLen > 0 ? (half - acc) / segLen : 0;
                    center = new Point(pts[i - 1].X + seg.X * t, pts[i - 1].Y + seg.Y * t);
                    break;
                }
                acc += segLen;
            }

            var badgePen = new Pen(brush, 1.6);
            dc.DrawEllipse(Brushes.White, badgePen, center, 11, 11);

            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var ft = new FormattedText(
                vm.Number.ToString(),
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Arial"),
                11.0,
                brush,
                dpi);
            dc.DrawText(ft, new Point(center.X - ft.Width / 2, center.Y - ft.Height / 2));
        }
    }

    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
    {
        var p = hitTestParameters.HitPoint;
        var pts = _vm?.Points;
        if (pts is null || pts.Length < 2) return null;

        for (int i = 0; i < pts.Length - 1; i++)
        {
            if (DistanceToSegment(p, pts[i], pts[i + 1]) <= 8.0)
                return new PointHitTestResult(this, p);
        }
        return null;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var vm = _vm;
        if (vm is null) return;

        var p = e.GetPosition(this);
        bool nearStart = ptsNear(p, true, pts => pts[0]);
        bool nearEnd = ptsNear(p, false, pts => pts[^1]);

        if (vm.Diagram.IsEditable && (nearStart || nearEnd))
        {
            e.Handled = true;
            _draggingEndpoint = true;
            _dragIsStart = nearStart;
            CaptureMouse();
            Focus();
            vm.BeginEndpointDrag(_dragIsStart, p);
            return;
        }

        e.Handled = true;
        vm.Diagram.SelectedConnection = vm;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_draggingEndpoint || !IsMouseCaptured || _vm is null) return;
        var p = e.GetPosition(this);
        var opposite = _dragIsStart ? _vm.TargetPort : _vm.SourcePort;
        var (snap, pos) = _vm.Diagram.FindNearestPort(p, opposite);
        _vm.Diagram.SetSnapHighlight(snap);
        _vm.UpdateDragPoint(p, snap);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_draggingEndpoint)
        {
            _draggingEndpoint = false;
            _vm?.EndEndpointDrag();
            _vm?.Diagram.SetSnapHighlight(null);
            if (IsMouseCaptured) ReleaseMouseCapture();
        }
    }

    private bool ptsNear(Point p, bool isStart, Func<Point[], Point> pick)
    {
        var pts = _vm?.Points;
        if (pts is null || pts.Length < 2) return false;
        return (p - pick(pts)).Length < 12;
    }

    private static double DistanceToSegment(Point p, Point a, Point b)
    {
        var ab = b - a;
        double len2 = ab.LengthSquared;
        if (len2 < 0.0001) return (p - a).Length;
        double t = Math.Clamp(((p - a) * ab) / len2, 0, 1);
        var proj = a + t * ab;
        return (p - proj).Length;
    }

    private static Brush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
