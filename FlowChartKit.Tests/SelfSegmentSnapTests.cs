using System.Collections.Generic;
using System.Linq;
using System.Windows;
using FlowChartKit.ViewModels;
using Xunit;

namespace FlowChartKit.Tests;

/// <summary>
/// 管路线段拖动时的**自身段落**对齐：被拖管路自己已有的水平 / 竖直段也参与吸附
/// （可把 Z 形拉成 L 形），但正在拖动的那一段除外。
/// </summary>
public class SelfSegmentSnapTests
{
    /// <summary>管路自身的水平走线（去重后的 Y 值）。</summary>
    private static List<double> HorizontalRuns(ConnectionViewModel conn)
    {
        var pts = conn.Points;
        var runs = new List<double>();
        for (int i = 1; i < pts.Length; i++)
        {
            if (Math.Abs(pts[i - 1].Y - pts[i].Y) >= 0.01) continue;
            if (!runs.Any(y => Math.Abs(y - pts[i].Y) < 0.01)) runs.Add(pts[i].Y);
        }
        return runs;
    }

    [Fact]
    public void HorizontalSegment_AlignsWithOwnOtherRun()
    {
        var d = new DiagramViewModel(1000, 1000);
        var conn = d.AddFreeConnection(new Point(100, 100), new Point(500, 400));

        // 中间水平段挪到 Y=250：自身此时应存在三条水平走线（起点侧 / 中间 / 终点侧）
        conn.SetDetour(null, 250);
        var runs = HorizontalRuns(conn);
        Assert.True(runs.Count >= 3, $"水平走线数 = {runs.Count}");

        // 起点侧与终点侧走线（排除中间的 250）
        var others = runs.Where(y => Math.Abs(y - 250) > 1).OrderBy(y => y).ToArray();
        Assert.True(others.Length >= 1);
        double target = others[^1];

        // 抓住中间段（从它当前位置开始拖），再拖到自身另一条水平走线附近
        d.BeginConnectionSegmentDrag(conn);
        d.UpdateConnectionSegmentDrag(conn, horizontal: true, pos: 250);
        d.UpdateConnectionSegmentDrag(conn, horizontal: true, pos: target + 3);

        Assert.Equal(target, conn.DetourY!.Value, 3);
        Assert.Single(d.AlignmentGuides);

        d.EndConnectionSegmentDrag(conn);
    }

    [Fact]
    public void DraggedSegment_DoesNotSnapToItself()
    {
        var d = new DiagramViewModel(1000, 1000);
        var conn = d.AddFreeConnection(new Point(100, 100), new Point(500, 400));
        conn.SetDetour(null, 250);

        // 只拖 1px：没有别的段在阈值内 → 不被自己吸住，也不产生捕捉线
        d.BeginConnectionSegmentDrag(conn);
        d.UpdateConnectionSegmentDrag(conn, horizontal: true, pos: 250);
        d.UpdateConnectionSegmentDrag(conn, horizontal: true, pos: 251);

        Assert.Equal(251, conn.DetourY!.Value, 3);
        Assert.Empty(d.AlignmentGuides);

        d.EndConnectionSegmentDrag(conn);
    }

    [Fact]
    public void OtherPipesStillWinWhenCloser()
    {
        var d = new DiagramViewModel(1000, 1000);
        var conn = d.AddFreeConnection(new Point(100, 100), new Point(500, 400));
        conn.SetDetour(null, 250);

        // 另一条管路的水平段在 Y=254（比自身走线更近）
        d.AddFreeConnection(new Point(700, 254), new Point(900, 254));

        d.BeginConnectionSegmentDrag(conn);
        d.UpdateConnectionSegmentDrag(conn, horizontal: true, pos: 250);
        d.UpdateConnectionSegmentDrag(conn, horizontal: true, pos: 255);

        Assert.Equal(254, conn.DetourY!.Value, 3);

        d.EndConnectionSegmentDrag(conn);
    }
}
