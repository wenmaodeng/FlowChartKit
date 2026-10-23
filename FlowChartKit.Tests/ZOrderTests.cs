using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using FlowChartKit;
using FlowChartKit.Data;
using FlowChartKit.ViewModels;
using Xunit;

namespace FlowChartKit.Tests;

/// <summary>
/// 叠放次序（Z 序）：管路 / 文本 / 部件在同一画布层按各自的 Z 排布，可置于顶层 / 底层、
/// 上移 / 下移一层，或精确置于某个元素之上 / 之下；Z 随存档持久化。
/// </summary>
public class ZOrderTests
{
    private static DiagramViewModel NewDiagram(out NodeViewModel node,
        out TextBlockViewModel text, out ConnectionViewModel conn)
    {
        var d = new DiagramViewModel(800, 600);
        node = d.AddNode(NodeKind.Led, 10, 10, "灯");
        text = d.AddTextItem(20, 20, "注释");
        conn = d.AddFreeConnection(new Point(0, 0), new Point(50, 50));
        return d;
    }

    [Fact]
    public void Default_ZIsZero_AndBaselineOrderIsPipeTextNode()
    {
        var d = NewDiagram(out var node, out var text, out var conn);

        Assert.Equal(0, node.Z);
        Assert.Equal(0, text.Z);
        Assert.Equal(0, conn.Z);

        // 基线（Z 相同时）：管路最底 → 文本 → 部件最顶。
        // 文本"上移一层"只会越过紧邻其上的部件，故文本应到部件之上、管路仍在最底
        d.BringForward(text);

        Assert.True(text.Z > node.Z);
        Assert.True(node.Z > conn.Z);
    }

    [Fact]
    public void BringToFront_PutsTextAboveNode()
    {
        var d = NewDiagram(out var node, out var text, out var conn);

        d.BringToFront(text);

        Assert.True(text.Z > node.Z);
        Assert.True(text.Z > conn.Z);
    }

    [Fact]
    public void SendToBack_PutsNodeBelowPipe()
    {
        var d = NewDiagram(out var node, out var text, out var conn);

        d.SendToBack(node);

        Assert.True(node.Z < conn.Z);
        Assert.True(node.Z < text.Z);
    }

    [Fact]
    public void BringForward_And_SendBackward_AreContiguous()
    {
        var d = NewDiagram(out var node, out var text, out var conn);

        // 顶层的部件再上移无效（Z 不变）
        int topZ = node.Z;
        d.BringForward(node);
        Assert.Equal(topZ, node.Z);

        // 底层的管路再下移无效
        d.SendToBack(text);
        d.SendBackward(text);
        Assert.True(text.Z < conn.Z);
        int bottomZ = text.Z;
        d.SendBackward(text);
        Assert.Equal(bottomZ, text.Z);
    }

    [Fact]
    public void MoveAbove_MoveBelow_AcrossKinds()
    {
        var d = NewDiagram(out var node, out var text, out var conn);

        // 文本置于部件之上（紧邻）
        Assert.True(d.MoveAbove(text, node));
        Assert.True(text.Z > node.Z);

        // 部件置于管路之下（紧邻）
        Assert.True(d.MoveBelow(node, conn));
        Assert.True(node.Z < conn.Z);
    }

    [Fact]
    public void MoveAbove_InvalidTargets_ReturnFalse()
    {
        var d = NewDiagram(out var node, out var text, out _);

        Assert.False(d.MoveAbove(text, text));        // 自身
        Assert.False(d.MoveAbove(text, new object())); // 非画布元素
        Assert.False(d.MoveAbove(new DiagramViewModel(100, 100).AddNode(NodeKind.Led, 0, 0), node)); // 它图的元素

        // 无效操作不改变次序
        Assert.Equal(0, text.Z);
        Assert.Equal(0, node.Z);
    }

    [Fact]
    public void SameKind_SwapOrderByZ()
    {
        var d = new DiagramViewModel(800, 600);
        var a = d.AddNode(NodeKind.Led, 10, 10, "A");
        var b = d.AddNode(NodeKind.Led, 20, 20, "B");

        // 同类型：默认后添加的在上
        d.BringToFront(a);
        Assert.True(a.Z > b.Z);

        d.SendToBack(a);
        Assert.True(a.Z < b.Z);
    }

    [Fact]
    public void JsonRoundTrip_PreservesZ()
    {
        var d = NewDiagram(out var node, out var text, out var conn);
        d.BringToFront(text);

        int nodeZ = node.Z, textZ = text.Z, connZ = conn.Z;
        var restored = DiagramViewModel.FromData(
            DiagramSerializer.FromJson(DiagramSerializer.ToJson(d.ToData()))!)!;

        Assert.Equal(textZ, restored.Texts[0].Z);
        Assert.Equal(nodeZ, restored.Nodes[0].Z);
        Assert.Equal(connZ, restored.Connections[0].Z);
        // 文本仍在部件之上
        Assert.True(restored.Texts[0].Z > restored.Nodes[0].Z);
    }

    [Fact]
    public void LegacyJson_WithoutZ_LoadsZero_AndKeepsBaseline()
    {
        const string legacy = """
        {
          "Version": 1,
          "Width": 800,
          "Height": 600,
          "Nodes": [ { "Id": "n1", "Kind": 0, "X": 10, "Y": 10 } ],
          "Connections": [],
          "Texts": [ { "Id": "t1", "Number": 1, "X": 20, "Y": 20, "Content": "注释" } ]
        }
        """;

        var restored = DiagramViewModel.FromData(DiagramSerializer.FromJson(legacy)!)!;

        Assert.Equal(0, restored.Nodes[0].Z);
        Assert.Equal(0, restored.Texts[0].Z);

        // 缺省层级仍然生效：文本上移一层即越过部件
        restored.BringForward(restored.Texts[0]);
        Assert.True(restored.Texts[0].Z > restored.Nodes[0].Z);
    }

    [Fact]
    public void CanvasItems_ContainsAllThreeKinds()
    {
        RunOnSta(() =>
        {
            var d = NewDiagram(out _, out _, out _);

            // 统一画布层：经 ItemsControl 的视图看，三类元素都在其中（前后关系由 Z 决定，
            // 声明顺序只是 Z 相同时的兜底；CompositeCollection 自身枚举的是容器，故走 Items 校验）
            Assert.Equal(
                d.Connections.Count + d.Texts.Count + d.Nodes.Count,
                d.CanvasItems.Count);

            var items = new System.Windows.Controls.ItemsControl { ItemsSource = d.CanvasItems }
                .Items.Cast<object>().ToList();

            Assert.Single(items.OfType<ConnectionViewModel>());
            Assert.Single(items.OfType<TextBlockViewModel>());
            Assert.Single(items.OfType<NodeViewModel>());
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null)
            ExceptionDispatchInfo.Capture(error).Throw();
    }
}
