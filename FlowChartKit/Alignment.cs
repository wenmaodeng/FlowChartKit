namespace FlowChartKit;

/// <summary>
/// 对齐参考线（捕捉线）：拖动部件时与其它部件对齐的提示线。
/// <see cref="IsVertical"/> 为 true 表示竖线（X 方向对齐：左 / 右 / 居中），
/// false 表示横线（Y 方向对齐：上 / 下 / 居中）；线的跨度覆盖被拖部件与参考部件的并集范围。
/// </summary>
public readonly record struct AlignmentGuide(
    bool IsVertical,   // true=竖线（X 对齐），false=横线（Y 对齐）
    double Offset,     // 线所在坐标：竖线为 X，横线为 Y
    double Start,      // 线起点：竖线为 Y 起点，横线为 X 起点
    double End);       // 线终点：竖线为 Y 终点，横线为 X 终点

/// <summary>
/// 部件拖动吸附结果：<see cref="X"/> / <see cref="Y"/> 为吸附后的坐标，
/// <see cref="Guides"/> 为本次需要显示的对齐参考线（无对齐时为空）。
/// </summary>
public readonly record struct NodeSnapResult(
    double X,
    double Y,
    IReadOnlyList<AlignmentGuide> Guides);

/// <summary>
/// 管路线段拖动吸附结果：<see cref="Pos"/> 为吸附后的坐标（水平段为 Y，竖直段为 X），
/// <see cref="Guides"/> 为本次需要显示的对齐参考线（无对齐时为空）。
/// </summary>
public readonly record struct SegmentSnapResult(
    double Pos,
    IReadOnlyList<AlignmentGuide> Guides);
