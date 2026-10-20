using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using FlowChartKit;
using FlowChartKit.Core;

namespace FlowChartKit.Demo;

/// <summary>方向下拉框的条目：显示名 + 实际枚举值。</summary>
public sealed record FlowModeItem(FlowMode Value, string Text);

/// <summary>
/// 宿主 ViewModel：完全不认识画布控件，只通过 <see cref="IFlowDirectionRequester.SetDirectionRequested"/>
/// 事件对外发出"按编号设置流向"的请求；由画布控件订阅该事件并落到自身图模型上。
/// 这是最纯的 MVVM 分层：VM 不引用任何 View / 控件类型，可独立单元测试。
/// </summary>
public class MainViewModel : ObservableBase, IFlowDirectionRequester
{
    private int _number = 1;
    private FlowMode _direction = FlowMode.Forward;
    private string _statusText = "就绪";

    /// <summary>请求按编号设置流向的事件；画布控件订阅并把请求落到图上。</summary>
    public event EventHandler<SetDirectionEventArgs>? SetDirectionRequested;

    /// <summary>ViewModel 直接在 XAML 里创建，无需代码后置注入。</summary>
    public MainViewModel()
    {
        // 按钮可用性只判编号；成功与否由事件回传的 Result 决定，不会出现"灰按钮"掩盖真实原因
        ApplyCommand = new RelayCommand(_ => Execute(), _ => Number > 0);
    }

    /// <summary>要设置流向的管路编号，从 1 开始，与画布上的编号徽章一致。</summary>
    public int Number
    {
        get => _number;
        set => Set(ref _number, value);
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

    /// <summary>最近一次执行的结果提示。</summary>
    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    /// <summary>按当前编号与方向发出设置流向请求。</summary>
    public ICommand ApplyCommand { get; }

    private void Execute()
    {
        // 只发请求、不持有控件；是否成功由处理方回填到事件参数的 Result
        var args = new SetDirectionEventArgs(Number, Direction);
        SetDirectionRequested?.Invoke(this, args);

        StatusText = args.Result == true
            ? $"管路 #{Number} 已设为{FlowModeDefaults.DisplayName(Direction)}"
            : $"未找到编号为 {Number} 的管路";
    }
}
