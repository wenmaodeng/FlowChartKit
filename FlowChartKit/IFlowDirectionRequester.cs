using System;

namespace FlowChartKit;

/// <summary>
/// 设置流向请求的发送方契约（由宿主 ViewModel 实现）。画布控件订阅此事件，
/// 把请求落到自身图模型上。VM 只依赖此中性契约，不引用任何控件类型，从而彻底解耦。
/// </summary>
public interface IFlowDirectionRequester
{
    /// <summary>请求按编号设置管路流向；处理方应回填 <see cref="SetDirectionEventArgs.Result"/>。</summary>
    event EventHandler<SetDirectionEventArgs> SetDirectionRequested;
}
