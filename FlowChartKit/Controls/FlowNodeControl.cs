using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FlowChartKit.Rendering;
using FlowChartKit.ViewModels;

namespace FlowChartKit.Controls;

/// <summary>
/// 部件控件（继承 Control）：不使用 ControlTemplate，通过 OnRender 自绘。
/// DataContext 应为 NodeViewModel；位置由 ItemsControl 的 ItemContainerStyle 绑定 Canvas.Left/Top。
/// 支持拖拽移动、选中态、端口吸附高亮，编辑模式下显示同类部件序号徽章。
/// </summary>
public class FlowNodeControl : Control
{
    private static readonly Brush LabelBrush = CreateBrush(0x33, 0x51, 0x7A);
    private static readonly Pen SelectionPen = CreateSelectionPen();
    // 序号徽章描边：复用 NodeRenderer 的冻结画刷与工厂（同程序集 internal 可见）
    private static readonly Pen NumberBadgePen = NodeRenderer.MakePen(NodeRenderer.Outline, 1.2);

    // 右下角缩放手柄：配色与管路端点手柄一致（库内固定的交互元素配色）
    // 右下角手柄对应的光标：拖右下角是 NW-SE 方向
    private static readonly Cursor ResizeCursor = Cursors.SizeNWSE;

    private static readonly Pen ResizeHandlePen = CreatePen(0x2F, 0x6F, 0xAF, 1.5);

    /// <summary>缩放手柄边长（px）：固定尺寸，不随部件缩放变化。</summary>
    private const double ResizeHandleSize = 8;

    /// <summary>标签文字相对图形底边的下移量：让开端口圆点与管路端点手柄。</summary>
    private const double LabelGap = 8;

    /// <summary>图形下方留给标签的高度（含下移量与一行文字高度）：随字号变化，避免大字号被截断。</summary>
    private static double LabelBandHeight(double fontSize) => LabelGap + fontSize * 1.7;

    private NodeViewModel? _vm;
    private DiagramViewModel? _diagram;
    private bool _dragging;
    private bool _dragMoved;
    private Point _dragStartCanvas;

    // 缩放拖拽（拖部件右下角手柄）：与移动互斥
    private bool _resizing;
    private bool _resizeMoved;
    private double _origScale;
    // 鼠标是否悬停在部件上：决定手柄是否显示（未选中的部件悬停时也可直接抓手柄）
    private bool _isMouseOver;

    // 单击退避：目录同时声明单击+双击的部件，单击挂起至双击判定窗口之外，双击时取消（防双击误触单击）
    private DispatcherTimer? _clickBackoffTimer;
    private NodeClickEventArgs? _pendingClickArgs;

    public static readonly RoutedEvent NodeClickEvent = EventManager.RegisterRoutedEvent(
        nameof(NodeClick), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(FlowNodeControl));

    /// <summary>部件被点击（按下-抬起间未发生拖动）时触发的路由事件，冒泡至画布控件统一处理。</summary>
    public event RoutedEventHandler NodeClick
    {
        add => AddHandler(NodeClickEvent, value);
        remove => RemoveHandler(NodeClickEvent, value);
    }
    private double _origX, _origY;

    // 标签文本缓存：仅在 Label 或 DPI 变化时重建，避免每次 OnRender 构造 FormattedText
    private FormattedText? _labelText;
    private double _labelTextDpi = -1;
    private double _labelTextSize = -1;

    // 序号文本缓存：仅在数值或 DPI 变化时重建
    private FormattedText? _numText;
    private int _numTextValue = -1;
    private double _numTextDpi = -1;

    // 宿主 ItemsControl 的 Canvas 面板缓存：可视树祖先不变，无需每次鼠标事件都遍历
    private Canvas? _itemsCanvas;

    // 宿主 DiagramCanvasControl 缓存：读取 AllowNodeInteraction 等画布开关（同上只缓存成功结果）
    private DiagramCanvasControl? _hostCanvas;

    static FlowNodeControl()
    {
        FocusableProperty.OverrideMetadata(typeof(FlowNodeControl),
            new FrameworkPropertyMetadata(true));
    }

    public FlowNodeControl()
    {
        DataContextChanged += OnDataContextChanged;
        // 挂起的单击属于当前部件：脱离可视树或切换上下文时必须取消（自订阅自身，无外部泄漏）
        Unloaded += (_, _) => CancelPendingClick();
        DataContextChanged += (_, _) => CancelPendingClick();
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
        // 容器复用：清掉上一个部件的悬停与缩放残留
        _isMouseOver = false;
        _resizing = false;

        if (_vm != null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            foreach (var port in _vm.Ports)
                port.PropertyChanged += OnPortPropertyChanged;
            SyncSize();
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

            case nameof(NodeViewModel.KindNumber):
                InvalidateVisual();
                break;

            // 宿主业务状态变化：外观可能随 State 区分（定义 Render(dc,node)），触发重绘
            case nameof(NodeViewModel.State):
                InvalidateVisual();
                break;

            // 缩放后同步控件占位尺寸并重绘
            case nameof(NodeViewModel.ShapeWidth):
            case nameof(NodeViewModel.ShapeHeight):
                SyncSize();
                InvalidateVisual();
                break;

            // 字号变化：标签文本缓存失效，占位高度也要跟着变
            case nameof(NodeViewModel.LabelFontSize):
                _labelText = null;
                SyncSize();
                InvalidateVisual();
                break;
        }
    }

    /// <summary>同步控件占位尺寸：宽高取缩放后的图形尺寸，下方留出标签带（随字号变化）。</summary>
    private void SyncSize()
    {
        if (_vm is null) return;
        Width = _vm.ShapeWidth;
        Height = _vm.ShapeHeight + LabelBandHeight(_vm.LabelFontSize);
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

        // DPI 每帧只取一次，供序号与标签两处文本缓存判定使用
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        // 部件外观：NodeRenderer 按该类型的基准尺寸绘制，这里用缩放系数映射到实际尺寸
        dc.PushTransform(new ScaleTransform(vm.Scale, vm.Scale));
        NodeRenderer.Render(dc, vm);
        dc.Pop();

        // 选中边框（橙色虚线矩形）
        if (vm.IsSelected)
            dc.DrawRectangle(null, SelectionPen, new Rect(0, 0, vm.ShapeWidth, vm.ShapeHeight));

        // 端口与序号徽章：编辑模式下常显；关闭编辑模式后不绘制（此时也禁止拖动，用不到端口）
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

            // 同类部件序号徽章（左上角；不随部件缩放，文本缓存复用）
            var badgeCenter = new Point(9, 9);
            dc.DrawEllipse(Brushes.White, NumberBadgePen, badgeCenter, 8, 8);
            var numFt = GetNumberText(vm.KindNumber, dpi);
            dc.DrawText(numFt, new Point(badgeCenter.X - numFt.Width / 2, badgeCenter.Y - numFt.Height / 2));

            // 右下角缩放手柄（选中或悬停时显示）：按住它拖动可等比缩放
            if (ShowResizeHandle(vm))
                dc.DrawRectangle(Brushes.White, ResizeHandlePen,
                    ResizeHandleRect(vm.ShapeWidth, vm.ShapeHeight));
        }

        // 标签（缓存 FormattedText，仅 Label/字号/DPI 变化时重建）
        if (_labelText is null || _labelTextDpi != dpi || _labelTextSize != vm.LabelFontSize)
        {
            _labelText = new FormattedText(
                vm.Label ?? string.Empty,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Arial"),
                vm.LabelFontSize,
                LabelBrush,
                dpi);
            _labelTextDpi = dpi;
            _labelTextSize = vm.LabelFontSize;
        }
        double tx = (vm.ShapeWidth - _labelText.Width) / 2;
        dc.DrawText(_labelText, new Point(Math.Max(0, tx), vm.ShapeHeight + LabelGap));
    }

    /// <summary>序号文本按数值/DPI 缓存，避免每帧构造 FormattedText。</summary>
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

    /// <summary>
    /// 缩放手柄是否可见（也决定是否可抓）：仅编辑模式，且部件已选中或鼠标悬停时显示，
    /// 避免未选中部件上也堆满交互元素。
    /// </summary>
    private bool ShowResizeHandle(NodeViewModel vm)
    {
        if (Diagram is not { IsEditable: true }) return false;
        return vm.IsSelected || _isMouseOver || _resizing;
    }

    /// <summary>手柄矩形（控件坐标）：固定在图形右下角，尺寸不随缩放变化。</summary>
    private static Rect ResizeHandleRect(double shapeWidth, double shapeHeight)
        => new(shapeWidth - ResizeHandleSize, shapeHeight - ResizeHandleSize,
               ResizeHandleSize, ResizeHandleSize);

    /// <summary>鼠标位置（控件坐标）是否落在缩放手柄上。</summary>
    private static bool IsOnResizeHandle(NodeViewModel vm, Point p)
        => ResizeHandleRect(vm.ShapeWidth, vm.ShapeHeight).Contains(p);

    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
    {
        var vm = _vm;
        if (vm is null) return null;

        var p = hitTestParameters.HitPoint;

        // 缩放手柄优先命中：该处若正好压着管路端点，也不让位，保证手柄抓得住
        if (ShowResizeHandle(vm) && IsOnResizeHandle(vm, p))
            return new PointHitTestResult(this, p);

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

    /// <summary>该画布坐标附近是否有管路端点，有则说明此处应优先由管路层处理。
    /// 相连管路经图模型索引直达；自由端不挂节点，经自由端集合单独覆盖，
    /// 避免每次命中测试全图扫描。</summary>
    private static bool HasConnectionEndpointNear(NodeViewModel node, Point canvasPoint)
    {
        var diagram = node.Diagram;
        if (diagram is null) return false;

        // 半径 12 的平方（与 PipeControl 的 12px 端点判定一致），全程不开根号
        const double radiusSquared = 12.0 * 12.0;

        bool EndpointNear(Point[] pts)
        {
            if (pts.Length < 2) return false;
            return (pts[0] - canvasPoint).LengthSquared < radiusSquared
                || (pts[^1] - canvasPoint).LengthSquared < radiusSquared;
        }

        foreach (var conn in diagram.GetConnectionsFor(node))
            if (EndpointNear(conn.Points)) return true;

        foreach (var conn in diagram.FreeEndConnections)
            if (EndpointNear(conn.Points)) return true;

        return false;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_vm is null) return;

        // 双击：仅运行模式按事件目录解析；取消已开始的拖拽意图与挂起的单击，
        // 避免双击的第二次按下残留拖拽状态、或先前单击在双击判定窗口内误触发
        if (e.ClickCount == 2 && Diagram is { IsEditable: false })
        {
            CancelPendingClick();
            _dragging = false;
            _dragMoved = true; // 让随后的 Up 不再走默认单击路径
            if (IsMouseCaptured) ReleaseMouseCapture();
            Cursor = Cursors.Arrow;

            // 画布禁用部件操作（如纯展示屏）：双击既不自切换也不上抛
            if (!AllowInteraction) return;

            var dblSpec = NodeEventSpec.Resolve(NodeDefinitionRegistry.Resolve(_vm.Kind).Events, NodeEventGesture.DoubleClick);
            if (dblSpec is not null)
            {
                // 内置手势处理：定义自消费（如 LED 双击换色）则不上抛宿主
                if (NodeDefinitionRegistry.Resolve(_vm.Kind).InvokeEvent(_vm, NodeEventGesture.DoubleClick, dblSpec.EventId))
                    return;
                RaiseEvent(new NodeClickEventArgs(_vm, NodeEventGesture.DoubleClick, dblSpec.EventId));
            }
            return;
        }

        // 点击/拖动共用起点：非编辑模式同样记录，抬起时未超阈值即视为点击
        _dragMoved = false;

        if (Diagram is not { IsEditable: true } diagram)
        {
            // 运行模式点击同样落焦点：Delete/Esc 等键盘事件经冒泡可达画布（鼠标焦点无键盘焦点视觉）
            Focus();
            return;
        }

        var canvas = GetItemsCanvas();
        if (canvas is null) return; // 可视树未就绪或不在画布内，跳过拖拽

        e.Handled = true;
        diagram.SelectedNode = _vm;
        Focus();

        // 右下角手柄：按住拖动即等比缩放（与整体移动互斥）
        if (ShowResizeHandle(_vm) && IsOnResizeHandle(_vm, e.GetPosition(this)))
        {
            _resizing = true;
            _resizeMoved = false;
            _origScale = _vm.Scale;
            _dragStartCanvas = e.GetPosition(canvas);
            CaptureMouse();
            Cursor = ResizeCursor;
            diagram.ClearAlignmentGuides();  // 缩放不参与对齐吸附
            return;
        }

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

        // 拖拽缩放（拖右下角手柄）
        if (_resizing)
        {
            if (!IsMouseCaptured || _vm is null) return;

            // 缩放中途切出编辑模式：立即终止，避免运行模式下继续改写图模型
            if (Diagram is not { IsEditable: true } resizeDiagram)
            {
                FinishResize();
                return;
            }

            var resizeCanvas = GetItemsCanvas();
            if (resizeCanvas is null) return;

            var rp = e.GetPosition(resizeCanvas);
            if (!_resizeMoved)
            {
                if (Math.Abs(rp.X - _dragStartCanvas.X) < SystemParameters.MinimumHorizontalDragDistance
                    && Math.Abs(rp.Y - _dragStartCanvas.Y) < SystemParameters.MinimumVerticalDragDistance)
                    return;
                _resizeMoved = true;
            }

            // 一次逻辑缩放只触发一次 GeometryChanged，相连管路每帧只重算一次
            resizeDiagram.ResizeNodeTo(_vm, rp.X, rp.Y);
            return;
        }

        if (!_dragging || !IsMouseCaptured || _vm is null)
        {
            // 悬停光标：手柄上提示对角缩放，其余位置恢复默认
            if (_vm is not null)
                Cursor = ShowResizeHandle(_vm) && IsOnResizeHandle(_vm, e.GetPosition(this))
                    ? ResizeCursor
                    : Cursors.Arrow;
            return;
        }

        // 拖拽中途切出编辑模式：立即终止本次拖拽，避免模式切换的中间态继续移动部件
        if (Diagram is not { IsEditable: true })
        {
            _dragging = false;
            ClearAlignmentGuides();
            if (IsMouseCaptured) ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
            return;
        }

        var canvas = GetItemsCanvas();
        if (canvas is null) return;

        var p = e.GetPosition(canvas);
        double dx = p.X - _dragStartCanvas.X;
        double dy = p.Y - _dragStartCanvas.Y;

        // 超过系统拖拽阈值前不视为拖动：避免轻微抖动触发管路重算并误吞点击
        if (!_dragMoved)
        {
            if (Math.Abs(dx) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(dy) < SystemParameters.MinimumVerticalDragDistance)
                return;
            _dragMoved = true;
        }

        double nx = _origX + dx;
        double ny = _origY + dy;
        var diagram = Diagram;
        if (diagram != null)
        {
            nx = Math.Clamp(nx, 0, Math.Max(0, diagram.CanvasWidth - _vm.ShapeWidth));
            ny = Math.Clamp(ny, 0, Math.Max(0, diagram.CanvasHeight - _vm.ShapeHeight));

            // 与其它部件对齐吸附（左/居中/右、上/居中/下）：吸附量再夹取一次，避免被推出画布
            var snap = diagram.SnapNode(_vm, nx, ny);
            nx = Math.Clamp(snap.X, 0, Math.Max(0, diagram.CanvasWidth - _vm.ShapeWidth));
            ny = Math.Clamp(snap.Y, 0, Math.Max(0, diagram.CanvasHeight - _vm.ShapeHeight));
            diagram.SetAlignmentGuides(snap.Guides);
        }
        // 一次逻辑移动只触发一次 GeometryChanged，相连管路每帧只重算一次
        _vm.MoveTo(nx, ny);
    }

    /// <summary>清除画布上的对齐参考线（拖动结束/取消时调用；图模型为空时空转）。</summary>
    private void ClearAlignmentGuides() => _vm?.Diagram?.ClearAlignmentGuides();

    /// <summary>捕获被外部夺走（如模态对话框弹出）：复位拖拽标志与光标，避免残留导致后续鼠标事件走拖拽路径。</summary>
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_resizing)
        {
            FinishResize();
            return;
        }
        if (!_dragging) return;
        _dragging = false;
        ClearAlignmentGuides();
        Cursor = Cursors.Arrow;
    }

    /// <summary>收尾缩放拖拽：提交当前缩放（Esc 回退走 <see cref="OnKeyDown"/>）。</summary>
    private void FinishResize()
    {
        _resizing = false;
        if (IsMouseCaptured) ReleaseMouseCapture();
        Cursor = Cursors.Arrow;
        InvalidateVisual();
    }

    /// <summary>Esc 取消缩放：回退到拖动前的缩放系数。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Escape || !_resizing || _vm is null) return;

        e.Handled = true;
        _vm.Scale = _origScale;
        FinishResize();
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        _isMouseOver = true;
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _isMouseOver = false;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_resizing)
        {
            FinishResize();
            return;
        }
        if (_dragging)
        {
            _dragging = false;
            ClearAlignmentGuides();
            if (IsMouseCaptured) ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
        }

        // 点击（未拖动）：仅运行模式（非编辑）上抛业务事件；编辑模式下点击只承担选中/拖拽
        // 画布禁用部件操作时同样不触发（选中仍由上方编辑分支/焦点逻辑承担）
        if (!_dragMoved && _vm is not null && Diagram is not { IsEditable: true } && AllowInteraction)
        {
            var catalog = NodeDefinitionRegistry.Resolve(_vm.Kind).Events;
            if (catalog.Count == 0)
            {
                // 未启用事件目录：默认单击语义（EventId=null），与既有行为向后兼容
                RaiseEvent(new NodeClickEventArgs(_vm));
            }
            else
            {
                // 目录非空：仅目录声明过的手势才触发
                var spec = NodeEventSpec.Resolve(catalog, NodeEventGesture.Click);
                if (spec is not null)
                {
                    // 内置手势处理：定义自消费则不上抛宿主
                    if (NodeDefinitionRegistry.Resolve(_vm.Kind).InvokeEvent(_vm, NodeEventGesture.Click, spec.EventId))
                        return;
                    var args = new NodeClickEventArgs(_vm, NodeEventGesture.Click, spec.EventId);
                    // 同时声明双击的部件：单击退避，双击将在判定窗口内取消挂起的单击
                    if (NodeEventSpec.NeedsClickBackoff(catalog))
                        DeferClick(args);
                    else
                        RaiseEvent(args);
                }
            }
        }
    }

    /// <summary>
    /// 挂起单击至双击判定窗口（系统双击间隔）之外：窗口内发生第二次按下（双击）则取消，
    /// 否则计时到点后按普通单击上抛。仅"单击+双击并存"的部件走此路径。
    /// </summary>
    private void DeferClick(NodeClickEventArgs args)
    {
        _pendingClickArgs = args;
        if (_clickBackoffTimer is null)
        {
            _clickBackoffTimer = new DispatcherTimer(DispatcherPriority.Input)
            {
                // WPF 未暴露双击时间间隔，经 Win32 读取（用户控制面板中的"双击速度"）
                Interval = TimeSpan.FromMilliseconds(GetDoubleClickTime()),
            };
            _clickBackoffTimer.Tick += FlushPendingClick;
        }
        _clickBackoffTimer.Stop();
        _clickBackoffTimer.Start();
    }

    private void FlushPendingClick(object? sender, EventArgs e)
    {
        _clickBackoffTimer?.Stop();
        var args = _pendingClickArgs;
        _pendingClickArgs = null;
        // 上下文可能在挂起期间被切换（如 DataContext 更换），只上抛仍属于当前部件的挂起事件；
        // 挂起窗口内画布关闭了部件操作（AllowNodeInteraction=false）则按取消处理
        if (args is not null && ReferenceEquals(_vm, args.Node) && AllowInteraction)
            RaiseEvent(args);
    }

    private void CancelPendingClick()
    {
        _clickBackoffTimer?.Stop();
        _pendingClickArgs = null;
    }

    /// <summary>
    /// 缓存解析宿主 Canvas；容器一旦生成其可视树祖先不会变化，
    /// 但只缓存成功解析的结果——缓存 null 会导致后续每次都重新遍历。
    /// 找不到时返回 null（而非强转非空），调用方负责在 null 时跳过拖拽。
    /// </summary>
    private Canvas? GetItemsCanvas()
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
        return null;
    }

    /// <summary>沿可视树解析宿主画布控件并缓存（同 <see cref="GetItemsCanvas"/> 模式）。</summary>
    private DiagramCanvasControl? GetHostCanvas()
    {
        if (_hostCanvas != null) return _hostCanvas;

        DependencyObject? d = this;
        while (d != null)
        {
            if (d is DiagramCanvasControl c)
            {
                _hostCanvas = c;
                return c;
            }
            d = VisualTreeHelper.GetParent(d);
        }
        return null;
    }

    /// <summary>
    /// 画布是否允许部件单击/双击操作（<see cref="DiagramCanvasControl.AllowNodeInteraction"/>）。
    /// 脱离画布使用（如单元测试直接构造）时默认允许，保持向后兼容。
    /// </summary>
    private bool AllowInteraction => GetHostCanvas()?.AllowNodeInteraction ?? true;

    private static Brush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static Pen CreatePen(byte r, byte g, byte b, double thickness)
    {
        var pen = new Pen(CreateBrush(r, g, b), thickness);
        pen.Freeze();
        return pen;
    }

    private static Pen CreateSelectionPen()
    {
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22)), 1.5);
        pen.DashStyle = DashStyles.Dash;
        pen.Freeze();
        return pen;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();
}
