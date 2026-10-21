using System.Windows;
using System.Windows.Media;

namespace FlowChartKit.Rendering;

/// <summary>共享端口规格：常规四端口（上下左右中点），泵模块以外的内置部件定义共用。</summary>
internal static class NodePorts
{
    public static IReadOnlyList<PortSpec> Standard { get; } =
    [
        new PortSpec("T", 0.5, 0, PortDir.Up),
        new PortSpec("B", 0.5, 1, PortDir.Down),
        new PortSpec("L", 0,   0.5, PortDir.Left),
        new PortSpec("R", 1,   0.5, PortDir.Right),
    ];
}

public sealed class SampleCupDefinition : INodeDefinition
{
    public NodeKind Kind => NodeKind.SampleCup;
    public string DisplayName => "样品杯";
    public (double W, double H) BaseSize => (46, 64);
    public IReadOnlyList<PortSpec> Ports => NodePorts.Standard;

    public void Render(DrawingContext dc)
    {
        NodeRenderer.DrawPath(dc, "M8,6 L8,44 Q8,58 23,58 Q38,58 38,44 L38,6 Z", NodeRenderer.FillLight, NodeRenderer.OutlinePen);
        NodeRenderer.DrawPath(dc, "M14,32 L32,32", null, NodeRenderer.AccentPen);
        NodeRenderer.DrawPath(dc, "M14,42 L32,42", null, NodeRenderer.AccentPen);
    }
}

public sealed class ReagentBottleDefinition : INodeDefinition
{
    public NodeKind Kind => NodeKind.ReagentBottle;
    public string DisplayName => "试剂瓶";
    public (double W, double H) BaseSize => (56, 58);
    public IReadOnlyList<PortSpec> Ports => NodePorts.Standard;

    public void Render(DrawingContext dc)
    {
        NodeRenderer.DrawPath(dc, "M22,4 L22,16 L10,48 Q10,56 18,56 L38,56 Q46,56 46,48 L34,16 L34,4 Z", NodeRenderer.FillLight, NodeRenderer.OutlinePen);
        NodeRenderer.DrawPath(dc, "M15,42 L41,42", null, NodeRenderer.AccentPen);
    }
}

public sealed class WasteDefinition : INodeDefinition
{
    public NodeKind Kind => NodeKind.Waste;
    public string DisplayName => "废液";
    public (double W, double H) BaseSize => (64, 76);
    public IReadOnlyList<PortSpec> Ports => NodePorts.Standard;

    public void Render(DrawingContext dc)
    {
        NodeRenderer.DrawRoundRect(dc, 22, 0, 20, 10, 2, NodeRenderer.FillLight, NodeRenderer.OutlinePen);
        NodeRenderer.DrawRoundRect(dc, 8, 10, 48, 62, 6, NodeRenderer.FillLight, NodeRenderer.OutlinePen);
        NodeRenderer.DrawPath(dc, "M14,38 q5,-6 10,0 t10,0 t10,0", null, NodeRenderer.AccentPen14);
        NodeRenderer.DrawPath(dc, "M14,52 q5,-6 10,0 t10,0 t10,0", null, NodeRenderer.AccentPen14);
    }
}

public sealed class ReactionCupDefinition : INodeDefinition
{
    public NodeKind Kind => NodeKind.ReactionCup;
    public string DisplayName => "反应杯";
    public (double W, double H) BaseSize => (56, 80);
    public IReadOnlyList<PortSpec> Ports => NodePorts.Standard;

    public void Render(DrawingContext dc)
    {
        NodeRenderer.DrawRoundRect(dc, 16, 6, 24, 68, 4, NodeRenderer.FillLight, NodeRenderer.OutlinePen);
        NodeRenderer.DrawPath(dc, "M20,18 L36,18", null, NodeRenderer.AccentPen);
    }
}

public sealed class CuvetteDefinition : INodeDefinition
{
    public NodeKind Kind => NodeKind.Cuvette;
    public string DisplayName => "2cm比色皿";
    public (double W, double H) BaseSize => (46, 74);
    public IReadOnlyList<PortSpec> Ports => NodePorts.Standard;

    // "G" 字样：一次性转为矢量轮廓并冻结——分辨率无关（高 DPI 不糊），且免每次渲染构造 FormattedText
    private static readonly Geometry s_glyph = NodeRenderer.BuildGlyph("G", 12.0, new Point(18, 28));

    public void Render(DrawingContext dc)
    {
        NodeRenderer.DrawRoundRect(dc, 16, 2, 14, 10, 1, NodeRenderer.FillLight, NodeRenderer.OutlinePen);
        NodeRenderer.DrawRoundRect(dc, 8, 12, 30, 56, 2, NodeRenderer.FillLight, NodeRenderer.OutlinePen);
        dc.DrawGeometry(NodeRenderer.Outline, null, s_glyph);
    }
}

public sealed class CentrifugeDefinition : INodeDefinition
{
    public NodeKind Kind => NodeKind.Centrifuge;
    public string DisplayName => "离心位";
    public (double W, double H) BaseSize => (56, 80);
    public IReadOnlyList<PortSpec> Ports => NodePorts.Standard;

    public void Render(DrawingContext dc)
    {
        NodeRenderer.DrawRoundRect(dc, 8, 4, 40, 72, 6, NodeRenderer.FillLight, NodeRenderer.OutlinePen);
        NodeRenderer.DrawPath(dc, "M8,22 L48,22", null, NodeRenderer.OutlinePen14);
        dc.DrawEllipse(NodeRenderer.FillWhite, NodeRenderer.AccentPen14, new Point(28, 48), 8, 8);
    }
}

public sealed class NeedleWashDefinition : INodeDefinition
{
    public NodeKind Kind => NodeKind.NeedleWash;
    public string DisplayName => "洗针位";
    public (double W, double H) BaseSize => (46, 84);
    public IReadOnlyList<PortSpec> Ports => NodePorts.Standard;

    public void Render(DrawingContext dc)
    {
        NodeRenderer.DrawPath(dc, "M23,10 L23,46", null, NodeRenderer.AccentPen2);
        NodeRenderer.DrawRoundRect(dc, 15, 2, 16, 8, 2, NodeRenderer.FillLight, NodeRenderer.OutlinePen);
        NodeRenderer.DrawRoundRect(dc, 8, 46, 30, 32, 4, NodeRenderer.FillLight, NodeRenderer.OutlinePen);
    }
}

public sealed class ThreeWayValveDefinition : INodeDefinition
{
    public NodeKind Kind => NodeKind.ThreeWayValve;
    public string DisplayName => "三通阀";
    public (double W, double H) BaseSize => (56, 52);
    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec("L", 0,   0.5, PortDir.Left),
        new PortSpec("R", 1,   0.5, PortDir.Right),
        new PortSpec("B", 0.5, 1,   PortDir.Down),
    ];

    public void Render(DrawingContext dc)
    {
        // 圆形阀体
        dc.DrawEllipse(NodeRenderer.FillWhite, NodeRenderer.OutlinePen, new Point(28, 26), 16, 16);
        // 内部 Y 形流道指示（左/右/下三个端口方向）
        NodeRenderer.DrawPath(dc, "M14,26 L28,26 L42,26", null, NodeRenderer.AccentPen);
        NodeRenderer.DrawPath(dc, "M28,26 L28,40", null, NodeRenderer.AccentPen);
    }
}

public sealed class PumpModuleDefinition : INodeDefinition
{
    public NodeKind Kind => NodeKind.PumpModule;
    public string DisplayName => "进样注射泵";
    public (double W, double H) BaseSize => (130, 320);

    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec("T",  0.50, 0.02, PortDir.Up),
        new PortSpec("L1", 0.00, 0.10, PortDir.Left),
        new PortSpec("L2", 0.00, 0.24, PortDir.Left),
        new PortSpec("R1", 1.00, 0.10, PortDir.Right),
        new PortSpec("R2", 1.00, 0.24, PortDir.Right),
        new PortSpec("B1", 0.30, 1.00, PortDir.Down),
        new PortSpec("B2", 0.45, 1.00, PortDir.Down),
        new PortSpec("B3", 0.60, 1.00, PortDir.Down),
        new PortSpec("B4", 0.75, 1.00, PortDir.Down),
    ];

    public void Render(DrawingContext dc)
    {
        // 外壳
        NodeRenderer.DrawRoundRect(dc, 2, 2, 126, 316, 18, NodeRenderer.FillLight, NodeRenderer.OutlinePen);
        // 多通阀：外圈
        dc.DrawEllipse(NodeRenderer.FillWhite, NodeRenderer.OutlinePen, new Point(65, 64), 30, 30);
        // 8 个接口
        for (int i = 0; i < 8; i++)
        {
            double a = i * Math.PI / 4;
            double cx = 65 + 44 * Math.Cos(a);
            double cy = 64 + 44 * Math.Sin(a);
            dc.DrawEllipse(NodeRenderer.FillLight, NodeRenderer.OutlinePen14, new Point(cx, cy), 6, 6);
        }
        // 内芯
        dc.DrawEllipse(NodeRenderer.PumpCoreFill, NodeRenderer.OutlinePen14, new Point(65, 64), 14, 14);
        // 注射器
        NodeRenderer.DrawRoundRect(dc, 50, 130, 30, 100, 4, NodeRenderer.FillWhite, NodeRenderer.OutlinePen);
        NodeRenderer.DrawPath(dc, "M50,160 L80,160", null, NodeRenderer.AccentPen);
        NodeRenderer.DrawRoundRect(dc, 61, 230, 8, 56, 2, NodeRenderer.PlungerFill, NodeRenderer.OutlinePen12);
        NodeRenderer.DrawRoundRect(dc, 44, 286, 42, 14, 3, NodeRenderer.FillLight, NodeRenderer.OutlinePen);
    }
}

/// <summary>
/// LED 指示灯（内置）：圆形灯座 + 状态色灯泡 + 高光。State 即颜色，用 <see cref="LedDefinition"/>
/// 的公开常量约定（Gray/Red/Green/Blue，默认 Gray）；双击循环切换四色，
/// 由定义经 <see cref="INodeDefinition.InvokeEvent"/> 自消费，宿主零代码开箱即用。
/// 宿主也可经 SetNodeStateByNumberCommand 程序化设置（传任意常量值或自定义载荷——未匹配时按 Gray 渲染）。
/// </summary>
public sealed class LedDefinition : INodeDefinition
{
    /// <summary>State 载荷的颜色约定值（宿主程序化设置时与定义保持一致）。</summary>
    public const string StateGray = "Gray";
    public const string StateRed = "Red";
    public const string StateGreen = "Green";
    public const string StateBlue = "Blue";

    /// <summary>双击循环顺序：灰 → 红 → 绿 → 蓝 → 灰。</summary>
    public static readonly IReadOnlyList<string> ColorCycle = [StateGray, StateRed, StateGreen, StateBlue];

    private static readonly Brush s_bezelFill = Frozen(0xC9, 0xD2, 0xDF);   // 金属灯座
    private static readonly Brush s_grayBulb = Frozen(0xB2, 0xB9, 0xC6);    // 灰（熄灭）
    private static readonly Brush s_redBulb = Frozen(0xE7, 0x4C, 0x3C);
    private static readonly Brush s_greenBulb = Frozen(0x2E, 0xCC, 0x71);
    private static readonly Brush s_blueBulb = Frozen(0x3D, 0x8B, 0xD9);
    private static readonly Brush s_gloss = Frozen(0xFF, 0xFF, 0xFF, 0x8C); // 半透明白高光

    private static Brush Frozen(byte r, byte g, byte b, byte a = 0xFF)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    public NodeKind Kind => NodeKind.Led;
    public string DisplayName => "指示灯";
    public (double W, double H) BaseSize => (32, 32);

    // 指示灯同样接入流路：常规四端口（上下左右）
    public IReadOnlyList<PortSpec> Ports => NodePorts.Standard;

    public IReadOnlyList<NodeEventSpec> Events { get; } =
    [
        new(NodeEventGesture.DoubleClick, "ToggleColor", "切换颜色"),
    ];

    public bool InvokeEvent(ViewModels.NodeViewModel node, NodeEventGesture gesture, string eventId)
    {
        if (gesture != NodeEventGesture.DoubleClick || eventId != "ToggleColor")
            return false;

        // 循环取下一色；当前值不在约定内（宿主自定义载荷）时视为灰，从头开始
        var current = node.State as string;
        var index = IndexOfColor(current);
        node.State = ColorCycle[(index + 1) % ColorCycle.Count];
        return true;
    }

    public void Render(DrawingContext dc) => RenderBulb(dc, StateGray);

    public void Render(DrawingContext dc, ViewModels.NodeViewModel node)
        => RenderBulb(dc, node?.State as string);

    private void RenderBulb(DrawingContext dc, string? state)
    {
        var center = new Point(16, 16);
        // 金属灯座
        dc.DrawEllipse(s_bezelFill, NodeRenderer.OutlinePen, center, 14, 14);
        // 灯泡（未匹配的 State 载荷按灰渲染）
        var bulb = state switch
        {
            StateRed => s_redBulb,
            StateGreen => s_greenBulb,
            StateBlue => s_blueBulb,
            _ => s_grayBulb,
        };
        dc.DrawEllipse(bulb, NodeRenderer.OutlinePen14, center, 10, 10);
        // 左上高光，营造玻璃质感
        dc.DrawEllipse(s_gloss, null, new Point(11.5, 11.5), 3.2, 2.4);
    }

    private static int IndexOfColor(string? state) => state switch
    {
        StateRed => 1,
        StateGreen => 2,
        StateBlue => 3,
        _ => 0,
    };
}
