namespace FlowChartKit;

/// <summary>部件支持的手势类型。后续扩展（长按等）在此追加。</summary>
public enum NodeEventGesture
{
    Click,
    DoubleClick,
}

/// <summary>
/// 部件事件目录条目：把一种手势映射为语义事件标识，宿主按 <see cref="EventId"/> 分发，
/// 解决"每个部件存在不同事件"的语义化问题。条目由 <see cref="Rendering.INodeDefinition"/> 声明。
/// </summary>
public sealed class NodeEventSpec
{
    public NodeEventSpec(NodeEventGesture gesture, string eventId, string? displayName = null)
    {
        Gesture = gesture;
        EventId = eventId;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? gesture.ToString() : displayName!;
    }

    /// <summary>触发该事件的手势。</summary>
    public NodeEventGesture Gesture { get; }

    /// <summary>语义事件标识（宿主分发键），如 "ToggleState"、"ShowDetails"。</summary>
    public string EventId { get; }

    /// <summary>展示用名称（文档/提示生成）。</summary>
    public string DisplayName { get; }

    /// <summary>
    /// 从目录中解析手势对应的事件：取首个匹配条目。目录为空或无该手势条目时返回 null
    /// （空目录视为未启用事件目录，由调用方按默认单击语义处理）。
    /// 同一手势声明多个条目时只取第一个——目录由部件作者负责保持唯一。
    /// </summary>
    public static NodeEventSpec? Resolve(IReadOnlyList<NodeEventSpec> catalog, NodeEventGesture gesture)
    {
        foreach (var spec in catalog)
            if (spec.Gesture == gesture)
                return spec;
        return null;
    }

    /// <summary>
    /// 该部件是否需要单击退避：仅当目录同时声明 Click 与 DoubleClick 时，
    /// 双击必然先产生一次单击，控件需把单击延迟到双击判定窗口之外再上抛，
    /// 由双击取消挂起的单击。只声明其一（或空目录）时单击即时触发、零延迟。
    /// </summary>
    public static bool NeedsClickBackoff(IReadOnlyList<NodeEventSpec> catalog)
    {
        var hasClick = false;
        var hasDoubleClick = false;
        foreach (var spec in catalog)
        {
            if (spec.Gesture == NodeEventGesture.Click) hasClick = true;
            else if (spec.Gesture == NodeEventGesture.DoubleClick) hasDoubleClick = true;
        }
        return hasClick && hasDoubleClick;
    }
}
