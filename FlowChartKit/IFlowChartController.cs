namespace FlowChartKit;

/// <summary>
/// 流路图外部控制接口：开关编辑模式、按管路编号设置流向。
/// <see cref="Controls.DiagramCanvasControl"/> 实现了该接口；
/// 宿主可面向接口编程，便于替换实现或做单元测试。
/// </summary>
public interface IFlowChartController
{
    /// <summary>编辑模式：true 允许拖动部件与管路端点，并显示端口与管路编号。</summary>
    bool IsEditable { get; set; }

    /// <summary>按管路编号设置流向。</summary>
    /// <param name="number">管路编号，从 1 开始，与画布上的编号徽章一致。</param>
    /// <param name="direction">目标流向。</param>
    /// <returns>找到并设置了该编号的管路时为 true；编号不存在时为 false。</returns>
    bool SetConnectionDirection(int number, FlowMode direction);
}
