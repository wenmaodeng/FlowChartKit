using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FlowChartKit.Core;
using FlowChartKit.Data;
using FlowChartKit;
using FlowChartKit.Rendering;
using FlowChartKit.ViewModels;

namespace FlowChartKit.Controls;

/// <summary>
/// 流路图画布控件：承载管路层（下层）与部件层（上层）两个 ItemsControl。
/// 内置并持有 <see cref="Diagram"/> 图模型，外部无需绑定数据源；
/// 通过 <see cref="LayoutFilePath"/> 指定序列化文件（未指定则用默认路径），加载时自动读取。
/// 全部操作走右键菜单（菜单构建见 <see cref="CanvasContextMenuBuilder"/>）；
/// 布局读写走 <see cref="LayoutStore"/>，可注入自定义存储；
/// 与宿主的解耦（设置流向请求）见 DiagramCanvasControl.DirectionBridge.cs。
/// </summary>
public partial class DiagramCanvasControl : UserControl
{
    /// <summary>画布默认底色（原 XAML 硬编码值 #FDFEFF）；冻结后作为依赖属性默认值可跨实例共享。</summary>
    private static readonly Brush DefaultCanvasPaper;

    static DiagramCanvasControl()
    {
        var paper = new SolidColorBrush(Color.FromRgb(0xFD, 0xFE, 0xFF));
        paper.Freeze();
        DefaultCanvasPaper = paper;
        // Background 语义：画布底色。内部 Canvas 绑定到此属性，宿主设 Background 即刻生效
        BackgroundProperty.OverrideMetadata(typeof(DiagramCanvasControl),
            new FrameworkPropertyMetadata(DefaultCanvasPaper));
    }

    private static readonly DependencyPropertyKey DiagramPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Diagram), typeof(DiagramViewModel), typeof(DiagramCanvasControl),
        new PropertyMetadata(null));

    public static readonly DependencyProperty DiagramProperty = DiagramPropertyKey.DependencyProperty;

    public static readonly DependencyProperty IsEditableProperty = DependencyProperty.Register(
        nameof(IsEditable), typeof(bool), typeof(DiagramCanvasControl),
        new FrameworkPropertyMetadata(true,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnIsEditableChanged));

    public static readonly DependencyProperty LayoutFilePathProperty = DependencyProperty.Register(
        nameof(LayoutFilePath), typeof(string), typeof(DiagramCanvasControl),
        new PropertyMetadata(null, OnLayoutFilePathChanged));

    public static readonly DependencyProperty PendingNodeKindProperty = DependencyProperty.Register(
        nameof(PendingNodeKind), typeof(NodeKind?), typeof(DiagramCanvasControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPendingNodeKindChanged));

    public static readonly DependencyProperty NodePlacedCommandProperty = DependencyProperty.Register(
        nameof(NodePlacedCommand), typeof(ICommand), typeof(DiagramCanvasControl),
        new PropertyMetadata(null));

    public static readonly DependencyProperty AllowContextMenuProperty = DependencyProperty.Register(
        nameof(AllowContextMenu), typeof(bool), typeof(DiagramCanvasControl),
        new PropertyMetadata(true));

    public static readonly DependencyProperty ClearLayoutRequiresConfirmProperty = DependencyProperty.Register(
        nameof(ClearLayoutRequiresConfirm), typeof(bool), typeof(DiagramCanvasControl),
        new PropertyMetadata(true));

    public static readonly DependencyProperty AutoFitCanvasProperty = DependencyProperty.Register(
        nameof(AutoFitCanvas), typeof(bool), typeof(DiagramCanvasControl),
        new PropertyMetadata(true, OnAutoFitCanvasChanged));

    public static readonly DependencyProperty DefaultPipeKindProperty = DependencyProperty.Register(
        nameof(DefaultPipeKind), typeof(PipeKind), typeof(DiagramCanvasControl),
        new PropertyMetadata(PipeKind.Standard));

    public static readonly DependencyProperty AlignmentSnapEnabledProperty = DependencyProperty.Register(
        nameof(AlignmentSnapEnabled), typeof(bool), typeof(DiagramCanvasControl),
        new PropertyMetadata(true, OnAlignmentSnapEnabledChanged));

    public static readonly DependencyProperty AlignmentSnapDistanceProperty = DependencyProperty.Register(
        nameof(AlignmentSnapDistance), typeof(double), typeof(DiagramCanvasControl),
        new PropertyMetadata(6.0, OnAlignmentSnapDistanceChanged));

    private bool _layoutLoaded;
    private bool _initialLoadDone;
    /// <summary>自适应收敛执行中标记：Resize 引发的 CanvasWidth/Height 通知会再次触发收敛，防重入。</summary>
    private bool _autoFitting;

    /// <summary>最近一次右键的画布坐标，供菜单里的"添加"操作定位。</summary>
    private Point _menuPosition;

    /// <summary>布局读写存储；默认 JSON 文件，宿主可注入自定义实现。</summary>
    public IDiagramLayoutStore LayoutStore { get; set; } = new JsonDiagramLayoutStore();

    /// <summary>
    /// 是否允许右键菜单（默认 true）。设为 false 后任何情况下（编辑/运行模式、有无选中）都不弹出右键菜单，
    /// 用于现场软件锁定布局、杜绝运行过程中的编辑行为；右键点选高亮不受影响。
    /// </summary>
    public bool AllowContextMenu
    {
        get => (bool)GetValue(AllowContextMenuProperty);
        set => SetValue(AllowContextMenuProperty, value);
    }

    /// <summary>
    /// 清空布局前是否弹出确认对话框（默认 true）。清空为破坏性操作且不可恢复，
    /// 默认要求确认以防现场误触；宿主如需一键清空可置为 false。
    /// </summary>
    public bool ClearLayoutRequiresConfirm
    {
        get => (bool)GetValue(ClearLayoutRequiresConfirmProperty);
        set => SetValue(ClearLayoutRequiresConfirmProperty, value);
    }

    public DiagramCanvasControl()
    {
        // 先创建内置图模型，再加载 XAML，保证内部绑定首次求值即可拿到实例
        var diagram = new DiagramViewModel();
        SetValue(DiagramPropertyKey, diagram);
        // 直接改 Diagram.IsEditable 时回写到依赖属性，保证外部绑定同步
        diagram.PropertyChanged += OnDiagramPropertyChanged;
        // 接住子级 FlowNodeControl 冒泡的部件点击，统一经 CLR 事件/命令桥对外（见 ClickBridge）
        HookNodeClick();
        // 键盘事件由获得焦点的部件/管路控件冒泡至此（见 OnCanvasKeyDown）
        KeyDown += OnCanvasKeyDown;
        // 自适应模式下随宿主大小变化收敛画布尺寸
        SizeChanged += OnControlSizeChanged;
        SaveLayoutCommand = new RelayCommand(_ => SaveLayout());
        AddNodeCommand = new RelayCommand(AddNode);
        AddConnectionCommand = new RelayCommand(AddConnection);
        ShowPropertiesCommand = new RelayCommand(_ => ShowProperties());
        ClearLayoutCommand = new RelayCommand(_ => ClearLayout());
        InitializeComponent();
        // DP 默认值不触发回调，初始状态需显式应用一次（默认开启自适应）
        ApplyScrollBarVisibility();
        // 外部设置的 LayoutFilePath 在构造之后才生效，故统一在 Loaded 时首次载入
        Loaded += OnLoaded;
    }

    /// <summary>内置流路图视图模型（控件自建并持有，外部只读）。</summary>
    public DiagramViewModel Diagram => (DiagramViewModel)GetValue(DiagramProperty);

    /// <summary>
    /// 编辑模式：true 允许拖动部件与管路端点，并显示端口与管路编号；false 为只读展示。
    /// 右键菜单始终可用，非编辑模式下正是靠菜单里的「编辑模式」勾选项切回来。
    /// 默认 true，可在 XAML 或代码中设置，也支持双向绑定。
    /// </summary>
    public bool IsEditable
    {
        get => (bool)GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }

    /// <summary>
    /// 画布自适应宿主大小：**默认 true**，画布逻辑尺寸自动收敛为控件可视区大小（隐藏滚动条，
    /// 越界部件与自由端被钳位回画布内，存档里的 Width/Height 不再决定显示范围）。
    /// 设为 false 可恢复"按存档尺寸显示、超出部分滚动查看"的行为。
    /// </summary>
    public bool AutoFitCanvas
    {
        get => (bool)GetValue(AutoFitCanvasProperty);
        set => SetValue(AutoFitCanvasProperty, value);
    }

    /// <summary>
    /// 新建管路默认使用的管路类型（默认 <see cref="PipeKind.Standard"/> 内置普通管路）。
    /// 设为宿主自定义的管路类型常量后，右键菜单"添加管路（默认）"与不带参数的添加操作都用它；
    /// 单条管路的类型可随时在属性对话框中切换。
    /// </summary>
    public PipeKind DefaultPipeKind
    {
        get => (PipeKind)GetValue(DefaultPipeKindProperty);
        set => SetValue(DefaultPipeKindProperty, value);
    }

    public static readonly DependencyProperty DefaultPipeRoutingProperty = DependencyProperty.Register(
        nameof(DefaultPipeRouting), typeof(PipeRouting), typeof(DiagramCanvasControl),
        new PropertyMetadata(PipeRouting.Orthogonal));

    /// <summary>
    /// 新建管路默认使用的连线布局（默认 <see cref="PipeRouting.Orthogonal"/> 垂直水平）。
    /// 单条管路的布局可随时在属性对话框中切换。
    /// </summary>
    public PipeRouting DefaultPipeRouting
    {
        get => (PipeRouting)GetValue(DefaultPipeRoutingProperty);
        set => SetValue(DefaultPipeRoutingProperty, value);
    }

    /// <summary>
    /// 拖动部件时是否启用对齐吸附（**默认 true**）：靠近其它部件的左/右/居中、上/下/居中时
    /// 自动吸附并显示捕捉线。设为 false 恢复为自由拖动。
    /// </summary>
    public bool AlignmentSnapEnabled
    {
        get => (bool)GetValue(AlignmentSnapEnabledProperty);
        set => SetValue(AlignmentSnapEnabledProperty, value);
    }

    /// <summary>
    /// 对齐吸附的触发距离（px，**默认 6**）：部件边或中线进入该距离内即吸附对齐。
    /// </summary>
    public double AlignmentSnapDistance
    {
        get => (double)GetValue(AlignmentSnapDistanceProperty);
        set => SetValue(AlignmentSnapDistanceProperty, value);
    }

    private static void OnAlignmentSnapEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DiagramCanvasControl { Diagram: { } diagram })
        {
            diagram.AlignmentSnapEnabled = (bool)e.NewValue;
            if (!(bool)e.NewValue) diagram.ClearAlignmentGuides();
        }
    }

    private static void OnAlignmentSnapDistanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DiagramCanvasControl { Diagram: { } diagram })
            diagram.AlignmentSnapDistance = (double)e.NewValue;
    }

    /// <summary>布局序列化文件路径；未指定（null 或空白）时使用 <see cref="DiagramSerializer.DefaultFilePath"/>。</summary>
    public string? LayoutFilePath
    {
        get => (string?)GetValue(LayoutFilePathProperty);
        set => SetValue(LayoutFilePathProperty, value);
    }

    /// <summary>实际使用的序列化路径。</summary>
    private string EffectiveLayoutFilePath
        => string.IsNullOrWhiteSpace(LayoutFilePath) ? DiagramSerializer.DefaultFilePath : LayoutFilePath;

    /// <summary>在当前右键位置添加指定类型的部件（右键菜单）。</summary>
    public ICommand AddNodeCommand { get; }

    /// <summary>在当前右键位置添加一条自由管路（右键菜单）。</summary>
    public ICommand AddConnectionCommand { get; }

    /// <summary>保存当前布局到 <see cref="EffectiveLayoutFilePath"/>（右键菜单）。</summary>
    public ICommand SaveLayoutCommand { get; }

    /// <summary>弹出属性窗口，显示当前选中部件或管路的属性（右键菜单）。</summary>
    public ICommand ShowPropertiesCommand { get; }

    /// <summary>清空画布上所有部件与管路（右键菜单）。</summary>
    public ICommand ClearLayoutCommand { get; }

    /// <summary>操作结果的提示文本，供宿主状态栏显示。</summary>
    public event Action<string>? StatusChanged;

    /// <summary>待放置的部件类型；非 null 时点击画布放置该部件，放置后自动清空，Esc 可取消。</summary>
    public NodeKind? PendingNodeKind
    {
        get => (NodeKind?)GetValue(PendingNodeKindProperty);
        set => SetValue(PendingNodeKindProperty, value);
    }

    /// <summary>部件放置完成后执行，参数为新建的 <see cref="NodeViewModel"/>。</summary>
    public ICommand? NodePlacedCommand
    {
        get => (ICommand?)GetValue(NodePlacedCommandProperty);
        set => SetValue(NodePlacedCommandProperty, value);
    }

    // async void 必须 try-catch 覆盖全部代码，否则 SynchronizationContext 捕获到未观察异常会崩溃
    // (WPF 中默认触发 DispatcherUnhandledException)。
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // 供宿主 VM 以 OneWayToSource 绑定取走后调用（见 DirectionBridge / StateBridge）。
            // 必须在 Loaded（绑定已附着）后赋值：构造期写入的本地值会被随后附着的绑定覆盖，
            // 这里赋值会作为目标值变更推送到 VM 侧属性。
            SetValue(SetDirectionByNumberCommandProperty, new RelayCommand(ExecuteSetDirection));
            SetValue(SetNodeStateByNumberCommandProperty, new RelayCommand(ExecuteSetNodeState));

            _layoutLoaded = true;
            // Loaded 会随控件重新并入可视树而多次触发；布局只在首次载入，
            // 否则每次重入都会经 Clear() 丢弃用户未保存的内存编辑
            if (_initialLoadDone) return;
            _initialLoadDone = true;
            await LoadLayoutAsync();
        }
        catch { /* async void 不允许未观察异常逃逸；初始化失败时保持当前状态 */ }
    }

    private static async void OnLayoutFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        try
        {
            if (d is DiagramCanvasControl control && control._layoutLoaded)
                await control.LoadLayoutAsync();
        }
        catch { /* 载入失败由 LoadLayoutAsync 内部处理，此处吞掉 async void 的未观察异常 */ }
    }

    private static void OnPendingNodeKindChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // 进入放置模式即落焦点：键盘事件经焦点元素冒泡到画布，Esc 取消才可达
        if (d is DiagramCanvasControl control && e.NewValue is not null)
            control.Focus();
    }

    /// <summary>异步载入序列化文件，避免阻塞 UI 线程；文件不存在、解析失败或部件类型不可解析
    /// （由 DiagramViewModel.Load 预校验拒绝，未知类型抛异常但不会 Clear）时保留当前图内容。</summary>
    private async Task LoadLayoutAsync()
    {
        try
        {
            var data = await LayoutStore.LoadAsync(EffectiveLayoutFilePath);
            if (data is null) return;
            Diagram.Load(data);
        }
        catch
        {
            // 载入失败（含未知部件类型）：保留载入前的图内容，宿主可通过"保存布局"重建文件
        }
    }

    // ---- 键盘操作 ----

    /// <summary>画布键盘操作：Delete 删除选中项（与右键菜单一致）、Esc 取消部件放置模式、
    /// 方向键微调选中部件。键盘事件由获得焦点的部件/管路控件冒泡至此，"选中即落焦点"是可达前提。</summary>
    private void OnCanvasKeyDown(object sender, KeyEventArgs e)
    {
        var diagram = Diagram;
        switch (e.Key)
        {
            case Key.Delete:
                var command = diagram.DeleteSelectedCommand;
                if (command.CanExecute(null))
                {
                    command.Execute(null);
                    e.Handled = true;
                }
                break;

            case Key.Escape:
                if (PendingNodeKind is not null)
                {
                    PendingNodeKind = null;
                    StatusChanged?.Invoke("已取消部件放置");
                    e.Handled = true;
                }
                break;

            case Key.Left or Key.Right or Key.Up or Key.Down:
                if (diagram.IsEditable && diagram.SelectedNode is { } node)
                {
                    NudgeNode(node, e.Key);
                    e.Handled = true;
                }
                break;
        }
    }

    /// <summary>方向键微调选中部件：1px，按住 Shift 为 10px；坐标钳位到画布内（与其他放置/拖拽入口一致）。</summary>
    private void NudgeNode(NodeViewModel node, Key key)
    {
        double step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
        double dx = key switch { Key.Left => -step, Key.Right => step, _ => 0 };
        double dy = key switch { Key.Up => -step, Key.Down => step, _ => 0 };
        node.MoveTo(
            Math.Clamp(node.X + dx, 0, Math.Max(0, Diagram.CanvasWidth - node.ShapeWidth)),
            Math.Clamp(node.Y + dy, 0, Math.Max(0, Diagram.CanvasHeight - node.ShapeHeight)));
    }

    private async void SaveLayout()
    {
        try
        {
            var path = EffectiveLayoutFilePath;
            await LayoutStore.SaveAsync(Diagram.ToData(), path);
            StatusChanged?.Invoke($"已保存：{path}");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"保存失败：{ex.Message}");
        }
    }

    private void ClearLayout()
    {
        // 破坏性操作确认：清空不可恢复，默认弹窗防误触（宿主可经 ClearLayoutRequiresConfirm 关闭）
        if (ClearLayoutRequiresConfirm)
        {
            const string message = "确定要清空画布上的所有部件与管路吗？";
            var result = Window.GetWindow(this) is { } owner
                ? MessageBox.Show(owner, message, "清空布局", MessageBoxButton.OKCancel, MessageBoxImage.Warning)
                : MessageBox.Show(message, "清空布局", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (result != MessageBoxResult.OK)
            {
                StatusChanged?.Invoke("已取消清空布局");
                return;
            }
        }
        Diagram.Clear();
        StatusChanged?.Invoke("已清空布局");
    }

    private void AddNode(object? parameter)
    {
        if (parameter is not NodeKind kind) return;

        var pos = ClampPlacement(kind, _menuPosition);
        var node = Diagram.AddNode(kind, pos.X, pos.Y);
        Diagram.SelectedNode = node;
        StatusChanged?.Invoke($"已添加部件：{node.Label}");
    }

    /// <summary>放置坐标按部件占位（新建部件 Scale=1，取基准尺寸）钳位到画布内，避免新部件初始即越界。</summary>
    private Point ClampPlacement(NodeKind kind, Point pos)
    {
        var (w, h) = NodeDefinitionRegistry.Resolve(kind).BaseSize;
        return new Point(
            Math.Clamp(pos.X, 0, Math.Max(0, Diagram.CanvasWidth - w)),
            Math.Clamp(pos.Y, 0, Math.Max(0, Diagram.CanvasHeight - h)));
    }

    /// <summary>
    /// 添加管路：命令参数为 <see cref="PipeKind"/> 时按该类型创建，否则用 <see cref="DefaultPipeKind"/>。
    /// 右键菜单的"添加管路"子菜单按注册表列出可选管路类型。
    /// </summary>
    private void AddConnection(object? parameter)
    {
        var kind = parameter is PipeKind k ? k : DefaultPipeKind;
        // 宿主未注册该类型时回退内置普通管路，避免创建后无法解析外观
        if (!PipeDefinitionRegistry.IsAvailable(kind)) kind = PipeKind.Standard;

        var start = _menuPosition;
        // 自由端初始坐标同样钳位到画布内，避免落在画布外无法拾取
        var end = new Point(Math.Min(start.X + 120, Diagram.CanvasWidth), start.Y);
        var conn = Diagram.AddFreeConnection(start, end, pipeKind: kind, routing: DefaultPipeRouting);
        Diagram.SelectedConnection = conn;

        var name = PipeDefinitionRegistry.Resolve(kind).DisplayName;
        StatusChanged?.Invoke($"已添加管路（{name}），拖动两端端点吸附到部件端口");
    }

    /// <summary>弹出当前选中对象的属性窗口（管路优先于部件）。</summary>
    private void ShowProperties()
    {
        object? target = Diagram.SelectedConnection ?? (object?)Diagram.SelectedNode;
        if (target is null) return;

        PropertiesDialog.Show(Window.GetWindow(this), target);
    }

    /// <summary>编辑模式下写到图模型。</summary>
    private static void OnIsEditableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DiagramCanvasControl control)
        {
            control.Diagram.IsEditable = (bool)e.NewValue;
            // 退出编辑模式时清掉可能残留的对齐参考线
            if (!(bool)e.NewValue) control.Diagram.ClearAlignmentGuides();
        }
    }

    /// <summary>图模型侧改编辑模式时回写到依赖属性；SetCurrentValue 不会覆盖外部绑定。</summary>
    private void OnDiagramPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 自适应模式：载入存档会按存档值覆盖画布尺寸，需立即拉回控件实际大小
        if (e.PropertyName is nameof(DiagramViewModel.CanvasWidth) or nameof(DiagramViewModel.CanvasHeight))
        {
            ApplyAutoFit();
            return;
        }
        if (e.PropertyName != nameof(DiagramViewModel.IsEditable)) return;
        if (IsEditable != Diagram.IsEditable)
            SetCurrentValue(IsEditableProperty, Diagram.IsEditable);
    }

    // ---- 画布自适应宿主大小（AutoFitCanvas） ----

    private static void OnAutoFitCanvasChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (DiagramCanvasControl)d;
        control.ApplyScrollBarVisibility();
        control.ApplyAutoFit();
    }

    /// <summary>自适应开启时隐藏滚动条（画布恒等于可视区，无需滚动）；关闭时恢复按需滚动。</summary>
    private void ApplyScrollBarVisibility()
    {
        var visibility = AutoFitCanvas ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Auto;
        CanvasScroller.HorizontalScrollBarVisibility = visibility;
        CanvasScroller.VerticalScrollBarVisibility = visibility;
    }

    private void OnControlSizeChanged(object sender, SizeChangedEventArgs e) => ApplyAutoFit();

    /// <summary>自适应模式下把画布逻辑尺寸收敛为控件可视区大小（越界部件/自由端被钳位，消除滚动条）。</summary>
    private void ApplyAutoFit()
    {
        if (!AutoFitCanvas || _autoFitting) return;
        var diagram = Diagram;
        if (diagram is null) return;

        // 布局早期 Viewport 可能尚未更新，回退到控件实际尺寸
        var width = CanvasScroller.ViewportWidth;
        var height = CanvasScroller.ViewportHeight;
        if (width <= 0 || height <= 0) { width = ActualWidth; height = ActualHeight; }
        if (width <= 0 || height <= 0) return;

        _autoFitting = true;
        try { diagram.Resize(width, height); }
        finally { _autoFitting = false; }
    }

    /// <summary>右键按下：记录位置并按命中结果更新选中状态。</summary>
    private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 菜单始终可用（含「编辑模式」勾选项），所以选中要先同步，菜单才能作用于正确对象
        _menuPosition = e.GetPosition(DiagramCanvas);

        var diagram = Diagram;
        var hit = VisualTreeHelper.HitTest(this, e.GetPosition(this))?.VisualHit;

        // 部件层在管路层之上，优先判定
        if (FindVisualAncestor<FlowNodeControl>(hit)?.DataContext is NodeViewModel node)
        {
            diagram.SelectedNode = node;
            return;
        }

        if (FindVisualAncestor<PipeControl>(hit)?.DataContext is ConnectionViewModel connection)
        {
            diagram.SelectedConnection = connection;
            return;
        }

        diagram.SelectedNode = null;
        diagram.SelectedConnection = null;
    }

    /// <summary>菜单打开前按当前选中状态重建菜单项（构建逻辑见 <see cref="CanvasContextMenuBuilder"/>）；
    /// <see cref="AllowContextMenu"/> 为 false 时直接吞掉事件，任何情况下不弹菜单。</summary>
    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (!AllowContextMenu)
        {
            e.Handled = true;
            CanvasMenu.PlacementTarget = null;
            CanvasMenu.IsOpen = false;
            return;
        }
        CanvasContextMenuBuilder.Build(CanvasMenu, this);
    }

    private static T? FindVisualAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d != null)
        {
            if (d is T target) return target;
            d = VisualTreeHelper.GetParent(d);
        }
        return null;
    }

    /// <summary>画布点击：若处于放置模式则创建部件，否则清除选中。</summary>
    private void DiagramCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var diagram = Diagram;

        if (PendingNodeKind is NodeKind kind)
        {
            var pos = ClampPlacement(kind, e.GetPosition(DiagramCanvas));
            var node = diagram.AddNode(kind, pos.X, pos.Y);
            PendingNodeKind = null;

            var command = NodePlacedCommand;
            if (command != null && command.CanExecute(node))
                command.Execute(node);

            e.Handled = true;
            return;
        }

        // 点击空白处清除选中
        diagram.SelectedNode = null;
        diagram.SelectedConnection = null;
    }
}
