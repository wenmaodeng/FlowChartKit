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
/// 宿主 ViewModel：完全不认识画布控件，只持有一个由画布控件经 OneWayToSource 绑定灌入的
/// <see cref="ICommand"/>，调用时传 <see cref="SetDirectionRequest"/> 并读取回填的 Result。
/// 这是最纯的 MVVM 分层：VM 不引用任何 View / 控件类型，可独立单元测试（用 mock command 即可）。
/// </summary>
public class MainViewModel : ObservableBase
{
    private int _number = 1;
    private FlowMode _direction = FlowMode.Forward;
    private string _statusText = "就绪";
    private ICommand? _setDirectionCommand;

    /// <summary>ViewModel 直接在 XAML 里创建，无需代码后置注入。</summary>
    public MainViewModel()
    {
        // 按钮可用性只判编号；成功与否由命令回填的 Result 决定，不会出现"灰按钮"掩盖真实原因
        ApplyCommand = new RelayCommand(_ => Execute(), _ => Number > 0);
    }

    /// <summary>由画布控件的同名依赖属性经 OneWayToSource 绑定灌入；未接线时为 null。</summary>
    public ICommand? SetDirectionCommand
    {
        get => _setDirectionCommand;
        set => Set(ref _setDirectionCommand, value);
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
        // 只发请求、不持有控件；是否成功由控件回填到请求对象的 Result
        if (SetDirectionCommand is null)
        {
            StatusText = "画布命令未接线（检查 OneWayToSource 绑定）";
            return;
        }

        var request = new SetDirectionRequest(Number, Direction);
        SetDirectionCommand.Execute(request);

        StatusText = request.Result == true
            ? $"管路 #{Number} 已设为{FlowModeDefaults.DisplayName(Direction)}"
            : $"未找到编号为 {Number} 的管路";
    }
}
