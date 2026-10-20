using System.Windows;

namespace FlowChartKit.Routing;

/// <summary>
/// 正交（横平竖直）布线器：按端口出口方向生成 L/Z 形路径，
/// 端口处带短直段（stub）避免在端口处出现反向折角。
/// </summary>
public static class OrthogonalRouter
{
    public static List<Point> Route(Point s, PortDir sDir, Point e, PortDir eDir, double stub = 16)
    {
        var sv = sDir.Vec();
        var ev = eDir.Vec();
        var s1 = s + sv * stub;
        var e1 = e + ev * stub;

        var pts = new List<Point> { s, s1 };

        if (sv.X != 0) // 起点水平出线
        {
            if (Math.Abs(s1.Y - e1.Y) < 0.01)
            {
                pts.Add(e1);
            }
            else
            {
                bool continueX = Math.Abs(e1.X - s1.X) < 0.01 || Math.Sign(e1.X - s1.X) == Math.Sign(sv.X);
                pts.Add(continueX ? new Point(e1.X, s1.Y) : new Point(s1.X, e1.Y));
                pts.Add(e1);
            }
        }
        else // 起点垂直出线
        {
            if (Math.Abs(s1.X - e1.X) < 0.01)
            {
                pts.Add(e1);
            }
            else
            {
                bool continueY = Math.Abs(e1.Y - s1.Y) < 0.01 || Math.Sign(e1.Y - s1.Y) == Math.Sign(sv.Y);
                pts.Add(continueY ? new Point(s1.X, e1.Y) : new Point(e1.X, s1.Y));
                pts.Add(e1);
            }
        }

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
