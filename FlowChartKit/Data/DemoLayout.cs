using FlowChartKit.ViewModels;

namespace FlowChartKit.Data;

/// <summary>
/// 内置示例流路图：一台进样注射泵 + 多通阀，外接样品/洗针/比色/废液/反应/离心位与 8 个试剂瓶。
/// 供画布控件在无存档文件时生成初始布局（右键菜单"恢复示例"也走这里）。
/// </summary>
public static class DemoLayout
{
    /// <summary>向已有图实例填充示例内容（不清空，调用方需自行 <c>Clear()</c>）。</summary>
    public static void Build(DiagramViewModel d)
    {
        var pump = d.AddNode(NodeKind.PumpModule, 300, 60, "进样注射泵");
        var sample = d.AddNode(NodeKind.SampleCup, 60, 110);
        var wash = d.AddNode(NodeKind.NeedleWash, 55, 290);
        var cuvette = d.AddNode(NodeKind.Cuvette, 55, 500);
        var waste = d.AddNode(NodeKind.Waste, 240, 520);
        var reaction = d.AddNode(NodeKind.ReactionCup, 400, 520);
        d.AddNode(NodeKind.Centrifuge, 560, 520);

        NodeViewModel B(double x, double y, string label) => d.AddNode(NodeKind.ReagentBottle, x, y, label);
        var r1 = B(660, 40, "纳氏试剂");
        var r2 = B(780, 40, "酒石酸钾钠");
        var r3 = B(900, 40, "4-氨基安替比林");
        var r4 = B(1020, 40, "硝普钠");
        var r5 = B(660, 210, "纯水");
        var r6 = B(780, 210, "缓冲溶液");
        var r7 = B(900, 210, "标液");
        var r8 = B(1020, 210, "蒸馏仪");

        PortViewModel Port(NodeViewModel n, string id)
            => n.Ports.First(p => p.Id == id);

        d.AddConnection(sample, Port(sample, "R"), pump, Port(pump, "L1"), FlowMode.Forward);
        d.AddConnection(pump, Port(pump, "L2"), wash, Port(wash, "R"), FlowMode.Forward);
        d.AddConnection(pump, Port(pump, "B1"), cuvette, Port(cuvette, "R"), FlowMode.Forward);
        d.AddConnection(pump, Port(pump, "B2"), waste, Port(waste, "T"), FlowMode.Forward);
        d.AddConnection(pump, Port(pump, "B3"), reaction, Port(reaction, "T"), FlowMode.Forward);
        foreach (var reagent in new[] { r1, r2, r3, r4 })
            d.AddConnection(reagent, Port(reagent, "T"), pump, Port(pump, "R1"), FlowMode.Forward);
        foreach (var reagent in new[] { r5, r6, r7, r8 })
            d.AddConnection(reagent, Port(reagent, "T"), pump, Port(pump, "R2"), FlowMode.Forward);
    }
}
