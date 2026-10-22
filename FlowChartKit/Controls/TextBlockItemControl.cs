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
/// 文本模块控件：OnRender 自绘文本（白底衬底 + 缓存 FormattedText），编辑模式下可拖动；
/// 内容与字号经属性窗口（右键菜单 → 属性）编辑，选中态显示虚线框，编辑模式开启编号显示时
/// 在左上角显示与管路同风格的编号徽章。
/// 交互骨架与 <see cref="FlowNodeControl"/> 同一模式：命中限定文本框、拖动阈值、鼠标捕获、
/// <see cref="DiagramViewModel.IsEditable"/> 门禁。
/// </summary>
public class TextBlockItemControl : Control
{
    private TextBlockViewModel? _vm;
    private DiagramViewModel? _diagram;

    // 拖动状态
    private bool _dragging;
    private bool _dragMoved;
    private Point _dragStartCanvas;
    private double _origX;
    private double _origY;

    // 文本缓存：仅在内容 / 字号 / DPI 变化时重建
    private FormattedText? _text;
    private string _textValue = "";
    private double _textSize = -1;
    private double _textDpi = -1;

    // 编号徽章文本缓存：仅在编号 / DPI 变化时重建
    private FormattedText? _numText;
    private int _numTextValue = -1;
    private double _numTextDpi = -1;

    // 编号徽章：白底圆 + 主色数字，与管路编号徽章同一语义（编辑模式 + ShowNumbers 时显示）
    private const double NumberBadgeRadius = 8;
    private static readonly Pen NumberBadgePen = CreatePen(0xE6, 0x7E, 0x22, 1.2);

    private static readonly Brush TextBrush = NodeRenderer.Frozen(0x30, 0x30, 0x30);
    private static readonly Pen SelectionPen = CreatePen(0xE6, 0x7E, 0x22, 1.0, new DoubleCollection { 3, 3 });

    public TextBlockItemControl()
    {
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => _dragging = false;
        Background = Brushes.Transparent;
        Cursor = Cursors.Arrow;
        ClipToBounds = false;   // 编号徽章画在文本框上方（控件区域外）
    }

    /// <summary>所属图模型（从 TextBlockViewModel.Diagram 读取）。</summary>
    public DiagramViewModel? Diagram => _diagram;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm != null)
            _vm.PropertyChanged -= OnVmPropertyChanged;
        if (_diagram != null)
            _diagram.PropertyChanged -= OnDiagramPropertyChanged;

        _vm = e.NewValue as TextBlockViewModel;
        _diagram = _vm?.Diagram;
        _text = null;

        if (_vm != null)
            _vm.PropertyChanged += OnVmPropertyChanged;
        if (_diagram != null)
            _diagram.PropertyChanged += OnDiagramPropertyChanged;

        UpdateCursor();
        InvalidateVisual();
        InvalidateMeasure();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(TextBlockViewModel.Content):
            case nameof(TextBlockViewModel.FontSize):
                _text = null;
                InvalidateMeasure();
                InvalidateVisual();
                break;

            case nameof(TextBlockViewModel.Number):
            case nameof(TextBlockViewModel.IsSelected):
                InvalidateVisual();
                break;
        }
    }

    /// <summary>编辑模式 / 编号显示开关变化：刷新光标与徽章。</summary>
    private void OnDiagramPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DiagramViewModel.IsEditable) or nameof(DiagramViewModel.ShowNumbers))
        {
            UpdateCursor();
            InvalidateVisual();
        }
    }

    /// <summary>光标：仅编辑模式显示可拖动光标（运行模式不可拖动，避免误导）。</summary>
    private void UpdateCursor()
        => Cursor = _diagram is { IsEditable: true } ? Cursors.SizeAll : Cursors.Arrow;

    /// <summary>占位尺寸即文本尺寸：由 Measure 直接给出（不依赖渲染副作用），首帧即可命中。</summary>
    protected override Size MeasureOverride(Size constraint)
    {
        var vm = _vm;
        if (vm is null) return new Size();

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var ft = GetText(vm, dpi);
        return new Size(ft.Width, ft.Height);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var vm = _vm;
        if (vm is null) return;

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var ft = GetText(vm, dpi);

        // DPI 变化等场景兜底：测得尺寸与当前占位不符时重测一次（之后差值消失，不会持续触发）
        if (Math.Abs(ActualWidth - ft.Width) > 0.5 || Math.Abs(ActualHeight - ft.Height) > 0.5)
            InvalidateMeasure();

        // 白底衬底：保证压在管路 / 网格上时可读
        var rect = new Rect(0, 0, ft.Width, ft.Height);
        dc.DrawRectangle(Brushes.White, null, rect);
        dc.DrawText(ft, new Point(0, 0));

        // 选中态：虚线框
        if (vm.IsSelected)
            dc.DrawRectangle(null, SelectionPen,
                new Rect(rect.X - 3, rect.Y - 2, rect.Width + 6, rect.Height + 4));

        // 编号徽章（编辑模式且开启编号显示）：画在文本框上方左角，与管路徽章同风格
        if (_diagram?.ShowNumbers == true && vm.Number > 0)
        {
            var center = new Point(NumberBadgeRadius + 1, -(NumberBadgeRadius + 4));
            dc.DrawEllipse(Brushes.White, NumberBadgePen, center, NumberBadgeRadius, NumberBadgeRadius);
            var numFt = GetNumberText(vm.Number, dpi);
            dc.DrawText(numFt, new Point(center.X - numFt.Width / 2, center.Y - numFt.Height / 2));
        }
    }

    private FormattedText GetText(TextBlockViewModel vm, double dpi)
    {
        if (_text is null || _textValue != vm.Content || _textSize != vm.FontSize || _textDpi != dpi)
        {
            _text = new FormattedText(
                vm.Content,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Arial"),
                vm.FontSize,
                TextBrush,
                dpi);
            _textValue = vm.Content;
            _textSize = vm.FontSize;
            _textDpi = dpi;
        }
        return _text;
    }

    /// <summary>编号文本按编号 / DPI 缓存（与部件序号徽章同一字体与配色）。</summary>
    private FormattedText GetNumberText(int value, double dpi)
    {
        if (_numText is null || _numTextValue != value || _numTextDpi != dpi)
        {
            _numText = new FormattedText(
                value.ToString(),
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Arial"),
                9.0,
                NodeRenderer.Accent,
                dpi);
            _numTextValue = value;
            _numTextDpi = dpi;
        }
        return _numText;
    }

    /// <summary>命中限定在文本框内：空白处让给下层管路。</summary>
    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
    {
        if (_vm is null) return null;
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        return bounds.Contains(hitTestParameters.HitPoint)
            ? new PointHitTestResult(this, hitTestParameters.HitPoint)
            : null;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_vm is null) return;

        Focus();   // Delete/Esc 等键盘事件经冒泡可达画布
        if (_diagram is not { IsEditable: true } diagram) return;

        e.Handled = true;
        diagram.SelectedTextBlock = _vm;

        var canvas = GetItemsCanvas();
        if (canvas is null) return;

        _dragging = true;
        _dragMoved = false;
        _dragStartCanvas = e.GetPosition(canvas);
        _origX = _vm.X;
        _origY = _vm.Y;
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging || !IsMouseCaptured || _vm is null) return;

        // 拖拽中途切出编辑模式：立即终止，避免运行模式下继续改写图模型
        if (_diagram is not { IsEditable: true } diagram)
        {
            FinishDrag();
            return;
        }

        var canvas = GetItemsCanvas();
        if (canvas is null) return;

        var p = e.GetPosition(canvas);
        double dx = p.X - _dragStartCanvas.X;
        double dy = p.Y - _dragStartCanvas.Y;

        // 超过系统拖拽阈值前不视为拖动：避免轻微抖动误吞点击
        if (!_dragMoved)
        {
            if (Math.Abs(dx) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(dy) < SystemParameters.MinimumVerticalDragDistance)
                return;
            _dragMoved = true;
        }

        // 钳到画布内（按当前文本占位尺寸）
        double nx = Math.Clamp(_origX + dx, 0, Math.Max(0, diagram.CanvasWidth - ActualWidth));
        double ny = Math.Clamp(_origY + dy, 0, Math.Max(0, diagram.CanvasHeight - ActualHeight));
        _vm.MoveTo(nx, ny);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_dragging) FinishDrag();
    }

    /// <summary>捕获被外部夺走：按收尾路径复位，避免拖拽标志残留。</summary>
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_dragging) FinishDrag();
    }

    private void FinishDrag()
    {
        _dragging = false;
        if (IsMouseCaptured) ReleaseMouseCapture();
    }

    /// <summary>宿主 ItemsControl 的 Canvas 面板：沿可视树上溯找到 IsItemsHost 的画布。</summary>
    private Canvas? GetItemsCanvas()
    {
        DependencyObject d = this;
        while ((d = VisualTreeHelper.GetParent(d)) is not null)
            if (d is Canvas c && c.IsItemsHost)
                return c;
        return null;
    }

    private static Pen CreatePen(byte r, byte g, byte b, double thickness, DoubleCollection? dash = null)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        var pen = dash is { Count: > 0 }
            ? new Pen(brush, thickness) { DashStyle = new DashStyle(dash, 0) }
            : new Pen(brush, thickness);
        pen.Freeze();
        return pen;
    }
}
