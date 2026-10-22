using System.Windows;
using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Data;
using FlowChartKit.Rendering;
using FlowChartKit.ViewModels;
using Xunit;

namespace FlowChartKit.Tests;

public class PipeDefinitionTests
{
    // 宿主自定义管路类型：与部件类型同一套常量扩展约定
    private const PipeKind Steam = (PipeKind)1000;

    private sealed class SteamPipeDefinition : IPipeDefinition
    {
        public PipeKind Kind => Steam;
        public string DisplayName => "蒸汽管";
        public Brush Stroke { get; } = NodeRenderer.Frozen(0xD0, 0x3A, 0x2B);
        public double Thickness => 3.5;
        public Brush SelectedStroke { get; } = NodeRenderer.Frozen(0xFF, 0x8A, 0x00);
        public double SelectedThickness => 4.0;
        public PipeFlowSpec Flow { get; } = PipeFlowSpec.Create([6, 3], TimeSpan.FromSeconds(0.3));
    }

    private sealed class StandardReplacement : IPipeDefinition
    {
        public PipeKind Kind => PipeKind.Standard;
        public string DisplayName => "粗管路";
        public Brush Stroke { get; } = Brushes.Black;
        public double Thickness => 6.0;
        public Brush SelectedStroke { get; } = Brushes.Red;
        public double SelectedThickness => 7.0;
        public PipeFlowSpec Flow { get; } = PipeFlowSpec.None; // 不流动
    }

    [Fact]
    public void BuiltIn_Standard_IsAlwaysResolvable()
    {
        var def = PipeDefinitionRegistry.Resolve(PipeKind.Standard);

        Assert.False(string.IsNullOrWhiteSpace(def.DisplayName));
        Assert.True(def.Thickness > 0 && def.SelectedThickness > 0);
        Assert.True(def.Flow.Enabled);
        Assert.Equal(8.0, def.Flow.Cycle);
        Assert.True(PipeDefinitionRegistry.IsBuiltIn(PipeKind.Standard));
        Assert.True(PipeDefinitionRegistry.IsAvailable(PipeKind.Standard));
    }

    [Fact]
    public void Register_OverridesBuiltIn_AndUnregisterFallsBack()
    {
        var custom = new SteamPipeDefinition();
        try
        {
            PipeDefinitionRegistry.Register(custom);

            Assert.Same(custom, PipeDefinitionRegistry.Resolve(Steam));
            Assert.False(PipeDefinitionRegistry.IsBuiltIn(Steam));
            Assert.Contains(custom, PipeDefinitionRegistry.All);
        }
        finally
        {
            Assert.True(PipeDefinitionRegistry.Unregister(Steam));
        }

        // 宿主类型注销后不再可解析（内置层没有该类型）
        Assert.False(PipeDefinitionRegistry.IsAvailable(Steam));
    }

    [Fact]
    public void Register_CanOverrideStandardAppearance()
    {
        try
        {
            PipeDefinitionRegistry.Register(new StandardReplacement());

            Assert.False(PipeDefinitionRegistry.IsBuiltIn(PipeKind.Standard));
            Assert.Equal(6.0, PipeDefinitionRegistry.Resolve(PipeKind.Standard).Thickness);
            Assert.False(PipeDefinitionRegistry.Resolve(PipeKind.Standard).Flow.Enabled);
        }
        finally
        {
            PipeDefinitionRegistry.Unregister(PipeKind.Standard);
        }

        // 注销覆盖后回退内置定义
        Assert.True(PipeDefinitionRegistry.IsBuiltIn(PipeKind.Standard));
        Assert.Equal(2.0, PipeDefinitionRegistry.Resolve(PipeKind.Standard).Thickness);
    }

    [Fact]
    public void FlowSpec_Create_FallsBackOnInvalidInput()
    {
        Assert.Equal(8.0, PipeFlowSpec.Create(null, TimeSpan.FromSeconds(1)).Cycle);
        Assert.Equal(8.0, PipeFlowSpec.Create([], TimeSpan.FromSeconds(1)).Cycle);
        Assert.Equal(9.0, PipeFlowSpec.Create([6, 3], TimeSpan.FromSeconds(0.3)).Cycle);
        Assert.False(PipeFlowSpec.None.Enabled);
        Assert.True(PipeFlowSpec.Default.Enabled);
    }

    [Fact]
    public void Connection_DefaultKind_IsStandard_AndCanBeChanged()
    {
        var d = new DiagramViewModel(800, 600);
        var conn = d.AddFreeConnection(new Point(0, 0), new Point(50, 50));

        Assert.Equal(PipeKind.Standard, conn.PipeKind);

        var raised = 0;
        conn.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ConnectionViewModel.PipeKind)) raised++;
        };
        conn.PipeKind = Steam;
        Assert.Equal(Steam, conn.PipeKind);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void AddConnection_UsesSpecifiedKind()
    {
        var d = new DiagramViewModel(800, 600);
        var a = d.AddNode(NodeKind.Led, 100, 100, "灯");
        var b = d.AddNode(NodeKind.Switch, 300, 100, "开关");

        var conn = d.AddConnection(a, a.Ports[0], b, b.Ports[0], FlowMode.Forward, pipeKind: Steam);
        Assert.Equal(Steam, conn.PipeKind);
    }

    [Fact]
    public void JsonRoundTrip_PreservesPipeKind()
    {
        var d = new DiagramViewModel(800, 600);
        var a = d.AddNode(NodeKind.Led, 100, 100, "灯");
        var b = d.AddNode(NodeKind.Switch, 300, 100, "开关");
        d.AddConnection(a, a.Ports[0], b, b.Ports[0], FlowMode.Forward, pipeKind: Steam);
        d.AddFreeConnection(new Point(10, 20), new Point(30, 40));
        var json = DiagramSerializer.ToJson(d.ToData());

        // 宿主未注册该类型：载入回退内置普通管路
        var restored = DiagramViewModel.FromData(DiagramSerializer.FromJson(json)!);
        Assert.Equal(PipeKind.Standard, restored.Connections[0].PipeKind);
        Assert.Equal(PipeKind.Standard, restored.Connections[1].PipeKind);

        // 注册后往返保留自定义类型
        PipeDefinitionRegistry.Register(new SteamPipeDefinition());
        try
        {
            var restored2 = DiagramViewModel.FromData(DiagramSerializer.FromJson(json)!);
            Assert.Equal(Steam, restored2.Connections[0].PipeKind);
            Assert.Equal(PipeKind.Standard, restored2.Connections[1].PipeKind);
            Assert.Equal(2, restored2.Connections.Count);
        }
        finally
        {
            PipeDefinitionRegistry.Unregister(Steam);
        }
    }

    [Fact]
    public void LegacyJson_WithoutPipeKind_FallsBackToStandard()
    {
        // 旧存档没有 PipeKind 字段：反序列化取默认值 Standard
        const string legacy = """
        {
          "Version": 1,
          "Width": 800,
          "Height": 600,
          "Nodes": [],
          "Connections": [
            { "Id": "c1", "Number": 1, "Direction": "Forward",
              "SourceFreeX": 0, "SourceFreeY": 0,
              "TargetFreeX": 40, "TargetFreeY": 0 }
          ]
        }
        """;

        var restored = DiagramViewModel.FromData(DiagramSerializer.FromJson(legacy)!);
        Assert.Single(restored.Connections);
        Assert.Equal(PipeKind.Standard, restored.Connections[0].PipeKind);
    }
}
