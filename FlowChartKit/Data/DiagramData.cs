using FlowChartKit;

namespace FlowChartKit.Data;

/// <summary>流路图持久化数据（JSON 序列化用）。</summary>
public sealed class DiagramData
{
    /// <summary>
    /// 存档格式版本（当前 1，首次发布）。为将来 schema 变更预留迁移锚点：
    /// 载入时发现更旧版本可按需引入转换逻辑，未识别的更高版本应提示宿主升级库。
    /// </summary>
    public int Version { get; set; } = 1;

    public double Width { get; set; } = 1400;
    public double Height { get; set; } = 900;
    public List<NodeData> Nodes { get; set; } = [];
    public List<ConnectionData> Connections { get; set; } = [];
}

public sealed class NodeData
{
    public string Id { get; set; } = "";
    public NodeKind Kind { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public string Label { get; set; } = "";
    public double Scale { get; set; } = 1;

    /// <summary>同类部件序号：保存布局时按当前值写入；0 表示未编号（载入时回退为按集合顺序推导，兼容旧存档）。</summary>
    public int Number { get; set; }
}

public sealed class ConnectionData
{
    public string Id { get; set; } = "";
    public int Number { get; set; }
    public string? SourceNodeId { get; set; }
    public string? SourcePortId { get; set; }
    public double? SourceFreeX { get; set; }
    public double? SourceFreeY { get; set; }
    public string? TargetNodeId { get; set; }
    public string? TargetPortId { get; set; }
    public double? TargetFreeX { get; set; }
    public double? TargetFreeY { get; set; }
    public FlowMode Direction { get; set; } = FlowMode.None;

    /// <summary>管路类型（决定颜色/粗细/流动画）；缺省为内置普通管路，旧存档无此字段时按该默认值回读。</summary>
    public PipeKind PipeKind { get; set; } = PipeKind.Standard;

    /// <summary>连线布局（垂直水平/直线/弧线）；缺省为垂直水平，旧存档无此字段时按该默认值回读。</summary>
    public PipeRouting Routing { get; set; } = PipeRouting.Orthogonal;

    /// <summary>正交布局下手工拖动中间竖直段后的 X 坐标；null 表示按端口方向自动取位。</summary>
    public double? DetourX { get; set; }

    /// <summary>正交布局下手工拖动中间水平段后的 Y 坐标；null 表示按端口方向自动取位。</summary>
    public double? DetourY { get; set; }
}
