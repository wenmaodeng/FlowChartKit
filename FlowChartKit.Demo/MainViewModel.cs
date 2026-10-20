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
/// 宿主 ViewModel：不认识画布控件，只依赖注入进来的回调函数。
/// 「哪条管路设成什么流向」是业务知识，放在这里，视图代码后置不持有这些参数。
/// </summary>
public class MainViewModel : ObservableBase, IFlowDirectionSink
{
    private Func<int, FlowMode, bool>? _setDirection;
    private int _number = 1;
    private FlowMode _direction = FlowMode.Forward;
    private string _statusText = "就绪";

    /// <summary>供 XAML 以 OneWayToSource 绑定接住画布控件的回调（在 XAML 里创建本 VM 时使用）。</summary>
    public MainViewModel()
    {
        // 按钮可用性只判编号：回调是否到位改由状态栏提示，避免"灰按钮"掩盖真实原因
        ApplyCommand = new RelayCommand(_ => Execute(), _ => Number > 0);
    }

    /// <param name="setDirection">按编号设置流向的回调，对应 <see cref="Controls.DiagramCanvasControl.SetConnectionDirection"/>。</param>
    public MainViewModel(Func<int, FlowMode, bool> setDirection) : this()
    {
        SetDirection = setDirection;
    }

    /// <summary>
    /// 按编号设置流向的回调。由画布控件写入（绑定回传或 IFlowDirectionSink 直写），也可代码注入；
    /// 为空时按钮自动不可用，不会出现空引用。
    /// </summary>
    public Func<int, FlowMode, bool>? SetDirection
    {
        get => _setDirection;
        set
        {
            if (!Set(ref _setDirection, value)) return;
            // 回调到位后按钮要立刻可用：CanExecute 依赖它，得主动让命令重新求值
            RelayCommand.Refresh();
        }
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

    /// <summary>按当前编号与方向执行回调。</summary>
    public ICommand ApplyCommand { get; }

    /// <summary>初始流向：1 号正向、2 号反向、3 号停止。</summary>
    public void ApplyInitialFlow()
    {
        var setDirection = SetDirection;
        if (setDirection is null) return;

        foreach (var (number, mode) in new[] { (1, FlowMode.Forward), (2, FlowMode.Reverse), (3, FlowMode.None) })
            setDirection(number, mode);

        StatusText = "已恢复初始流向";
    }

    private void Execute()
    {
        var setDirection = SetDirection;
        if (setDirection is null)
        {
            StatusText = "回调尚未就绪：画布控件还没把设置流向的方法传过来";
            return;
        }

        bool ok = setDirection(Number, Direction);
        StatusText = ok
            ? $"管路 #{Number} 已设为{FlowModeDefaults.DisplayName(Direction)}"
            : $"未找到编号为 {Number} 的管路";
    }
}
