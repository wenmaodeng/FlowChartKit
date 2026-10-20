using FlowChartKit;

namespace FlowChartKit.Data;

/// <summary>流路图持久化数据（JSON 序列化用）。</summary>
public sealed class DiagramData
{
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
    public double W { get; set; }
    public double H { get; set; }
    public double Scale { get; set; } = 1;
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
}
