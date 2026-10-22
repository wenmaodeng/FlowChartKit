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
/// 管路类型。核心库仅内置 <see cref="Standard"/>（普通管路）；业务管路（如蒸汽管、废液管）
/// 由宿主以同数值区间的常量扩展（如 <c>(PipeKind)1000</c>），序列化按枚举名落盘。
/// 每种类型的颜色/粗细/流动画由 <c>IPipeDefinition</c> 提供（见 PipeDefinitionRegistry）。
/// </summary>
public enum PipeKind
{
    /// <summary>普通管路（默认）：蓝灰实线，流动时为等距虚线动画</summary>
    Standard = 0
}

/// <summary>
/// 管路连线布局。决定两点之间如何走线，与管路样式（<see cref="PipeKind"/>）互不影响：
/// 同一条管路可以既是"蒸汽管"又是"弧线"。
/// </summary>
public enum PipeRouting
{
    /// <summary>垂直水平（默认）：横平竖直的正交折线，带端口出线短直段；可拖动中间线段调整绕行位置</summary>
    Orthogonal = 0,
    /// <summary>直线：两端点直接连线（可能穿过部件）</summary>
    Straight = 1,
    /// <summary>弧线：按端口出线方向生成的三次贝塞尔曲线，端口处切线方向与端口朝向一致</summary>
    Curved = 2
}

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

/// <summary>连线布局的默认显示名（属性对话框与宿主下拉框共用）</summary>
public static class PipeRoutingDefaults
{
    public static string DisplayName(PipeRouting routing) => routing switch
    {
        PipeRouting.Straight => "直线",
        PipeRouting.Curved => "弧线",
        _ => "垂直水平"
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
