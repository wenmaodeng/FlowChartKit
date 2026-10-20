using System.Globalization;
using System.Windows;
using System.Windows.Media;
using FlowChartKit;

namespace FlowChartKit.Rendering;

/// <summary>
/// 按部件类型直接向 DrawingContext 绘制矢量图形（不创建 FrameworkElement）。
/// 颜色风格与仪器流路图一致：描边蓝、浅色填充。
/// 路径几何按字符串缓存并冻结；外观可通过 <see cref="NodeRendererRegistry"/> 整体替换。
/// </summary>
public static class NodeRenderer
{
    public static readonly Brush Outline = Frozen(0x4A, 0x78, 0xC2);
    public static readonly Brush FillLight = Frozen(0xEA, 0xF0, 0xFB);
    public static readonly Brush FillWhite = Frozen(0xF7, 0xFA, 0xFF);
    public static readonly Brush Accent = Frozen(0x2F, 0x5F, 0xA8);
    private static readonly Brush PumpCoreFill = Frozen(0xCF, 0xE0, 0xF6);
    private static readonly Brush PlungerFill = Frozen(0xB9, 0xCD, 0xE9);

    private static readonly Pen OutlinePen = MakePen(Outline, 1.6);
    private static readonly Pen OutlinePen14 = MakePen(Outline, 1.4);
    private static readonly Pen OutlinePen12 = MakePen(Outline, 1.2);
    private static readonly Pen AccentPen = MakePen(Accent, 1.2);
    private static readonly Pen AccentPen14 = MakePen(Accent, 1.4);
    private static readonly Pen AccentPen2 = MakePen(Accent, 2.0);

    private static readonly Pen HighlightPen = CreateHighlightPen();

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static Pen MakePen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness);
        pen.LineJoin = PenLineJoin.Round;
        pen.StartLineCap = PenLineCap.Round;
        pen.EndLineCap = PenLineCap.Round;
        pen.Freeze();
        return pen;
    }

    private static Pen CreateHighlightPen()
    {
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x2B)), 2.0);
        pen.DashStyle = new DashStyle(new double[] { 2, 2 }, 0);
        pen.Freeze();
        return pen;
    }

    /// <summary>按部件类型绘制外观；优先取 <see cref="NodeRendererRegistry"/> 中注册的渲染器，未注册时用内置绘制。</summary>
    public static void Render(DrawingContext dc, NodeKind kind)
    {
        if (NodeRendererRegistry.Resolve(kind) is { } custom)
        {
            custom.Render(dc);
            return;
        }

        switch (kind)
        {
            case NodeKind.SampleCup: DrawSampleCup(dc); break;
            case NodeKind.ReagentBottle: DrawReagentBottle(dc); break;
            case NodeKind.Waste: DrawWaste(dc); break;
            case NodeKind.ReactionCup: DrawReactionCup(dc); break;
            case NodeKind.Cuvette: DrawCuvette(dc); break;
            case NodeKind.Centrifuge: DrawCentrifuge(dc); break;
            case NodeKind.NeedleWash: DrawNeedleWash(dc); break;
            case NodeKind.PumpModule: DrawPump(dc); break;
        }
    }

    /// <summary>在指定中心绘制端口标记（浅色圆点 + 细描边），编辑模式下常显。</summary>
    public static void RenderPort(DrawingContext dc, double cx, double cy)
        => dc.DrawEllipse(FillWhite, OutlinePen12, new Point(cx, cy), 3.2, 3.2);

    /// <summary>在指定中心绘制端口吸附高亮圈（橙色虚线环），拖动端点靠近时叠加在标记外。</summary>
    public static void RenderPortHighlight(DrawingContext dc, double cx, double cy)
        => dc.DrawEllipse(null, HighlightPen, new Point(cx, cy), 8, 8);

    // 路径数据都是编译期常量：解析一次并冻结，跨部件实例复用，避免每次渲染重新 Parse
    private static readonly Dictionary<string, Geometry> s_pathCache = new();

    private static Geometry CachedPath(string data)
    {
        if (!s_pathCache.TryGetValue(data, out var geo))
        {
            geo = Geometry.Parse(data);
            geo.Freeze();
            s_pathCache[data] = geo;
        }
        return geo;
    }

    private static void DrawPath(DrawingContext dc, string data, Brush? fill, Pen pen)
        => dc.DrawGeometry(fill, pen, CachedPath(data));

    private static void DrawRoundRect(DrawingContext dc, double x, double y, double w, double h, double r, Brush? fill, Pen pen)
        => dc.DrawGeometry(fill, pen, new RectangleGeometry(new Rect(x, y, w, h), r, r));

    private static void DrawSampleCup(DrawingContext dc)
    {
        DrawPath(dc, "M8,6 L8,44 Q8,58 23,58 Q38,58 38,44 L38,6 Z", FillLight, OutlinePen);
        DrawPath(dc, "M14,32 L32,32", null, AccentPen);
        DrawPath(dc, "M14,42 L32,42", null, AccentPen);
    }

    private static void DrawReagentBottle(DrawingContext dc)
    {
        DrawPath(dc, "M22,4 L22,16 L10,48 Q10,56 18,56 L38,56 Q46,56 46,48 L34,16 L34,4 Z", FillLight, OutlinePen);
        DrawPath(dc, "M15,42 L41,42", null, AccentPen);
    }

    private static void DrawWaste(DrawingContext dc)
    {
        DrawRoundRect(dc, 22, 0, 20, 10, 2, FillLight, OutlinePen);
        DrawRoundRect(dc, 8, 10, 48, 62, 6, FillLight, OutlinePen);
        DrawPath(dc, "M14,38 q5,-6 10,0 t10,0 t10,0", null, AccentPen14);
        DrawPath(dc, "M14,52 q5,-6 10,0 t10,0 t10,0", null, AccentPen14);
    }

    private static void DrawReactionCup(DrawingContext dc)
    {
        DrawRoundRect(dc, 16, 6, 24, 68, 4, FillLight, OutlinePen);
        DrawPath(dc, "M20,18 L36,18", null, AccentPen);
    }

    // 比色皿的 "G" 字样：FormattedText 可跨渲染复用，无需每帧构造
    private static readonly FormattedText CuvetteText = new(
        "G", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
        new Typeface("Arial"), 12.0, Outline, 1.0);

    private static void DrawCuvette(DrawingContext dc)
    {
        DrawRoundRect(dc, 16, 2, 14, 10, 1, FillLight, OutlinePen);
        DrawRoundRect(dc, 8, 12, 30, 56, 2, FillLight, OutlinePen);
        dc.DrawText(CuvetteText, new Point(18, 28));
    }

    private static void DrawCentrifuge(DrawingContext dc)
    {
        DrawRoundRect(dc, 8, 4, 40, 72, 6, FillLight, OutlinePen);
        DrawPath(dc, "M8,22 L48,22", null, OutlinePen14);
        dc.DrawEllipse(FillWhite, AccentPen14, new Point(28, 48), 8, 8);
    }

    private static void DrawNeedleWash(DrawingContext dc)
    {
        DrawPath(dc, "M23,10 L23,46", null, AccentPen2);
        DrawRoundRect(dc, 15, 2, 16, 8, 2, FillLight, OutlinePen);
        DrawRoundRect(dc, 8, 46, 30, 32, 4, FillLight, OutlinePen);
    }

    private static void DrawPump(DrawingContext dc)
    {
        // 外壳
        DrawRoundRect(dc, 2, 2, 126, 316, 18, FillLight, OutlinePen);
        // 多通阀：外圈
        dc.DrawEllipse(FillWhite, OutlinePen, new Point(65, 64), 30, 30);
        // 8 个接口
        for (int i = 0; i < 8; i++)
        {
            double a = i * Math.PI / 4;
            double cx = 65 + 44 * Math.Cos(a);
            double cy = 64 + 44 * Math.Sin(a);
            dc.DrawEllipse(FillLight, OutlinePen14, new Point(cx, cy), 6, 6);
        }
        // 内芯
        dc.DrawEllipse(PumpCoreFill, OutlinePen14, new Point(65, 64), 14, 14);
        // 注射器
        DrawRoundRect(dc, 50, 130, 30, 100, 4, FillWhite, OutlinePen);
        DrawPath(dc, "M50,160 L80,160", null, AccentPen);
        DrawRoundRect(dc, 61, 230, 8, 56, 2, PlungerFill, OutlinePen12);
        DrawRoundRect(dc, 44, 286, 42, 14, 3, FillLight, OutlinePen);
    }
}
