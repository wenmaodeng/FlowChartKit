using System.Windows;
using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;
using FlowChartKit.ViewModels;
using Xunit;

namespace FlowChartKit.Tests;

public class NodeDefinitionTests
{
    [Fact]
    public void Registry_ResolvesEveryNodeKind()
    {
        foreach (var kind in Enum.GetValues<NodeKind>())
        {
            var def = NodeDefinitionRegistry.Resolve(kind);
            Assert.NotNull(def);
            Assert.Equal(kind, def.Kind);
        }
    }

    [Fact]
    public void BuiltInDefinitions_HaveValidMetadata()
    {
        foreach (var kind in Enum.GetValues<NodeKind>())
        {
            var def = NodeDefinitionRegistry.Resolve(kind);

            Assert.False(string.IsNullOrWhiteSpace(def.DisplayName), $"{kind} 缺显示名");
            Assert.True(def.BaseSize.W > 0 && def.BaseSize.H > 0, $"{kind} 基准尺寸非法");
            Assert.NotEmpty(def.Ports);

            foreach (var port in def.Ports)
            {
                Assert.False(string.IsNullOrWhiteSpace(port.Id), $"{kind} 存在空端口 Id");
                Assert.InRange(port.FX, 0, 1);
                Assert.InRange(port.FY, 0, 1);
            }

            // 端口 Id 在部件内唯一，否则序列化按 Id 找回端口时会错配
            Assert.Equal(def.Ports.Count, def.Ports.Select(p => p.Id).Distinct().Count());
        }
    }

    [Fact]
    public void BuiltInDefinitions_RenderWithoutException()
    {
        foreach (var kind in Enum.GetValues<NodeKind>())
        {
            var def = NodeDefinitionRegistry.Resolve(kind);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
                def.Render(dc);
            // 渲染出了实际内容（边界非空）
            Assert.True(visual.ContentBounds.Width > 0 && visual.ContentBounds.Height > 0,
                $"{kind} 未绘制任何内容");
        }
    }

    [Fact]
    public void NodeViewModel_TakesNameSizePortsFromDefinition()
    {
        foreach (var kind in Enum.GetValues<NodeKind>())
        {
            var def = NodeDefinitionRegistry.Resolve(kind);
            var node = new NodeViewModel(kind);

            Assert.Equal(def.DisplayName, node.Label);
            Assert.Equal(def.BaseSize.W, node.BaseWidth);
            Assert.Equal(def.BaseSize.H, node.BaseHeight);
            Assert.Equal(def.Ports.Select(p => p.Id), node.Ports.Select(p => p.Id));
            Assert.All(node.Ports, p => Assert.Same(node, p.Owner));
        }
    }

    [Fact]
    public void ThreeWayValve_HasThreePorts()
    {
        var node = new NodeViewModel(NodeKind.ThreeWayValve);

        Assert.Equal("三通阀", node.Label);
        Assert.Equal(3, node.Ports.Count);
        Assert.Equal(["L", "R", "B"], node.Ports.Select(p => p.Id).ToArray());
        Assert.Equal([PortDir.Left, PortDir.Right, PortDir.Down], node.Ports.Select(p => p.Dir).ToArray());
    }

    [Fact]
    public void PumpModule_HasNineDedicatedPorts()
    {
        var def = NodeDefinitionRegistry.Resolve(NodeKind.PumpModule);
        Assert.Equal(9, def.Ports.Count);
        Assert.Equal(["T", "L1", "L2", "R1", "R2", "B1", "B2", "B3", "B4"],
            def.Ports.Select(p => p.Id).ToArray());
    }

    [Fact]
    public void Register_OverrideAffectsNewNodes_UnregisterRestoresBuiltIn()
    {
        // 用其他测试用不到的 Waste，且 finally 移除覆盖，避免污染共享注册表
        var builtIn = NodeDefinitionRegistry.Resolve(NodeKind.Waste);
        var custom = new TestDefinition(NodeKind.Waste, "自定义废液", (100, 120),
            [new PortSpec("X", 0.5, 0.5, PortDir.Up)]);
        try
        {
            NodeDefinitionRegistry.Register(custom);

            Assert.Same(custom, NodeDefinitionRegistry.Resolve(NodeKind.Waste));

            var node = new NodeViewModel(NodeKind.Waste);
            Assert.Equal("自定义废液", node.Label);
            Assert.Equal(100, node.BaseWidth);
            Assert.Equal(120, node.BaseHeight);
            Assert.Single(node.Ports);
            Assert.Equal("X", node.Ports[0].Id);
        }
        finally
        {
            NodeDefinitionRegistry.Unregister(NodeKind.Waste);
        }
        // 注销自定义定义后回退内置定义，而非不可解析
        Assert.Same(builtIn, NodeDefinitionRegistry.Resolve(NodeKind.Waste));
    }

    [Fact]
    public void Unregister_WithoutOverride_KeepsBuiltIn()
    {
        var builtIn = NodeDefinitionRegistry.Resolve(NodeKind.Waste);

        Assert.False(NodeDefinitionRegistry.Unregister(NodeKind.Waste)); // 没有自定义定义可移除
        Assert.Same(builtIn, NodeDefinitionRegistry.Resolve(NodeKind.Waste)); // 内置定义不受影响
    }

    [Fact]
    public void Resolve_UnknownKind_Throws()
        => Assert.Throws<InvalidOperationException>(
            () => NodeDefinitionRegistry.Resolve((NodeKind)int.MaxValue));

    [Fact]
    public void All_MergesBuiltInsAndOverrides()
    {
        var builtInCount = Enum.GetValues<NodeKind>().Length;
        Assert.Equal(builtInCount, NodeDefinitionRegistry.All.Count);

        var customKind = (NodeKind)1001; // 枚举之外的自定义类型
        var custom = new TestDefinition(customKind, "自定义", (10, 10),
            [new PortSpec("X", 0.5, 0.5, PortDir.Up)]);
        var wasteOverride = new TestDefinition(NodeKind.Waste, "覆盖废液", (10, 10),
            [new PortSpec("X", 0.5, 0.5, PortDir.Up)]);
        try
        {
            NodeDefinitionRegistry.Register(custom);
            NodeDefinitionRegistry.Register(wasteOverride);

            var all = NodeDefinitionRegistry.All;
            Assert.Equal(builtInCount + 1, all.Count); // 新类型 +1；同类型覆盖不增量
            Assert.Contains(all, d => ReferenceEquals(d, custom));
            Assert.DoesNotContain(all, d => d.Kind == NodeKind.Waste && !ReferenceEquals(d, wasteOverride));
        }
        finally
        {
            NodeDefinitionRegistry.Unregister(customKind);
            NodeDefinitionRegistry.Unregister(NodeKind.Waste);
        }
    }

    private sealed class TestDefinition(
        NodeKind kind, string displayName, (double W, double H) baseSize, IReadOnlyList<PortSpec> ports)
        : INodeDefinition
    {
        public NodeKind Kind => kind;
        public string DisplayName => displayName;
        public (double W, double H) BaseSize => baseSize;
        public IReadOnlyList<PortSpec> Ports => ports;
        public void Render(DrawingContext dc) { }
    }
}
