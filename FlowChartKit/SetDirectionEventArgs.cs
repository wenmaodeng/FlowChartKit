using System;

namespace FlowChartKit;

/// <summary>请求按编号设置管路流向的事件参数，配合 <see cref="IFlowDirectionRequester.SetDirectionRequested"/> 使用。</summary>
public class SetDirectionEventArgs : EventArgs
{
    public SetDirectionEventArgs(int number, FlowMode direction)
    {
        Number = number;
        Direction = direction;
    }

    /// <summary>管路编号，从 1 开始，与画布上的编号徽章一致。</summary>
    public int Number { get; }

    /// <summary>目标流向。</summary>
    public FlowMode Direction { get; }

    /// <summary>由处理方（画布控件）回填：是否找到并设置了该编号的管路。</summary>
    public bool? Result { get; set; }
}
