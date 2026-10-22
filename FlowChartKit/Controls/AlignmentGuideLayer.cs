using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FlowChartKit.Rendering;

namespace FlowChartKit.Controls;

/// <summary>
/// 对齐参考线图层：绘制拖动部件时与其它部件对齐产生的捕捉线（虚线）。
/// 覆盖整个画布、不参与命中测试（IsHitTestVisible=False），参考线由图模型的
/// <c>AlignmentGuides</c> 提供，拖动结束清空即自动消失。
/// </summary>
public class AlignmentGuideLayer : Control
{
    private static readonly Pen GuidePen = CreateGuidePen();

    public static readonly DependencyProperty GuidesProperty = DependencyProperty.Register(
        nameof(Guides), typeof(IReadOnlyList<AlignmentGuide>), typeof(AlignmentGuideLayer),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    static AlignmentGuideLayer()
    {
        // 图层始终在最上层且不接收输入：仅作视觉提示
        IsHitTestVisibleProperty.OverrideMetadata(typeof(AlignmentGuideLayer),
            new FrameworkPropertyMetadata(false));
    }

    /// <summary>当前要绘制的对齐参考线；为空时不绘制。</summary>
    public IReadOnlyList<AlignmentGuide> Guides
    {
        get => (IReadOnlyList<AlignmentGuide>)GetValue(GuidesProperty);
        set => SetValue(GuidesProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var guides = Guides;
        if (guides is null || guides.Count == 0) return;

        foreach (var g in guides)
        {
            if (g.IsVertical)
                dc.DrawLine(GuidePen, new Point(g.Offset, g.Start), new Point(g.Offset, g.End));
            else
                dc.DrawLine(GuidePen, new Point(g.Start, g.Offset), new Point(g.End, g.Offset));
        }
    }

    /// <summary>捕捉线样式：1px 橙色虚线，与端口吸附高亮同色系但更细，避免喧宾夺主。</summary>
    private static Pen CreateGuidePen()
    {
        var pen = new Pen(NodeRenderer.Frozen(0xE6, 0x7E, 0x22), 1.0)
        {
            DashStyle = new DashStyle([5, 3], 0)
        };
        pen.Freeze();
        return pen;
    }
}
