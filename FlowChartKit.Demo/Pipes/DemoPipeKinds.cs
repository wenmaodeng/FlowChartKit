using FlowChartKit;

namespace FlowChartKit.Demo.Pipes;

/// <summary>
/// Demo 自定义管路类型：核心库 <see cref="PipeKind"/> 仅含内置的 Standard，
/// 业务管路（同 DemoNodeKinds 的约定）由宿主用常量扩展，按枚举名落盘。
/// </summary>
public static class DemoPipeKinds
{
    public const PipeKind Steam = (PipeKind)1000;    // 蒸汽管：粗红线、快流动
    public const PipeKind Waste = (PipeKind)1001;    // 废液管：灰色、不流动
    public const PipeKind Sample = (PipeKind)1002;   // 采样管：细绿线、慢流动
}
