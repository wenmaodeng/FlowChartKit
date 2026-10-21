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
    public void BuiltInDefinitions_RenderWithNodeContext_WithoutException()
    {
        // 二参 DIM 默认委托一参实现：未 override 状态外观的定义经上下文调用必须照常渲染
        var node = new NodeViewModel(NodeKind.SampleCup) { State = "任意宿主载荷" };
        var def = NodeDefinitionRegistry.Resolve(NodeKind.SampleCup);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
            def.Render(dc, node);

        Assert.True(visual.ContentBounds.Width > 0 && visual.ContentBounds.Height > 0);
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
    public void IsBuiltIn_FalseWhenOverridden_TrueAfterUnregister()
    {
        var customKind = (NodeKind)1002;
        var custom = new TestDefinition(customKind, "自定义", (10, 10),
            [new PortSpec("X", 0.5, 0.5, PortDir.Up)]);
        var wasteOverride = new TestDefinition(NodeKind.Waste, "覆盖废液", (10, 10),
            [new PortSpec("X", 0.5, 0.5, PortDir.Up)]);
        try
        {
            Assert.True(NodeDefinitionRegistry.IsBuiltIn(NodeKind.Waste));

            NodeDefinitionRegistry.Register(custom);
            NodeDefinitionRegistry.Register(wasteOverride);

            // 覆盖注册的内置类型与新类型都归扩展（菜单分组依据）
            Assert.False(NodeDefinitionRegistry.IsBuiltIn(NodeKind.Waste));
            Assert.False(NodeDefinitionRegistry.IsBuiltIn(customKind));
            Assert.True(NodeDefinitionRegistry.IsBuiltIn(NodeKind.Led));
        }
        finally
        {
            NodeDefinitionRegistry.Unregister(customKind);
            NodeDefinitionRegistry.Unregister(NodeKind.Waste);
        }

        // 注销后自动回退内置
        Assert.True(NodeDefinitionRegistry.IsBuiltIn(NodeKind.Waste));
    }

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

    // ── 事件目录（NodeEventSpec）──

    [Fact]
    public void Events_DefaultInterfaceImplementation_IsEmptyCatalog()
    {
        // 未声明 Events 的定义走默认接口实现：空目录 = 未启用事件目录（默认单击语义）
        Assert.Empty(NodeDefinitionRegistry.Resolve(NodeKind.SampleCup).Events);
    }

    [Fact]
    public void NodeEventSpec_Resolve_MatchesGesture()
    {
        var click = new NodeEventSpec(NodeEventGesture.Click, "ValveInfo");
        var dbl = new NodeEventSpec(NodeEventGesture.DoubleClick, "ToggleState");
        IReadOnlyList<NodeEventSpec> catalog = [click, dbl];

        Assert.Same(click, NodeEventSpec.Resolve(catalog, NodeEventGesture.Click));
        Assert.Same(dbl, NodeEventSpec.Resolve(catalog, NodeEventGesture.DoubleClick));
    }

    [Fact]
    public void NodeEventSpec_Resolve_EmptyOrUnmatched_ReturnsNull()
    {
        Assert.Null(NodeEventSpec.Resolve([], NodeEventGesture.Click));

        IReadOnlyList<NodeEventSpec> clickOnly = [new(NodeEventGesture.Click, "A")];
        Assert.Null(NodeEventSpec.Resolve(clickOnly, NodeEventGesture.DoubleClick));
    }

    [Fact]
    public void NodeEventSpec_Resolve_TakesFirstMatch_WhenDuplicated()
    {
        // 同一手势重复声明只取第一个——唯一性由目录作者负责，解析器保持确定性
        IReadOnlyList<NodeEventSpec> catalog =
        [
            new(NodeEventGesture.Click, "First"),
            new(NodeEventGesture.Click, "Second"),
        ];

        Assert.Equal("First", NodeEventSpec.Resolve(catalog, NodeEventGesture.Click)!.EventId);
    }

    [Fact]
    public void NodeEventSpec_NeedsClickBackoff_OnlyWhenBothGesturesDeclared()
    {
        // 仅双击 / 仅单击 / 空目录：单击即时触发，无需退避
        Assert.False(NodeEventSpec.NeedsClickBackoff([new(NodeEventGesture.DoubleClick, "A")]));
        Assert.False(NodeEventSpec.NeedsClickBackoff([new(NodeEventGesture.Click, "A")]));
        Assert.False(NodeEventSpec.NeedsClickBackoff([]));

        // 单击+双击并存：双击必先产生一次单击，需退避由双击取消
        Assert.True(NodeEventSpec.NeedsClickBackoff(
        [
            new(NodeEventGesture.Click, "A"),
            new(NodeEventGesture.DoubleClick, "B"),
        ]));
    }

    // ── 内置 LED 指示灯 ──

    [Fact]
    public void Led_DoubleClick_CyclesGrayRedGreenBlue()
    {
        var def = NodeDefinitionRegistry.Resolve(NodeKind.Led);
        var node = new NodeViewModel(NodeKind.Led);
        Assert.Null(node.State); // 初始未设置，渲染按灰

        // 双击循环：灰 → 红 → 绿 → 蓝 → 灰（未设置的 State 视为灰起点）
        def.InvokeEvent(node, NodeEventGesture.DoubleClick, "ToggleColor");
        Assert.Equal(LedDefinition.StateRed, node.State);
        def.InvokeEvent(node, NodeEventGesture.DoubleClick, "ToggleColor");
        Assert.Equal(LedDefinition.StateGreen, node.State);
        def.InvokeEvent(node, NodeEventGesture.DoubleClick, "ToggleColor");
        Assert.Equal(LedDefinition.StateBlue, node.State);
        def.InvokeEvent(node, NodeEventGesture.DoubleClick, "ToggleColor");
        Assert.Equal(LedDefinition.StateGray, node.State);
        def.InvokeEvent(node, NodeEventGesture.DoubleClick, "ToggleColor");
        Assert.Equal(LedDefinition.StateRed, node.State);
    }

    [Fact]
    public void Led_InvokeEvent_UnknownStateStartsCycleAsGray()
    {
        var def = NodeDefinitionRegistry.Resolve(NodeKind.Led);
        var node = new NodeViewModel(NodeKind.Led) { State = "宿主自定义载荷" };

        // 未匹配约定色的 State 视为灰：下一次切到红
        def.InvokeEvent(node, NodeEventGesture.DoubleClick, "ToggleColor");
        Assert.Equal(LedDefinition.StateRed, node.State);
    }

    [Fact]
    public void Led_InvokeEvent_OtherGesturesNotConsumed()
    {
        var def = NodeDefinitionRegistry.Resolve(NodeKind.Led);
        var node = new NodeViewModel(NodeKind.Led);

        // 只声明并消费双击；单击/未知 EventId 不消费（返回 false，照常上抛宿主）
        Assert.False(def.InvokeEvent(node, NodeEventGesture.Click, "ToggleColor"));
        Assert.False(def.InvokeEvent(node, NodeEventGesture.DoubleClick, "Unknown"));
        Assert.Null(node.State);
    }

    [Fact]
    public void Led_Render_AllStates_WithoutException()
    {
        var def = NodeDefinitionRegistry.Resolve(NodeKind.Led);
        var node = new NodeViewModel(NodeKind.Led);

        foreach (var state in new object?[] { null, "宿主自定义载荷", LedDefinition.StateGray, LedDefinition.StateRed, LedDefinition.StateGreen, LedDefinition.StateBlue })
        {
            node.State = state;
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
                def.Render(dc, node);
            Assert.True(visual.ContentBounds.Width > 0 && visual.ContentBounds.Height > 0);
        }
    }

    [Fact]
    public void BuiltInDefinitions_InvokeEvent_DefaultNotConsumed()
    {
        // 未实现内置手势处理的定义（DIM 默认）：调用返回 false，事件照常上抛宿主
        var def = NodeDefinitionRegistry.Resolve(NodeKind.SampleCup);
        var node = new NodeViewModel(NodeKind.SampleCup);
        Assert.False(def.InvokeEvent(node, NodeEventGesture.Click, "Any"));
        Assert.False(def.InvokeEvent(node, NodeEventGesture.DoubleClick, "Any"));
    }
}
