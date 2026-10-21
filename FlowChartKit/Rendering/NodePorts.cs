namespace FlowChartKit.Rendering;

/// <summary>共享端口规格：常规四端口（上下左右中点）。核心库与宿主定义均可复用。</summary>
public static class NodePorts
{
    public static IReadOnlyList<PortSpec> Standard { get; } =
    [
        new PortSpec("T", 0.5, 0, PortDir.Up),
        new PortSpec("B", 0.5, 1, PortDir.Down),
        new PortSpec("L", 0,   0.5, PortDir.Left),
        new PortSpec("R", 1,   0.5, PortDir.Right),
    ];
}
