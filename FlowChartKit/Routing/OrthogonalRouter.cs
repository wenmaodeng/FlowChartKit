using System.Windows;

namespace FlowChartKit.Routing;

/// <summary>
/// 正交（横平竖直）布线器：按端口出口方向生成 L/Z 形路径，
/// 端口处带短直段（stub）避免在端口处出现反向折角。
/// 可选参数 detourX / detourY 用于覆盖中间竖直段 / 水平段的位置，
/// 供用户拖动线段调整绕行路径；不指定时按端口方向自动取位（与旧行为完全一致）。
/// </summary>
public static class OrthogonalRouter
{
    /// <summary>端口出线短直段（stub）的默认长度：两端各一段，供"可拖动区域"判定排除相同长度。</summary>
    public const double DefaultStub = 16;

    public static List<Point> Route(Point s, PortDir sDir, Point e, PortDir eDir, double stub = DefaultStub)
        => Route(s, sDir, e, eDir, null, null, stub);

    /// <summary>
    /// 生成正交折线。<paramref name="detourX"/> 指定中间竖直段的 X，
    /// <paramref name="detourY"/> 指定中间水平段的 Y（null 表示自动取位）。
    /// </summary>
    public static List<Point> Route(Point s, PortDir sDir, Point e, PortDir eDir,
        double? detourX, double? detourY, double stub = DefaultStub)
    {
        var sv = sDir.Vec();
        var ev = eDir.Vec();
        var s1 = s + sv * stub;
        var e1 = e + ev * stub;

        var pts = new List<Point> { s, s1 };

        if (sv.X != 0) // 起点水平出线
        {
            // 中间竖直段的 X：默认沿用"继续同向则落在 e1.X，否则落在 s1.X"的判定；
            // 仅指定了水平段位置（detourY）时，把竖线落在两端中点，形成可绕行的 S 形
            double mx = detourX ?? (detourY.HasValue
                ? (s1.X + e1.X) / 2
                : (Math.Abs(e1.X - s1.X) < 0.01 || Math.Sign(e1.X - s1.X) == Math.Sign(sv.X)
                    ? e1.X
                    : s1.X));
            // 中间水平段的 Y：默认与目标出线段齐平
            double my = detourY ?? e1.Y;

            pts.Add(new Point(mx, s1.Y));
            pts.Add(new Point(mx, my));
            pts.Add(new Point(e1.X, my));
        }
        else // 起点垂直出线
        {
            // 中间水平段的 Y：默认沿用"继续同向则落在 e1.Y，否则落在 s1.Y"的判定；
            // 仅指定了竖直段位置（detourX）时，把横线落在两端中点，形成可绕行的 S 形
            double my = detourY ?? (detourX.HasValue
                ? (s1.Y + e1.Y) / 2
                : (Math.Abs(e1.Y - s1.Y) < 0.01 || Math.Sign(e1.Y - s1.Y) == Math.Sign(sv.Y)
                    ? e1.Y
                    : s1.Y));
            double mx = detourX ?? e1.X;

            pts.Add(new Point(s1.X, my));
            pts.Add(new Point(mx, my));
            pts.Add(new Point(mx, e1.Y));
        }

        pts.Add(e1);
        pts.Add(e);
        return Clean(pts);
    }

    /// <summary>自由端点的自动出口方向：沿与另一端连线的主轴方向向外。</summary>
    public static PortDir AutoDir(Point p, Point other)
    {
        var v = p - other;
        return Math.Abs(v.X) >= Math.Abs(v.Y)
            ? (v.X >= 0 ? PortDir.Right : PortDir.Left)
            : (v.Y >= 0 ? PortDir.Down : PortDir.Up);
    }

    private static List<Point> Clean(List<Point> pts)
    {
        var result = new List<Point>();
        foreach (var p in pts)
        {
            if (result.Count == 0 || DistanceSquared(result[^1], p) > 0.25) // 0.5²，避免开方
                result.Add(p);
        }
        for (int i = result.Count - 2; i >= 1; i--)
        {
            if (IsCollinear(result[i - 1], result[i], result[i + 1]))
                result.RemoveAt(i);
        }
        return result;
    }

    private static double DistanceSquared(Point a, Point b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    private static bool IsCollinear(Point a, Point b, Point c)
        => (Math.Abs(a.Y - b.Y) < 0.01 && Math.Abs(b.Y - c.Y) < 0.01)
           || (Math.Abs(a.X - b.X) < 0.01 && Math.Abs(b.X - c.X) < 0.01);
}
