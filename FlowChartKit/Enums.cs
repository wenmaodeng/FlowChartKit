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
    PumpModule     // 注射泵 + 多通阀
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

/// <summary>部件类型的默认显示名 / 尺寸 / 默认端口</summary>
public static class NodeKindDefaults
{
    public static string DisplayName(NodeKind kind) => kind switch
    {
        NodeKind.SampleCup => "样品杯",
        NodeKind.ReagentBottle => "试剂瓶",
        NodeKind.Waste => "废液",
        NodeKind.ReactionCup => "反应杯",
        NodeKind.Cuvette => "2cm比色皿",
        NodeKind.Centrifuge => "离心位",
        NodeKind.NeedleWash => "洗针位",
        NodeKind.PumpModule => "进样注射泵",
        _ => "部件"
    };

    public static (double W, double H) Size(NodeKind kind) => kind switch
    {
        NodeKind.SampleCup => (46, 64),
        NodeKind.ReagentBottle => (56, 58),
        NodeKind.Waste => (64, 76),
        NodeKind.ReactionCup => (56, 80),
        NodeKind.Cuvette => (46, 74),
        NodeKind.Centrifuge => (56, 80),
        NodeKind.NeedleWash => (46, 84),
        NodeKind.PumpModule => (130, 320),
        _ => (56, 64)
    };
}
