using System.Windows.Input;

namespace FlowChartKit.Core;

/// <summary>
/// 轻量命令实现。CanExecuteChanged 同时支持两种刷新方式：
/// 1) 默认跟随 <see cref="CommandManager.RequerySuggested"/>（WPF 全局轮询，菜单/按钮免手工刷新）；
/// 2) 调用方在相关属性变化时显式 <see cref="RaiseCanExecuteChanged"/>，立即刷新、不依赖焦点/输入事件。
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
        // RequerySuggested 对处理器持弱引用，不会根住本命令
        CommandManager.RequerySuggested += OnRequerySuggested;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged;

    /// <summary>显式通知可用性可能已变化，请在影响判定条件的属性 setter 中调用。</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private void OnRequerySuggested(object? sender, EventArgs e) => RaiseCanExecuteChanged();

    /// <summary>全局请求重查所有命令（保留给批量场景；日常优先用实例级 <see cref="RaiseCanExecuteChanged"/>）。</summary>
    public static void Refresh() => CommandManager.InvalidateRequerySuggested();
}
