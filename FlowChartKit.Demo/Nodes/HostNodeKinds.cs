using FlowChartKit;

namespace FlowChartKit.Demo.Nodes;

/// <summary>
/// 宿主自定义部件类型：核心库枚举之外的扩展值（序列化按数值落盘，回读不受影响）。
/// </summary>
public static class HostNodeKinds
{
    public const NodeKind SyringePump = (NodeKind)1010;      // 注射泵（四边各 4 端口）
    public const NodeKind PureWaterBucket = (NodeKind)1011;  // 纯水桶（仅顶部 1 端口）
    public const NodeKind WasteBucket = (NodeKind)1012;      // 废液桶（仅顶部 1 端口，在瓶盖中心）
    public const NodeKind SampleSlot = (NodeKind)1013;       // 样品位（左右各 1 端口，接近顶部）
    public const NodeKind WashSlot = (NodeKind)1014;         // 洗针槽（左右各 1 端口，接近顶部）
    public const NodeKind Cuvette1Cm = (NodeKind)1015;       // 1cm 比色皿（仅顶部居中 1 端口）
    public const NodeKind Cuvette3Cm = (NodeKind)1016;       // 3cm 比色皿（仅顶部居中 1 端口）
    public const NodeKind ReagentBottle = (NodeKind)1017;    // 试剂瓶（仅顶部居中 1 端口）
    public const NodeKind ExhaustValve = (NodeKind)1018;     // 排气阀（上下左右各 1 端口）
    public const NodeKind DigestionPot = (NodeKind)1019;     // 消解锅（左右各 2 端口）
    public const NodeKind WaterPump = (NodeKind)1020;        // 水泵（左右各 1 端口）
}
