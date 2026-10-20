using System.Windows;
using FlowChartKit;
using FlowChartKit.Routing;
using Xunit;

namespace FlowChartKit.Tests;

public class OrthogonalRouterTests
{
    [Fact]
    public void Route_AllSegmentsAxisAligned()
    {
        var pts = OrthogonalRouter.Route(
            new Point(10, 10), PortDir.Right,
            new Point(210, 110), PortDir.Left);

        Assert.True(pts.Count >= 2);
        for (int i = 1; i < pts.Count; i++)
        {
            bool horizontal = pts[i].Y == pts[i - 1].Y;
            bool vertical = pts[i].X == pts[i - 1].X;
            Assert.True(horizontal || vertical,
                $"段 {i - 1}->{i} 不是水平/垂直段：{pts[i - 1]} -> {pts[i]}");
        }
    }

    [Fact]
    public void Route_StartsAndEndsAtGivenAnchors()
    {
        var sp = new Point(10, 10);
        var ep = new Point(210, 110);
        var pts = OrthogonalRouter.Route(sp, PortDir.Right, ep, PortDir.Left);

        Assert.Equal(sp, pts[0]);
        Assert.Equal(ep, pts[^1]);
    }

    [Fact]
    public void Route_FirstStubFollowsSourceDirection()
    {
        // 两端留有足够间距时，首段应沿源端口朝向伸出
        var pts = OrthogonalRouter.Route(
            new Point(10, 10), PortDir.Right,
            new Point(210, 10), PortDir.Left);

        Assert.True(pts[1].X > pts[0].X,
            $"首段未沿 Right 方向伸出：{pts[0]} -> {pts[1]}");
    }

    [Fact]
    public void Route_NoCollinearTriples()
    {
        var pts = OrthogonalRouter.Route(
            new Point(0, 0), PortDir.Down,
            new Point(100, 200), PortDir.Up);

        for (int i = 1; i < pts.Count - 1; i++)
        {
            var a = pts[i - 1];
            var b = pts[i];
            var c = pts[i + 1];
            bool collinear = (a.X == b.X && b.X == c.X) || (a.Y == b.Y && b.Y == c.Y);
            Assert.False(collinear, $"存在共线三点：{a} {b} {c}");
        }
    }

    // AutoDir(p, other) 返回"在 p 点、背向 other"的朝向（v = p - other 取主轴）
    [Theory]
    [InlineData(0, 0, 100, 20, PortDir.Left)]   // other 在右 → 朝左伸出
    [InlineData(0, 0, -100, 20, PortDir.Right)] // other 在左 → 朝右伸出
    [InlineData(0, 0, 20, 100, PortDir.Up)]     // other 在下 → 朝上伸出
    [InlineData(0, 0, 20, -100, PortDir.Down)]  // other 在上 → 朝下伸出
    public void AutoDir_PointsAwayFromOtherOnDominantAxis(
        double px, double py, double ox, double oy, PortDir expected)
        => Assert.Equal(expected, OrthogonalRouter.AutoDir(new Point(px, py), new Point(ox, oy)));
}
