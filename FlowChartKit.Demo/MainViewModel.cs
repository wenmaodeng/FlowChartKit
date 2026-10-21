using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using FlowChartKit;
using FlowChartKit.Core;
using FlowChartKit.Rendering;

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
