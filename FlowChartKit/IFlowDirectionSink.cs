using System;

namespace FlowChartKit;

/// <summary>
/// 宿主 ViewModel 实现此接口后，画布控件会在 DataContext 就绪时主动把
/// "按编号设置管路流向"的回调写入 <see cref="SetDirection"/>。
/// 这是 <see cref="Controls.DiagramCanvasControl.SetDirectionCallback"/> 绑定回传之外的保底通道：
/// 不依赖绑定引擎的求值时机，也不需要宿主在 Loaded 里做任何事。
/// </summary>
public interface IFlowDirectionSink
{
    /// <summary>按编号设置流向的回调，由画布控件赋值。</summary>
    Func<int, FlowMode, bool>? SetDirection { get; set; }
}
