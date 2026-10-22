using System.Collections.Concurrent;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using FlowChartKit;

namespace FlowChartKit.Rendering;

/// <summary>
/// 部件绘制入口与共享绘制工具：外观由 <see cref="NodeDefinitionRegistry"/> 解析出的
/// <see cref="INodeDefinition"/> 自管理，本类提供冻结画刷/画笔与几何缓存供各定义复用。
/// 颜色风格与仪器流路图一致：描边蓝、浅色填充。
/// </summary>
public static class NodeRenderer
{
    public static readonly Brush Outline = Frozen(0x4A, 0x78, 0xC2);
    public static readonly Brush FillLight = Frozen(0xEA, 0xF0, 0xFB);
    public static readonly Brush FillWhite = Frozen(0xF7, 0xFA, 0xFF);
    public static readonly Brush Accent = Frozen(0x2F, 0x5F, 0xA8);
    /// <summary>泵体阀芯填充色（浅蓝），自定义泵类部件可与内置风格保持一致。</summary>
    public static readonly Brush PumpCoreFill = Frozen(0xCF, 0xE0, 0xF6);
    /// <summary>柱塞填充色（中蓝灰），用于泵的推杆等内部结构。</summary>
    public static readonly Brush PlungerFill = Frozen(0xB9, 0xCD, 0xE9);

    /// <summary>共享描边画笔：1.6px 描边蓝；自定义部件建议优先复用（已冻结，跨实例共享）。</summary>
    public static readonly Pen OutlinePen = MakePen(Outline, 1.6);
    /// <summary>共享描边画笔：1.4px，用于阀体等中等轮廓。</summary>
    public static readonly Pen OutlinePen14 = MakePen(Outline, 1.4);
    /// <summary>共享描边画笔：1.2px，用于细节线条与端口标记。</summary>
    public static readonly Pen OutlinePen12 = MakePen(Outline, 1.2);
    /// <summary>共享强调画笔：1.2px 强调蓝，用于流道/内部线条。</summary>
    public static readonly Pen AccentPen = MakePen(Accent, 1.2);
    /// <summary>共享强调画笔：1.4px 强调蓝，用于较粗的流道指示。</summary>
    public static readonly Pen AccentPen14 = MakePen(Accent, 1.4);
    /// <summary>共享强调画笔：2.0px 强调蓝，用于最粗的流道指示。</summary>
    public static readonly Pen AccentPen2 = MakePen(Accent, 2.0);

    /// <summary>端口吸附高亮笔：2.0px 橙色虚线；宿主自定义高亮（如自定义吸附提示）可复用。</summary>
    public static readonly Pen HighlightPen = CreateHighlightPen();

    /// <summary>按 RGB 创建冻结的纯色画刷（可跨实例共享）；自定义部件可用它构造自己的配色。</summary>
    public static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    /// <summary>创建冻结的圆头圆角画笔（可跨实例共享）；自定义部件可用它基于自己的画刷生成统一风格的笔。</summary>
    public static Pen MakePen(Brush brush, double thickness)
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

    /// <summary>按部件类型绘制外观：委托给 <see cref="NodeDefinitionRegistry"/> 解析出的部件定义。</summary>
    public static void Render(DrawingContext dc, NodeKind kind)
        => NodeDefinitionRegistry.Resolve(kind).Render(dc);

    /// <summary>带部件上下文绘制：定义可按 <see cref="ViewModels.NodeViewModel.State"/> 区分外观。</summary>
    public static void Render(DrawingContext dc, ViewModels.NodeViewModel node)
        => NodeDefinitionRegistry.Resolve(node.Kind).Render(dc, node);

    /// <summary>在指定中心绘制端口标记（浅色圆点 + 细描边），编辑模式下常显。</summary>
    public static void RenderPort(DrawingContext dc, double cx, double cy)
        => dc.DrawEllipse(FillWhite, OutlinePen12, new Point(cx, cy), 3.2, 3.2);

    /// <summary>在指定中心绘制端口吸附高亮圈（橙色虚线环），拖动端点靠近时叠加在标记外。</summary>
    public static void RenderPortHighlight(DrawingContext dc, double cx, double cy)
        => dc.DrawEllipse(null, HighlightPen, new Point(cx, cy), 8, 8);

    // 路径数据都是编译期常量：解析一次并冻结，跨部件实例复用，避免每次渲染重新 Parse
    // 用 ConcurrentDictionary 是为了将来若有后台预渲染也能安全访问（UI 线程渲染为主，非必需但更稳妥）
    private static readonly ConcurrentDictionary<string, Geometry> s_pathCache = new();

    private static Geometry CachedPath(string data)
    {
        if (s_pathCache.TryGetValue(data, out var geo)) return geo;
        geo = Geometry.Parse(data);
        geo.Freeze();
        return s_pathCache.GetOrAdd(data, geo);
    }

    // 圆角矩形参数同样是编译期常量：按参数缓存并冻结，与 CachedPath 同理
    private static readonly ConcurrentDictionary<(double X, double Y, double W, double H, double R), Geometry> s_roundRectCache = new();

    private static Geometry CachedRoundRect(double x, double y, double w, double h, double r)
    {
        var key = (x, y, w, h, r);
        if (s_roundRectCache.TryGetValue(key, out var geo)) return geo;
        geo = new RectangleGeometry(new Rect(x, y, w, h), r, r);
        geo.Freeze();
        return s_roundRectCache.GetOrAdd(key, geo);
    }

    /// <summary>按 SVG 风格路径数据绘制几何；路径解析结果按 data 缓存并冻结，重复渲染零开销。</summary>
    public static void DrawPath(DrawingContext dc, string data, Brush? fill, Pen pen)
        => dc.DrawGeometry(fill, pen, CachedPath(data));

    /// <summary>绘制圆角矩形；按 (x,y,w,h,r) 缓存并冻结，供自定义部件的底座、面板等复用。</summary>
    public static void DrawRoundRect(DrawingContext dc, double x, double y, double w, double h, double r, Brush? fill, Pen pen)
        => dc.DrawGeometry(fill, pen, CachedRoundRect(x, y, w, h, r));

    /// <summary>把短文本一次性转成矢量轮廓并冻结：分辨率无关（高 DPI 不糊），供各定义静态缓存复用。</summary>
    public static Geometry BuildGlyph(string text, double size, Point origin)
    {
        var ft = new FormattedText(
            text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Arial"), size, Brushes.Black, 1.0);
        var geo = ft.BuildGeometry(origin);
        geo.Freeze();
        return geo;
    }
}
