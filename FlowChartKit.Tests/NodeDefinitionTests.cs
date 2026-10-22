using System.Windows;
using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Demo.Nodes;
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
    public void RegisteredDefinitions_HaveValidMetadata()
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
    public void RegisteredDefinitions_RenderWithoutException()
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
    public void RegisteredDefinitions_RenderWithNodeContext_WithoutException()
    {
        // 二参 DIM 默认委托一参实现：未 override 状态外观的定义经上下文调用必须照常渲染
        var node = new NodeViewModel(DemoNodeKinds.SyringePump) { State = "任意宿主载荷" };
        var def = NodeDefinitionRegistry.Resolve(DemoNodeKinds.SyringePump);

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
    public void SyringePump_HasSixteenPorts_FourPerSide()
    {
        var node = new NodeViewModel(DemoNodeKinds.SyringePump);

        Assert.Equal("注射泵", node.Label);
        Assert.Equal(16, node.Ports.Count);
        Assert.Equal(["T1", "T2", "T3", "T4", "B1", "B2", "B3", "B4",
                      "L1", "L2", "L3", "L4", "R1", "R2", "R3", "R4"],
            node.Ports.Select(p => p.Id).ToArray());
        // 每边 4 个端口，朝向与所在边一致
        Assert.Equal(4, node.Ports.Count(p => p.Dir == PortDir.Up));
        Assert.Equal(4, node.Ports.Count(p => p.Dir == PortDir.Down));
        Assert.Equal(4, node.Ports.Count(p => p.Dir == PortDir.Left));
        Assert.Equal(4, node.Ports.Count(p => p.Dir == PortDir.Right));
    }

    [Fact]
    public void Register_OverrideAffectsNewNodes_UnregisterRestoresBuiltIn()
    {
        // 用内置层类型 Led 做覆盖：注销后真正回退内置定义（其余类型由 Demo 注册在覆盖层，
        // Unregister 会直接移除而非回退，不能在此使用）
        var builtIn = NodeDefinitionRegistry.Resolve(NodeKind.Led);
        var custom = new TestDefinition(NodeKind.Led, "自定义灯", (100, 120),
            [new PortSpec("X", 0.5, 0.5, PortDir.Up)]);
        try
        {
            NodeDefinitionRegistry.Register(custom);

            Assert.Same(custom, NodeDefinitionRegistry.Resolve(NodeKind.Led));

            var node = new NodeViewModel(NodeKind.Led);
            Assert.Equal("自定义灯", node.Label);
            Assert.Equal(100, node.BaseWidth);
            Assert.Equal(120, node.BaseHeight);
            Assert.Single(node.Ports);
            Assert.Equal("X", node.Ports[0].Id);
        }
        finally
        {
            NodeDefinitionRegistry.Unregister(NodeKind.Led);
        }
        // 注销自定义定义后回退内置定义，而非不可解析
        Assert.Same(builtIn, NodeDefinitionRegistry.Resolve(NodeKind.Led));
    }

    [Fact]
    public void Unregister_WithoutOverride_KeepsBuiltIn()
    {
        var builtIn = NodeDefinitionRegistry.Resolve(NodeKind.Led);

        Assert.False(NodeDefinitionRegistry.Unregister(NodeKind.Led)); // 没有自定义定义可移除
        Assert.Same(builtIn, NodeDefinitionRegistry.Resolve(NodeKind.Led)); // 内置定义不受影响
    }

    [Fact]
    public void Resolve_UnknownKind_Throws()
        => Assert.Throws<InvalidOperationException>(
            () => NodeDefinitionRegistry.Resolve((NodeKind)int.MaxValue));

    [Fact]
    public void IsBuiltIn_FalseWhenOverridden_TrueAfterUnregister()
    {
        var customKind = (NodeKind)5002; // 远离 Demo 扩展区的未注册类型
        var custom = new TestDefinition(customKind, "自定义", (10, 10),
            [new PortSpec("X", 0.5, 0.5, PortDir.Up)]);
        var ledOverride = new TestDefinition(NodeKind.Led, "覆盖灯", (10, 10),
            [new PortSpec("X", 0.5, 0.5, PortDir.Up)]);
        try
        {
            Assert.True(NodeDefinitionRegistry.IsBuiltIn(NodeKind.Led));

            NodeDefinitionRegistry.Register(custom);
            NodeDefinitionRegistry.Register(ledOverride);

            // 覆盖注册的内置类型与新类型都归扩展（菜单分组依据）
            Assert.False(NodeDefinitionRegistry.IsBuiltIn(NodeKind.Led));
            Assert.False(NodeDefinitionRegistry.IsBuiltIn(customKind));
        }
        finally
        {
            NodeDefinitionRegistry.Unregister(customKind);
            NodeDefinitionRegistry.Unregister(NodeKind.Led);
        }

        // 注销后自动回退内置
        Assert.True(NodeDefinitionRegistry.IsBuiltIn(NodeKind.Led));
    }

    [Fact]
    public void All_MergesBuiltInsAndOverrides()
    {
        // 初态 = 内置 Led + Demo 注册的覆盖层定义（含枚举外的注射泵）
        var initialCount = NodeDefinitionRegistry.All.Count;

        var customKind = (NodeKind)5001; // 远离 Demo 扩展区(1010)的未注册类型
        var custom = new TestDefinition(customKind, "自定义", (10, 10),
            [new PortSpec("X", 0.5, 0.5, PortDir.Up)]);
        var ledOverride = new TestDefinition(NodeKind.Led, "覆盖灯", (10, 10),
            [new PortSpec("X", 0.5, 0.5, PortDir.Up)]);
        try
        {
            NodeDefinitionRegistry.Register(custom);
            NodeDefinitionRegistry.Register(ledOverride);

            var all = NodeDefinitionRegistry.All;
            Assert.Equal(initialCount + 1, all.Count); // 新类型 +1；同类型覆盖不增量
            Assert.Contains(all, d => ReferenceEquals(d, custom));
            Assert.Contains(all, d => ReferenceEquals(d, ledOverride));
        }
        finally
        {
            NodeDefinitionRegistry.Unregister(customKind);
            NodeDefinitionRegistry.Unregister(NodeKind.Led);
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
        Assert.Empty(NodeDefinitionRegistry.Resolve(DemoNodeKinds.SyringePump).Events);
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
        Assert.Equal(LedColorState.Red, node.State);
        def.InvokeEvent(node, NodeEventGesture.DoubleClick, "ToggleColor");
        Assert.Equal(LedColorState.Green, node.State);
        def.InvokeEvent(node, NodeEventGesture.DoubleClick, "ToggleColor");
        Assert.Equal(LedColorState.Blue, node.State);
        def.InvokeEvent(node, NodeEventGesture.DoubleClick, "ToggleColor");
        Assert.Equal(LedColorState.Gray, node.State);
        def.InvokeEvent(node, NodeEventGesture.DoubleClick, "ToggleColor");
        Assert.Equal(LedColorState.Red, node.State);
    }

    [Fact]
    public void Led_InvokeEvent_UnknownStateStartsCycleAsGray()
    {
        var def = NodeDefinitionRegistry.Resolve(NodeKind.Led);
        var node = new NodeViewModel(NodeKind.Led) { State = "宿主自定义载荷" };

        // 未匹配约定色的 State 视为灰：下一次切到红
        def.InvokeEvent(node, NodeEventGesture.DoubleClick, "ToggleColor");
        Assert.Equal(LedColorState.Red, node.State);
    }

    [Fact]
    public void Switch_InvokeEvent_TogglesState()
    {
        var def = NodeDefinitionRegistry.Resolve(NodeKind.Switch);
        var node = new NodeViewModel(NodeKind.Switch);

        // 默认 Off → On → Off
        def.InvokeEvent(node, NodeEventGesture.DoubleClick, "ToggleSwitch");
        Assert.Equal(SwitchState.On, node.State);
        def.InvokeEvent(node, NodeEventGesture.DoubleClick, "ToggleSwitch");
        Assert.Equal(SwitchState.Off, node.State);
    }

    [Fact]
    public void Switch_InvokeEvent_UnknownStateTreatedAsOff()
    {
        var def = NodeDefinitionRegistry.Resolve(NodeKind.Switch);
        var node = new NodeViewModel(NodeKind.Switch) { State = "宿主自定义载荷" };

        // 未匹配约定态的 State 视为 Off：切换后为 On
        def.InvokeEvent(node, NodeEventGesture.DoubleClick, "ToggleSwitch");
        Assert.Equal(SwitchState.On, node.State);
    }

    [Fact]
    public void Switch_RenderWithoutException_AllStates()
    {
        var def = NodeDefinitionRegistry.Resolve(NodeKind.Switch);
        foreach (var state in new object?[] { null, "宿主自定义载荷", SwitchState.Off, SwitchState.On })
        {
            var node = new NodeViewModel(NodeKind.Switch) { State = state };
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
                def.Render(dc, node);
            Assert.True(visual.ContentBounds.Width > 0 && visual.ContentBounds.Height > 0,
                $"State={state} 渲染为空");
        }
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

        foreach (var state in new object?[] { null, "宿主自定义载荷", LedColorState.Gray, LedColorState.Red, LedColorState.Green, LedColorState.Blue })
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
        var def = NodeDefinitionRegistry.Resolve(DemoNodeKinds.SyringePump);
        var node = new NodeViewModel(DemoNodeKinds.SyringePump);
        Assert.False(def.InvokeEvent(node, NodeEventGesture.Click, "Any"));
        Assert.False(def.InvokeEvent(node, NodeEventGesture.DoubleClick, "Any"));
    }
}
