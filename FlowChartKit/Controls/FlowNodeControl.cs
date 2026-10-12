using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FlowChartKit.Rendering;
using FlowChartKit.ViewModels;

namespace FlowChartKit.Controls;

/// <summary>
/// 部件控件（继承 Control）：不使用 ControlTemplate，通过 OnRender 自绘。
/// DataContext 应为 NodeViewModel；位置由 ItemsControl 的 ItemContainerStyle 绑定 Canvas.Left/Top。
/// 支持拖拽移动、选中态、端口吸附高亮。
/// </summary>
public class FlowNodeControl : Control
{
    private static readonly Brush LabelBrush = CreateBrush(0x33, 0x51, 0x7A);
    private static readonly Pen SelectionPen = CreateSelectionPen();

    private NodeViewModel? _vm;
    private bool _dragging;
    private Point _dragStartCanvas;
    private double _origX, _origY;

    static FlowNodeControl()
    {
        FocusableProperty.OverrideMetadata(typeof(FlowNodeControl),
            new FrameworkPropertyMetadata(true));
    }

    public FlowNodeControl()
    {
        DataContextChanged += OnDataContextChanged;
        Background = Brushes.Transparent;
    }

    /// <summary>所属图模型（从 NodeViewModel.Diagram 读取）。</summary>
    public DiagramViewModel? Diagram => _vm?.Diagram;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm != null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            foreach (var port in _vm.Ports)
                port.PropertyChanged -= OnPortPropertyChanged;
        }

        _vm = e.NewValue as NodeViewModel;

        if (_vm != null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            foreach (var port in _vm.Ports)
                port.PropertyChanged += OnPortPropertyChanged;
            Width = _vm.ShapeWidth;
            Height = _vm.ShapeHeight + 22;
        }

        InvalidateVisual();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NodeViewModel.IsSelected) or nameof(NodeViewModel.Label))
            InvalidateVisual();
    }

    private void OnPortPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PortViewModel.IsHighlighted))
            InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var vm = _vm;
        if (vm is null) return;

        // 部件外观
        NodeRenderer.Render(dc, vm.Kind);

        // 选中边框（橙色虚线矩形）
        if (vm.IsSelected)
            dc.DrawRectangle(null, SelectionPen, new Rect(0, 0, vm.ShapeWidth, vm.ShapeHeight));

        // 端口吸附高亮圈
        foreach (var port in vm.Ports)
        {
            if (port.IsHighlighted)
                NodeRenderer.RenderPortHighlight(dc, port.FX * vm.ShapeWidth, port.FY * vm.ShapeHeight);
        }

        // 标签
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var ft = new FormattedText(
            vm.Label ?? string.Empty,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Arial"),
            11.0,
            LabelBrush,
            dpi);
        double tx = (vm.ShapeWidth - ft.Width) / 2;
        dc.DrawText(ft, new Point(Math.Max(0, tx), vm.ShapeHeight + 3));
    }

    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
    {
        var p = hitTestParameters.HitPoint;
        if (p.X >= 0 && p.X <= ActualWidth && p.Y >= 0 && p.Y <= ActualHeight)
            return new PointHitTestResult(this, p);
        return null;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_vm is null) return;
        if (Diagram is not { IsEditable: true } diagram) return;

        e.Handled = true;
        diagram.SelectedNode = _vm;
        Focus();

        var canvas = FindItemsCanvas();
        _dragStartCanvas = e.GetPosition(canvas);
        _origX = _vm.X;
        _origY = _vm.Y;
        _dragging = true;
        CaptureMouse();
        Cursor = Cursors.SizeAll;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging || !IsMouseCaptured || _vm is null) return;
        var canvas = FindItemsCanvas();
        var p = e.GetPosition(canvas);

        double nx = _origX + p.X - _dragStartCanvas.X;
        double ny = _origY + p.Y - _dragStartCanvas.Y;
        var diagram = Diagram;
        if (diagram != null)
        {
            nx = Math.Clamp(nx, 0, Math.Max(0, diagram.CanvasWidth - _vm.ShapeWidth));
            ny = Math.Clamp(ny, 0, Math.Max(0, diagram.CanvasHeight - _vm.ShapeHeight));
        }
        _vm.X = nx;
        _vm.Y = ny;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_dragging)
        {
            _dragging = false;
            if (IsMouseCaptured) ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
        }
    }

    private Canvas FindItemsCanvas()
    {
        DependencyObject? d = this;
        while (d != null)
        {
            if (d is Canvas c) return c;
            d = VisualTreeHelper.GetParent(d);
        }
        return null!;
    }

    private static Brush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static Pen CreateSelectionPen()
    {
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22)), 1.5);
        pen.DashStyle = DashStyles.Dash;
        pen.Freeze();
        return pen;
    }
}
