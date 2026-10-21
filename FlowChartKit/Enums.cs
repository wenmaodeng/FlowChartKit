using System.Windows;

namespace FlowChartKit;

/// <summary>部件类型</summary>
public enum NodeKind
{
    SampleCup,     // 样品杯
    ReagentBottle, // 试剂瓶（锥形瓶）
    Waste,         // 废液
    ReactionCup,   // 反应杯
    Cuvette,       // 比色皿
    Centrifuge,    // 离心位
    NeedleWash,    // 洗针位
    PumpModule,    // 注射泵 + 多通阀
    ThreeWayValve, // 三通阀
    Led            // LED 指示灯（双击切换红/绿/蓝/灰）
}

/// <summary>端口出口方向</summary>
public enum PortDir { Up, Down, Left, Right }

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
