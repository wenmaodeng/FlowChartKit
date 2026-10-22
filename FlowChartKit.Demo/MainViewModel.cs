using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using FlowChartKit;
using FlowChartKit.Core;
using FlowChartKit.Demo.Nodes;
using FlowChartKit.Demo.Pipes;
using FlowChartKit.Rendering;
using FlowChartKit.ViewModels;

namespace FlowChartKit.Demo;

/// <summary>方向下拉框的条目：显示名 + 实际枚举值。</summary>
public sealed record FlowModeItem(FlowMode Value, string Text);

/// <summary>部件类型下拉框的条目：显示名 + 实际枚举值。</summary>
public sealed record NodeKindItem(NodeKind Value, string Text);

/// <summary>
/// 宿主 ViewModel：完全不认识画布控件，只持有一个由画布控件经 OneWayToSource 绑定灌入的
/// <see cref="ICommand"/>，调用时传 <see cref="SetDirectionRequest"/> 并读取回填的 Result。
/// 这是最纯的 MVVM 分层：VM 不引用任何 View / 控件类型，可独立单元测试（用 mock command 即可）。
/// </summary>
public class MainViewModel : ObservableBase
{
    private int _number = 1;
    private FlowMode _direction = FlowMode.Forward;
    private string _statusText = "就绪";
    private ICommand? _setDirectionByNumberCommand;

    private ICommand? _setNodeStateByNumberCommand;
    /// <summary>由画布控件的同名依赖属性经 OneWayToSource 绑定灌入；未接线时为 null。</summary>
    public ICommand? SetNodeStateByNumberCommand
    {
        get => _setNodeStateByNumberCommand;
        set => Set(ref _setNodeStateByNumberCommand, value);
    }

    /// <summary>ViewModel 直接在 XAML 里创建，无需代码后置注入。</summary>
    public MainViewModel()
    {
        // 按钮可用性只判编号；成功与否由命令回填的 Result 决定，不会出现"灰按钮"掩盖真实原因
        ApplyCommand = new RelayCommand(_ => Execute(), _ => Number > 0);

        // 部件点击命令桥：宿主提供命令、画布控件在点击时执行（同 Button.Command，XAML 用默认 OneWay 绑定）；
        // 参数为 NodeClickEventArgs，按部件类型与编号分发
        NodeClickCommand = new RelayCommand(p =>
        {
            if (p is not NodeClickEventArgs args) return;
            var node = args.Node;

            // 事件目录语义分发示例：双击电磁阀切换开关状态（写回 State，属性对话框会实时刷新）
            if (args.EventId == "ToggleState")
            {
                node.State = node.State as string is "开" ? "关" : "开";
                StatusText = $"电磁阀 #{node.KindNumber} 已切换为 {node.State}";
                return;
            }

            // 事件目录启用后按 EventId 语义分发；未声明目录的部件 EventId 为 null
            var gestureText = args.EventId is null ? "" : $" [{args.EventId}]";
            StatusText = $"{(args.Gesture == NodeEventGesture.DoubleClick ? "双击" : "点击")}部件：" +
                         $"{NodeDefinitionRegistry.Resolve(node.Kind).DisplayName} #{node.KindNumber}{gestureText}" +
                         (node.State is null ? "" : $"（State={node.State}）");
        });

        // 设置 LED 状态：同一命令桥，第三个参数传 LedColorState 枚举（与 LedDefinition 的约定一致）
        ApplyLedStateCommand = new RelayCommand(_ => ExecuteSetLedState(), _ => LedNumber > 0);

        // 管路样式测试：切换选中管路类型 / 生成四种管路对比示例 / 添加无名称部件
        ApplyPipeKindCommand = new RelayCommand(_ => ApplyPipeKind(), _ => Diagram is not null);
        BuildPipeDemoCommand = new RelayCommand(_ => BuildPipeDemo(), _ => Diagram is not null);
        AddNamelessNodeCommand = new RelayCommand(_ => AddNamelessNode(), _ => Diagram is not null);

        // 连线布局测试：切换选中管路的走线方式（垂直水平 / 直线 / 弧线）
        ApplyRoutingCommand = new RelayCommand(_ => ApplyRouting(), _ => Diagram is not null);
    }

    /// <summary>由画布控件的同名依赖属性经 OneWayToSource 绑定灌入；未接线时为 null。</summary>
    public ICommand? SetDirectionByNumberCommand
    {
        get => _setDirectionByNumberCommand;
        set => Set(ref _setDirectionByNumberCommand, value);
    }

    private ICommand? _nodeClickCommand;
    /// <summary>部件点击命令（本 VM 提供并赋给画布控件，控件在点击时调用，方向与 SetDirectionByNumberCommand 相反）。</summary>
    public ICommand? NodeClickCommand
    {
        get => _nodeClickCommand;
        set => Set(ref _nodeClickCommand, value);
    }

    /// <summary>要设置流向的管路编号，从 1 开始，与画布上的编号徽章一致。</summary>
    public int Number
    {
        get => _number;
        set
        {
            if (Set(ref _number, value))
            {
                ApplyCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private int _ledNumber = 1;
    /// <summary>要设置状态的指示灯编号（同类部件序号），从 1 开始，与画布上的序号徽章一致。</summary>
    public int LedNumber
    {
        get => _ledNumber;
        set
        {
            if (Set(ref _ledNumber, value))
                ApplyLedStateCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>目标流向。</summary>
    public FlowMode Direction
    {
        get => _direction;
        set => Set(ref _direction, value);
    }

    /// <summary>下拉框可选项，显示名与画布右键菜单共用 FlowModeDefaults。</summary>
    public IReadOnlyList<FlowModeItem> Directions { get; } =
        new[] { FlowMode.Forward, FlowMode.Reverse, FlowMode.None }
            .Select(m => new FlowModeItem(m, FlowModeDefaults.DisplayName(m)))
            .ToList();

    private DiagramViewModel? _diagram;
    /// <summary>
    /// 画布的 Diagram（由窗口 Loaded 时注入）。宿主通常只经命令桥与画布交互，
    /// Demo 直接持有它以便按坐标编排示例并读取 SelectedConnection。
    /// </summary>
    public DiagramViewModel? Diagram
    {
        get => _diagram;
        set
        {
            if (!Set(ref _diagram, value)) return;
            ApplyPipeKindCommand.RaiseCanExecuteChanged();
            BuildPipeDemoCommand.RaiseCanExecuteChanged();
            AddNamelessNodeCommand.RaiseCanExecuteChanged();
            ApplyRoutingCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>管路类型下拉条目：显示名 + 类型值；内置与宿主扩展（标注「扩展」）一并列出。</summary>
    public sealed record PipeKindItem(PipeKind Value, string Text);

    /// <summary>可选管路类型：取自 <see cref="PipeDefinitionRegistry.All"/>（App 启动注册后枚举）。</summary>
    public IReadOnlyList<PipeKindItem> PipeKinds { get; } =
        PipeDefinitionRegistry.All
            .Select(d => new PipeKindItem(
                d.Kind,
                PipeDefinitionRegistry.IsBuiltIn(d.Kind) ? d.DisplayName : $"{d.DisplayName}（扩展）"))
            .ToList();

    private PipeKind _selectedPipeKind = DemoPipeKinds.Steam;
    /// <summary>要应用到选中管路的类型（测试单条管路切换类型后颜色/粗细/动画即时变化）。</summary>
    public PipeKind SelectedPipeKind
    {
        get => _selectedPipeKind;
        set => Set(ref _selectedPipeKind, value);
    }

    private PipeKind _defaultPipeKind = PipeKind.Standard;
    /// <summary>画布 <c>DefaultPipeKind</c>：新建管路（右键菜单默认项、不带参数的添加）使用的类型。</summary>
    public PipeKind DefaultPipeKind
    {
        get => _defaultPipeKind;
        set => Set(ref _defaultPipeKind, value);
    }

    /// <summary>把 <see cref="SelectedPipeKind"/> 应用到当前选中的管路。</summary>
    public RelayCommand ApplyPipeKindCommand { get; }

    /// <summary>生成四条不同类型的管路，便于直观对比颜色/粗细/流动画。</summary>
    public RelayCommand BuildPipeDemoCommand { get; }

    /// <summary>添加一个名称为空的部件，验证"名称允许为空"。</summary>
    public RelayCommand AddNamelessNodeCommand { get; }

    /// <summary>把 <see cref="SelectedRouting"/> 应用到当前选中的管路。</summary>
    public RelayCommand ApplyRoutingCommand { get; }

    /// <summary>连线布局下拉条目：显示名 + 布局值。</summary>
    public sealed record PipeRoutingItem(PipeRouting Value, string Text);

    /// <summary>可选连线布局：垂直水平（可拖中间线段）/ 直线 / 弧线。</summary>
    public IReadOnlyList<PipeRoutingItem> Routings { get; } =
        new[] { PipeRouting.Orthogonal, PipeRouting.Straight, PipeRouting.Curved }
            .Select(r => new PipeRoutingItem(r, PipeRoutingDefaults.DisplayName(r)))
            .ToList();

    private PipeRouting _selectedRouting = PipeRouting.Curved;
    /// <summary>要应用到选中管路的连线布局。</summary>
    public PipeRouting SelectedRouting
    {
        get => _selectedRouting;
        set => Set(ref _selectedRouting, value);
    }

    private PipeRouting _defaultRouting = PipeRouting.Orthogonal;
    /// <summary>画布 <c>DefaultPipeRouting</c>：新建管路使用的连线布局。</summary>
    public PipeRouting DefaultRouting
    {
        get => _defaultRouting;
        set => Set(ref _defaultRouting, value);
    }

    private void ApplyRouting()
    {
        if (Diagram?.SelectedConnection is not { } conn)
        {
            StatusText = "未选中管路：先在画布上单击一条管路";
            return;
        }

        conn.Routing = SelectedRouting;
        StatusText = SelectedRouting == PipeRouting.Orthogonal
            ? $"管路 #{conn.Number} 已改为「垂直水平」（拖动中间水平段可上下移、竖直段可左右移）"
            : $"管路 #{conn.Number} 已改为「{PipeRoutingDefaults.DisplayName(SelectedRouting)}」";
    }

    private static string NameOf(PipeKind kind) => PipeDefinitionRegistry.Resolve(kind).DisplayName;

    private void ApplyPipeKind()
    {
        if (Diagram?.SelectedConnection is not { } conn)
        {
            StatusText = "未选中管路：先在画布上单击一条管路";
            return;
        }

        conn.PipeKind = SelectedPipeKind;
        StatusText = $"管路 #{conn.Number} 已切换为「{NameOf(SelectedPipeKind)}」";
    }

    private void BuildPipeDemo()
    {
        var d = Diagram;
        if (d is null) { StatusText = "画布未就绪"; return; }

        double y = 90;
        double x2 = Math.Max(220, Math.Min(460, d.CanvasWidth - 80));

        // 内置 + 三种宿主扩展：粗快（蒸汽）/ 不流动（废液）/ 细慢且反向（采样）
        (PipeKind Kind, FlowMode Direction)[] rows =
        [
            (PipeKind.Standard, FlowMode.Forward),
            (DemoPipeKinds.Steam, FlowMode.Forward),
            (DemoPipeKinds.Waste, FlowMode.Forward),
            (DemoPipeKinds.Sample, FlowMode.Reverse),
        ];

        foreach (var (kind, direction) in rows)
        {
            d.AddFreeConnection(new Point(80, y), new Point(x2, y), direction, kind);
            y += 70;
        }

        StatusText = "已生成：普通 / 蒸汽(粗·快) / 废液(不流动) / 采样(细·慢·反向)";
    }

    private void AddNamelessNode()
    {
        var d = Diagram;
        if (d is null) { StatusText = "画布未就绪"; return; }

        // label 传空串 = 该部件不显示名称（传 null 才会回退到类型默认名）
        var node = d.AddNode(DemoNodeKinds.ThreeWayValve, 560, 80, label: "");
        StatusText = $"已添加无名称三通阀（Id={node.Id}）；属性对话框可再填回名称";
    }

    private void ExecuteSetLedState()
    {
        if (SetNodeStateByNumberCommand is null)
        {
            StatusText = "画布命令未接线（检查 OneWayToSource 绑定）";
            return;
        }

        // 枚举直接装箱为 object 载荷下发；LedDefinition 按枚举渲染对应颜色
        var request = new SetNodeStateRequest(NodeKind.Led, LedNumber, SelectedLedColor);
        SetNodeStateByNumberCommand.Execute(request);

        StatusText = request.Result == true
            ? $"指示灯 #{LedNumber} 已设为{LedColors.First(c => c.Value == SelectedLedColor).Text}"
            : $"未找到 指示灯 #{LedNumber}";
    }

    /// <summary>最近一次执行的结果提示。</summary>
    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    /// <summary>按当前编号与方向发出设置流向请求。</summary>
    public RelayCommand ApplyCommand { get; }

    /// <summary>按当前编号与 LED 颜色发出设置指示灯状态请求。</summary>
    public RelayCommand ApplyLedStateCommand { get; }

    /// <summary>LED 颜色下拉框条目：显示名 + 枚举值。</summary>
    public sealed record LedColorItem(LedColorState Value, string Text);

    /// <summary>LED 颜色可选项，与 <see cref="LedColorState"/> 一一对应。</summary>
    public IReadOnlyList<LedColorItem> LedColors { get; } =
    [
        new(LedColorState.Gray, "灰色（熄灭）"),
        new(LedColorState.Red, "红色"),
        new(LedColorState.Green, "绿色"),
        new(LedColorState.Blue, "蓝色"),
    ];

    private LedColorState _selectedLedColor = LedColorState.Red;
    /// <summary>要下发给指示灯的颜色状态（作为 object 载荷进入 SetNodeState 第三参数）。</summary>
    public LedColorState SelectedLedColor
    {
        get => _selectedLedColor;
        set => Set(ref _selectedLedColor, value);
    }

    private void Execute()
    {
        // 只发请求、不持有控件；是否成功由控件回填到请求对象的 Result
        if (SetDirectionByNumberCommand is null)
        {
            StatusText = "画布命令未接线（检查 OneWayToSource 绑定）";
            return;
        }

        var request = new SetDirectionRequest(Number, Direction);
        SetDirectionByNumberCommand.Execute(request);

        StatusText = request.Result == true
            ? $"管路 #{Number} 已设为{FlowModeDefaults.DisplayName(Direction)}"
            : $"未找到编号为 {Number} 的管路";
    }
}
