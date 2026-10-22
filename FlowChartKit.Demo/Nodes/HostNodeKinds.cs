using FlowChartKit;

namespace FlowChartKit.Demo.Nodes;

/// <summary>
/// 宿主自定义部件类型：核心库枚举之外的扩展值（序列化按数值落盘，回读不受影响）。
/// </summary>
public static class HostNodeKinds
{
    public const NodeKind SyringePump = (NodeKind)1010; // 注射泵（四边各 4 端口）
}
