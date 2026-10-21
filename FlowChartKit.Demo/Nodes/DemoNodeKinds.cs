using FlowChartKit;

namespace FlowChartKit.Demo.Nodes;

/// <summary>
/// Demo 自定义部件类型：核心库枚举之外的扩展值（序列化按数值落盘，回读不受影响）。
/// 历史值 0-8 曾位于核心库枚举（后移除），此处以常量保持原数值——既有存档直接回读兼容。
/// </summary>
public static class DemoNodeKinds
{
    public const NodeKind SampleCup     = (NodeKind)1000;    // 样品杯
    public const NodeKind ReagentBottle = (NodeKind)1001;    // 试剂瓶（锥形瓶）
    public const NodeKind Waste         = (NodeKind)1002;    // 废液
    public const NodeKind ReactionCup   = (NodeKind)1003;    // 反应杯
    public const NodeKind Cuvette       = (NodeKind)1004;    // 比色皿
    public const NodeKind Centrifuge    = (NodeKind)1005;    // 离心位
    public const NodeKind NeedleWash    = (NodeKind)1006;    // 洗针位
    public const NodeKind PumpModule    = (NodeKind)1007;    // 注射泵 + 多通阀
    public const NodeKind ThreeWayValve = (NodeKind)1008;    // 三通阀
    public const NodeKind SolenoidValve = (NodeKind)1009; // 电磁阀
}
