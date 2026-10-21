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
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty NodePlacedCommandProperty = DependencyProperty.Register(
        nameof(NodePlacedCommand), typeof(ICommand), typeof(DiagramCanvasControl),
        new PropertyMetadata(null));

    private bool _layoutLoaded;
    private bool _initialLoadDone;

    /// <summary>最近一次右键的画布坐标，供菜单里的"添加"操作定位。</summary>
    private Point _menuPosition;

    /// <summary>布局读写存储；默认 JSON 文件，宿主可注入自定义实现。</summary>
    public IDiagramLayoutStore LayoutStore { get; set; } = new JsonDiagramLayoutStore();

    public DiagramCanvasControl()
    {
        // 先创建内置图模型，再加载 XAML，保证内部绑定首次求值即可拿到实例
        var diagram = new DiagramViewModel();
        SetValue(DiagramPropertyKey, diagram);
        // 直接改 Diagram.IsEditable 时回写到依赖属性，保证外部绑定同步
        diagram.PropertyChanged += OnDiagramPropertyChanged;
        // 接住子级 FlowNodeControl 冒泡的部件点击，统一经 CLR 事件/命令桥对外（见 ClickBridge）
        HookNodeClick();
        SaveLayoutCommand = new RelayCommand(_ => SaveLayout());
        AddNodeCommand = new RelayCommand(AddNode);
        AddConnectionCommand = new RelayCommand(_ => AddConnection());
        ShowPropertiesCommand = new RelayCommand(_ => ShowProperties());
        ClearLayoutCommand = new RelayCommand(_ => ClearLayout());
        InitializeComponent();
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

    /// <summary>待放置的部件类型；非 null 时点击画布放置该部件，放置后自动清空。</summary>
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

    /// <summary>异步载入序列化文件，避免阻塞 UI 线程；文件不存在、解析失败或部件类型不可解析时保留当前图内容。</summary>
    private async Task LoadLayoutAsync()
    {
        try
        {
            var data = await LayoutStore.LoadAsync(EffectiveLayoutFilePath);
            if (data is null) return;

            // 先校验全部部件类型可解析，再整体载入：
            // Diagram.Load 会先 Clear，若中途因未知 NodeKind 抛异常会留下"半载入"画布
            foreach (var nd in data.Nodes)
                NodeDefinitionRegistry.Resolve(nd.Kind);

            Diagram.Load(data);
        }
        catch
        {
            // 载入失败：保留载入前的图内容，宿主可通过"保存布局"重建文件
        }
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

    private void AddConnection()
    {
        var start = _menuPosition;
        // 自由端初始坐标同样钳位到画布内，避免落在画布外无法拾取
        var end = new Point(Math.Min(start.X + 120, Diagram.CanvasWidth), start.Y);
        var conn = Diagram.AddFreeConnection(start, end);
        Diagram.SelectedConnection = conn;
        StatusChanged?.Invoke("已添加管路，拖动两端端点吸附到部件端口");
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
            control.Diagram.IsEditable = (bool)e.NewValue;
    }

    /// <summary>图模型侧改编辑模式时回写到依赖属性；SetCurrentValue 不会覆盖外部绑定。</summary>
    private void OnDiagramPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DiagramViewModel.IsEditable)) return;
        if (IsEditable != Diagram.IsEditable)
            SetCurrentValue(IsEditableProperty, Diagram.IsEditable);
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

    /// <summary>菜单打开前按当前选中状态重建菜单项（构建逻辑见 <see cref="CanvasContextMenuBuilder"/>）。</summary>
    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
        => CanvasContextMenuBuilder.Build(CanvasMenu, this);

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
