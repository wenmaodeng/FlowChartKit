using System.Windows;

namespace FlowChartKit;

/// <summary>
/// 部件类型。核心库仅内置 LED 与开关；业务部件由宿主以同数值区间的常量扩展
/// （参考 DemoNodeKinds：历史值 0-8 及 (NodeKind)1000 电磁阀），序列化按数值落盘。
/// </summary>
public enum NodeKind
{
    /// <summary>LED 指示灯（双击切换红/绿/蓝/灰）。保留历史数值以兼容旧存档</summary>
    Led,
    /// <summary>开关（双击切换通/断）。保留历史数值以兼容旧存档</summary>
    Switch
}

/// <summary>端口出口方向</summary>
public enum PortDir { Up, Down, Left, Right }

/// <summary>
/// 开关部件的通断状态：作为 <c>NodeViewModel.State</c> 的约定载荷类型，
/// 经 <c>SetNodeStateByNumberCommand</c>（或直接 <c>SetNodeState</c>）下发；
/// 双击切换的也是此枚举。未匹配的载荷（null 或宿主自定义对象）按 Off 渲染。
/// </summary>
public enum SwitchState
{
    /// <summary>断开（默认）</summary>
    Off = 0,
    /// <summary>闭合</summary>
    On = 1
}

/// <summary>
/// LED 指示灯的颜色状态：作为 <c>NodeViewModel.State</c> 的约定载荷类型，
/// 经 <c>SetNodeStateByNumberCommand</c>（或直接 <c>SetNodeState</c>）下发；
/// 双击循环切换的也是此枚举。未匹配的载荷（null 或宿主自定义对象）按 Gray 渲染。
/// </summary>
public enum LedColorState
{
    /// <summary>灰色（熄灭，默认）</summary>
    Gray = 0,
    /// <summary>红色</summary>
    Red = 1,
    /// <summary>绿色</summary>
    Green = 2,
    /// <summary>蓝色</summary>
    Blue = 3
}

/// <summary>流路方向</summary>
public enum FlowMode
{
    /// <summary>停止（不显示流向动画）</summary>
    None,
    /// <summary>正向：源 → 目标</summary>
    Forward,
    /// <summary>反向：目标 → 源</summary>
    Reverse
}

public static class PortDirExtensions
{
    public static Vector Vec(this PortDir d) => d switch
    {
        PortDir.Up => new Vector(0, -1),
        PortDir.Down => new Vector(0, 1),
        PortDir.Left => new Vector(-1, 0),
        _ => new Vector(1, 0)
    };
}

/// <summary>流路方向的默认显示名</summary>
public static class FlowModeDefaults
{
    public static string DisplayName(FlowMode mode) => mode switch
    {
        FlowMode.Forward => "正向",
        FlowMode.Reverse => "反向",
        _ => "停止"
    };
}
