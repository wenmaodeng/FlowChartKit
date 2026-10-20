using System.IO;
using System.Windows;
using FlowChartKit;
using FlowChartKit.Data;
using FlowChartKit.ViewModels;
using Xunit;

namespace FlowChartKit.Tests;

public class DiagramSerializerTests
{
    [Fact]
    public void JsonRoundTrip_PreservesTopology()
    {
        var d = new DiagramViewModel(800, 600);
        var a = d.AddNode(NodeKind.PumpModule, 100, 100, "泵");
        var b = d.AddNode(NodeKind.SampleCup, 300, 200, "杯");
        var conn = d.AddConnection(a, a.Ports[0], b, b.Ports[1], FlowMode.Reverse);
        d.AddFreeConnection(new Point(10, 20), new Point(30, 40), FlowMode.None);

        var json = DiagramSerializer.ToJson(d.ToData());
        var data = DiagramSerializer.FromJson(json);
        Assert.NotNull(data);

        var restored = DiagramViewModel.FromData(data!);

        Assert.Equal(d.CanvasWidth, restored.CanvasWidth);
        Assert.Equal(2, restored.Nodes.Count);
        Assert.Equal(2, restored.Connections.Count);

        var ra = restored.Nodes[0];
        var rb = restored.Nodes[1];
        Assert.Equal(a.Id, ra.Id);
        Assert.Equal("泵", ra.Label);
        Assert.Equal(100, ra.X);
        Assert.Equal(200, rb.Y);

        var rc = restored.Connections[0];
        Assert.Equal(conn.Id, rc.Id);
        Assert.Equal(conn.Number, rc.Number);
        Assert.Equal(FlowMode.Reverse, rc.Direction);
        Assert.Equal(ra.Id, rc.SourceNode?.Id);
        Assert.Equal(a.Ports[0].Id, rc.SourcePort?.Id);
        Assert.Equal(rb.Id, rc.TargetNode?.Id);
        Assert.Equal(b.Ports[1].Id, rc.TargetPort?.Id);

        var free = restored.Connections[1];
        Assert.Equal(new Point(10, 20), free.FreeSource);
        Assert.Equal(new Point(30, 40), free.FreeTarget);
        Assert.True(free.Points.Length >= 2); // 加载后已重算折线
    }

    [Fact]
    public void FromJson_EmptyOrWhitespace_ReturnsNull()
    {
        Assert.Null(DiagramSerializer.FromJson(""));
        Assert.Null(DiagramSerializer.FromJson("   "));
    }

    [Fact]
    public void Load_CorruptedJsonFile_ReturnsNull()
    {
        var path = Path.Combine(Path.GetTempPath(), $"flowchart-corrupted-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{ this is not valid json at all [");

            var result = DiagramSerializer.Load(path);
            Assert.Null(result); // 损坏 JSON 不应抛异常
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_CorruptedJsonFile_ReturnsNull()
    {
        var path = Path.Combine(Path.GetTempPath(), $"flowchart-corrupted-async-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, "{ this is not valid json at all [");

            var result = await DiagramSerializer.LoadAsync(path);
            Assert.Null(result); // 损坏 JSON 不应抛异常
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void SaveLoad_FileRoundTrip()
    {
        var d = new DiagramViewModel(640, 480);
        d.AddNode(NodeKind.ReagentBottle, 50, 60, "试剂");

        var path = Path.Combine(Path.GetTempPath(), $"flowchart-test-{Guid.NewGuid():N}.json");
        try
        {
            DiagramSerializer.Save(d.ToData(), path);
            var loaded = DiagramSerializer.Load(path);

            Assert.NotNull(loaded);
            Assert.Single(loaded!.Nodes);
            Assert.Equal("试剂", loaded.Nodes[0].Label);
            Assert.Equal(640, loaded.Width);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
