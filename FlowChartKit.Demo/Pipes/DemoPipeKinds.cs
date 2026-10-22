using FlowChartKit;

namespace FlowChartKit.Demo.Pipes;

/// <summary>
/// Demo 自定义管路类型：核心库 <see cref="PipeKind"/> 仅含内置的 Standard，
/// 业务管路（同 DemoNodeKinds 的约定）由宿主用常量扩展，按枚举名落盘。
/// </summary>
public static class DemoPipeKinds
{
    public const PipeKind FlowPath = (PipeKind)1003; // 流路：2px #3161FF 蓝线
}
