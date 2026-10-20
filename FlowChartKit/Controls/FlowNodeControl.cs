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

    /// <summary>标签文字相对图形底边的下移量：让开端口圆点与管路端点手柄。</summary>
    private const double LabelGap = 8;

    /// <summary>图形下方留给标签的高度（含下移量与文字高度）。</summary>
    private const double LabelBandHeight = 27;

    private NodeViewModel? _vm;
    private DiagramViewModel? _diagram;
    private bool _dragging;
    private Point _dragStartCanvas;
    private double _origX, _origY;

    // 标签文本缓存：仅在 Label 或 DPI 变化时重建，避免每次 OnRender 构造 FormattedText
    private FormattedText? _labelText;
    private double _labelTextDpi = -1;

    // 宿主 ItemsControl 的 Canvas 面板缓存：可视树祖先不变，无需每次鼠标事件都遍历
    private Canvas? _itemsCanvas;

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

        if (_diagram != null)
        {
            _diagram.PropertyChanged -= OnDiagramPropertyChanged;
            _diagram = null;
        }

        _vm = e.NewValue as NodeViewModel;

        if (_vm != null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            foreach (var port in _vm.Ports)
                port.PropertyChanged += OnPortPropertyChanged;
            Width = _vm.ShapeWidth;
            Height = _vm.ShapeHeight + LabelBandHeight;
        }

        InvalidateVisual();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(NodeViewModel.IsSelected):
                InvalidateVisual();
                break;

            case nameof(NodeViewModel.Label):
                _labelText = null;
                InvalidateVisual();
                break;

            // 缩放后同步控件占位尺寸并重绘
            case nameof(NodeViewModel.ShapeWidth):
            case nameof(NodeViewModel.ShapeHeight):
                if (_vm != null)
                {
                    Width = _vm.ShapeWidth;
                    Height = _vm.ShapeHeight + LabelBandHeight;
                }
                InvalidateVisual();
                break;
        }
    }

    private void OnPortPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PortViewModel.IsHighlighted))
            InvalidateVisual();
    }

    private void OnDiagramPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DiagramViewModel.IsEditable))
            InvalidateVisual();
    }

    /// <summary>
    /// 挂上图模型的属性通知以跟随编辑模式重绘。
    /// NodeViewModel.Diagram 由 AddNode 赋值且不发通知，所以在渲染时幂等地补齐订阅。
    /// </summary>
    private void EnsureDiagramHook()
    {
        var diagram = _vm?.Diagram;
        if (diagram == _diagram) return;

        if (_diagram != null)
            _diagram.PropertyChanged -= OnDiagramPropertyChanged;

        _diagram = diagram;

        if (_diagram != null)
            _diagram.PropertyChanged += OnDiagramPropertyChanged;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var vm = _vm;
        if (vm is null) return;
        EnsureDiagramHook();

        // 部件外观：NodeRenderer 按该类型的基准尺寸绘制，这里用缩放系数映射到实际尺寸
        dc.PushTransform(new ScaleTransform(vm.Scale, vm.Scale));
        NodeRenderer.Render(dc, vm.Kind);
        dc.Pop();

        // 选中边框（橙色虚线矩形）
        if (vm.IsSelected)
            dc.DrawRectangle(null, SelectionPen, new Rect(0, 0, vm.ShapeWidth, vm.ShapeHeight));

        // 端口：编辑模式下全部常显；关闭编辑模式后不绘制（此时也禁止拖动，用不到端口）
        if (_diagram?.IsEditable == true)
        {
            foreach (var port in vm.Ports)
            {
                double cx = port.FX * vm.ShapeWidth;
                double cy = port.FY * vm.ShapeHeight;
                NodeRenderer.RenderPort(dc, cx, cy);
                if (port.IsHighlighted)
                    NodeRenderer.RenderPortHighlight(dc, cx, cy);
            }
        }

        // 标签（缓存 FormattedText，仅 Label/DPI 变化时重建）
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        if (_labelText is null || _labelTextDpi != dpi)
        {
            _labelText = new FormattedText(
                vm.Label ?? string.Empty,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Arial"),
                11.0,
                LabelBrush,
                dpi);
            _labelTextDpi = dpi;
        }
        double tx = (vm.ShapeWidth - _labelText.Width) / 2;
        dc.DrawText(_labelText, new Point(Math.Max(0, tx), vm.ShapeHeight + LabelGap));
    }

    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
    {
        var vm = _vm;
        if (vm is null) return null;

        var p = hitTestParameters.HitPoint;

        // 只认部件图形本身：图形下方的标签带不归部件所有，
        // 否则底边端口附近的点击会被部件层吃掉（部件层在管路层之上）
        if (p.X < 0 || p.X > vm.ShapeWidth || p.Y < 0 || p.Y > vm.ShapeHeight)
            return null;

        // 端口上已接有管路时，把端点附近让给管路层，
        // 否则端点正好压在部件边界上，抓不住（半径与 PipeControl 的 12px 端点判定一致）
        if (HasConnectionEndpointNear(vm, new Point(vm.X + p.X, vm.Y + p.Y)))
            return null;

        return new PointHitTestResult(this, p);
    }

    /// <summary>该画布坐标附近是否有管路端点，有则说明此处应优先由管路层处理。</summary>
    private static bool HasConnectionEndpointNear(NodeViewModel node, Point canvasPoint)
    {
        var diagram = node.Diagram;
        if (diagram is null) return false;

        // 半径 12 的平方（与 PipeControl 的 12px 端点判定一致），全程不开根号
        const double radiusSquared = 12.0 * 12.0;

        foreach (var conn in diagram.Connections)
        {
            var pts = conn.Points;
            if (pts.Length < 2) continue;

            if ((pts[0] - canvasPoint).LengthSquared < radiusSquared
                || (pts[^1] - canvasPoint).LengthSquared < radiusSquared)
                return true;
        }

        return false;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_vm is null) return;
        if (Diagram is not { IsEditable: true } diagram) return;

        e.Handled = true;
        diagram.SelectedNode = _vm;
        Focus();

        var canvas = GetItemsCanvas();
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
        var canvas = GetItemsCanvas();
        var p = e.GetPosition(canvas);

        double nx = _origX + p.X - _dragStartCanvas.X;
        double ny = _origY + p.Y - _dragStartCanvas.Y;
        var diagram = Diagram;
        if (diagram != null)
        {
            nx = Math.Clamp(nx, 0, Math.Max(0, diagram.CanvasWidth - _vm.ShapeWidth));
            ny = Math.Clamp(ny, 0, Math.Max(0, diagram.CanvasHeight - _vm.ShapeHeight));
        }
        // 一次逻辑移动只触发一次 GeometryChanged，相连管路每帧只重算一次
        _vm.MoveTo(nx, ny);
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

    /// <summary>缓存解析宿主 Canvas；容器一旦生成其可视树祖先不会变化。</summary>
    private Canvas GetItemsCanvas()
    {
        if (_itemsCanvas != null) return _itemsCanvas;

        DependencyObject? d = this;
        while (d != null)
        {
            if (d is Canvas c)
            {
                _itemsCanvas = c;
                return c;
            }
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
