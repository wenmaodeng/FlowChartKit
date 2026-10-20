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
using FlowChartKit.ViewModels;

namespace FlowChartKit.Controls;

/// <summary>
/// 流路图画布控件：承载管路层（下层）与部件层（上层）两个 ItemsControl。
/// 内置并持有 <see cref="Diagram"/> 图模型，外部无需绑定数据源；
/// 通过 <see cref="LayoutFilePath"/> 指定序列化文件（未指定则用默认路径），加载时自动读取。
/// 全部操作走右键菜单：未选中时可添加部件/管路；选中管路后可设正向/反向/停止/删除；
/// 选中部件后可删除；始终提供编辑模式、保存布局、恢复示例。
/// 与宿主的解耦方式：宿主 ViewModel 实现 <see cref="IFlowDirectionRequester"/> 并触发
/// <see cref="IFlowDirectionRequester.SetDirectionRequested"/> 事件，本控件订阅后调用
/// <see cref="SetConnectionDirection"/> 落到图模型——VM 完全不引用本控件类型。
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

    /// <summary>最近一次右键的画布坐标，供菜单里的"添加"操作定位。</summary>
    private Point _menuPosition;

    /// <summary>当前订阅了设置流向事件的宿主请求方（DataContext 实现 <see cref="IFlowDirectionRequester"/> 时）。</summary>
    private IFlowDirectionRequester? _requester;

    public DiagramCanvasControl()
    {
        // 先创建内置图模型，再加载 XAML，保证内部绑定首次求值即可拿到实例
        var diagram = new DiagramViewModel();
        SetValue(DiagramPropertyKey, diagram);
        // 直接改 Diagram.IsEditable 时回写到依赖属性，保证外部绑定同步
        diagram.PropertyChanged += OnDiagramPropertyChanged;
        SaveLayoutCommand = new RelayCommand(_ => SaveLayout());
        ResetDemoCommand = new RelayCommand(_ => ResetToDemo());
        AddNodeCommand = new RelayCommand(AddNode);
        AddConnectionCommand = new RelayCommand(_ => AddConnection());
        ShowPropertiesCommand = new RelayCommand(_ => ShowProperties());
        InitializeComponent();
        // 外部设置的 LayoutFilePath 在构造之后才生效，故统一在 Loaded 时首次载入
        Loaded += OnLoaded;
        // 订阅宿主 VM 的"设置流向"请求事件；VM 不引用控件，靠此事件桥接
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>内置流路图视图模型（控件自建并持有，外部只读）。</summary>
    public DiagramViewModel Diagram => (DiagramViewModel)GetValue(DiagramProperty);

    /// <summary>
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

    /// <summary>重建内置示例布局（右键菜单）。</summary>
    public ICommand ResetDemoCommand { get; }

    /// <summary>弹出属性窗口，显示当前选中部件或管路的属性（右键菜单）。</summary>
    public ICommand ShowPropertiesCommand { get; }

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

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _layoutLoaded = true;
        LoadLayout();
    }

    private static void OnLayoutFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DiagramCanvasControl control && control._layoutLoaded)
            control.LoadLayout();
    }

    /// <summary>载入序列化文件；文件不存在或解析失败时回退到示例布局。</summary>
    private void LoadLayout()
    {
        try
        {
            var data = DiagramSerializer.Load(EffectiveLayoutFilePath);
            if (data is null) BuildDemo();
            else Diagram.Load(data);
        }
        catch
        {
            BuildDemo();
        }
    }

    private void SaveLayout()
    {
        try
        {
            var path = EffectiveLayoutFilePath;
            DiagramSerializer.Save(Diagram.ToData(), path);
            StatusChanged?.Invoke($"已保存：{path}");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"保存失败：{ex.Message}");
        }
    }

    private void ResetToDemo()
    {
        BuildDemo();
        StatusChanged?.Invoke("已恢复示例布局");
    }

    private void BuildDemo()
    {
        Diagram.Clear();
        DemoLayout.Build(Diagram);
    }

    private void AddNode(object? parameter)
    {
        if (parameter is not NodeKind kind) return;

        var node = Diagram.AddNode(kind, _menuPosition.X, _menuPosition.Y);
        Diagram.SelectedNode = node;
        StatusChanged?.Invoke($"已添加部件：{node.Label}");
    }

    private void AddConnection()
    {
        var start = _menuPosition;
        var conn = Diagram.AddFreeConnection(start, new Point(start.X + 120, start.Y));
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

    /// <summary>
    /// 按管路编号设置流向。由 <see cref="OnSetDirectionRequested"/> 在收到 VM 的
    /// <see cref="IFlowDirectionRequester.SetDirectionRequested"/> 事件时调用，也可被宿主代码直接调用。
    /// 编号与画布上的编号徽章一致，从 1 开始。
    /// </summary>
    /// <returns>找到并设置了该编号的管路时为 true；编号不存在时为 false。</returns>
    public bool SetConnectionDirection(int number, FlowMode direction)
    {
        var connection = Diagram.Connections.FirstOrDefault(c => c.Number == number);
        if (connection is null)
        {
            StatusChanged?.Invoke($"未找到编号为 {number} 的管路");
            return false;
        }

        connection.Direction = direction;
        StatusChanged?.Invoke($"管路 #{number} 已设为{FlowModeDefaults.DisplayName(direction)}");
        return true;
    }

    /// <summary>DataContext 变为实现 <see cref="IFlowDirectionRequester"/> 的 VM 时订阅其请求事件；切换时先退订旧 VM。</summary>
    private void OnDataContextChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        if (_requester is not null)
            _requester.SetDirectionRequested -= OnSetDirectionRequested;
        _requester = e.NewValue as IFlowDirectionRequester;
        if (_requester is not null)
            _requester.SetDirectionRequested += OnSetDirectionRequested;
    }

    /// <summary>响应 VM 的设置流向请求，落到图模型并把结果回填到事件参数。</summary>
    private void OnSetDirectionRequested(object? sender, SetDirectionEventArgs e)
    {
        e.Result = SetConnectionDirection(e.Number, e.Direction);
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

    /// <summary>菜单打开前按当前选中状态重建菜单项。</summary>
    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var diagram = Diagram;
        var menu = CanvasMenu;
        menu.Items.Clear();

        if (diagram.SelectedConnection is not null)
        {
            menu.Items.Add(NewItem(FlowModeDefaults.DisplayName(FlowMode.Forward), diagram.SetDirectionCommand, FlowMode.Forward));
            menu.Items.Add(NewItem(FlowModeDefaults.DisplayName(FlowMode.Reverse), diagram.SetDirectionCommand, FlowMode.Reverse));
            menu.Items.Add(NewItem(FlowModeDefaults.DisplayName(FlowMode.None), diagram.SetDirectionCommand, FlowMode.None));
            menu.Items.Add(new Separator());
            menu.Items.Add(NewItem("属性", ShowPropertiesCommand, null));
            menu.Items.Add(NewItem("删除管路", diagram.DeleteSelectedCommand, null));
        }
        else if (diagram.SelectedNode is not null)
        {
            menu.Items.Add(NewItem("属性", ShowPropertiesCommand, null));
            menu.Items.Add(NewItem("删除部件", diagram.DeleteSelectedCommand, null));
        }
        else
        {
            var addNode = new MenuItem { Header = "添加部件" };
            foreach (var kind in Enum.GetValues<NodeKind>())
                addNode.Items.Add(NewItem(NodeKindDefaults.DisplayName(kind), AddNodeCommand, kind));
            menu.Items.Add(addNode);
            menu.Items.Add(NewItem("添加管路", AddConnectionCommand, null));
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(NewEditableItem());
        menu.Items.Add(NewItem("保存布局", SaveLayoutCommand, null));
        menu.Items.Add(NewItem("恢复示例", ResetDemoCommand, null));
    }

    private static MenuItem NewItem(string header, ICommand command, object? parameter)
        => new() { Header = header, Command = command, CommandParameter = parameter };

    private MenuItem NewEditableItem()
    {
        // 走依赖属性而非直接改 Diagram，保证外部绑定与图模型始终一致
        var item = new MenuItem { Header = "编辑模式", IsCheckable = true, IsChecked = IsEditable };
        item.Click += (_, _) => IsEditable = item.IsChecked == true;
        return item;
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
            var pos = e.GetPosition(DiagramCanvas);
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
