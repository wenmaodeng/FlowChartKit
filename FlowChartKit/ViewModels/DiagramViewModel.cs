using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using FlowChartKit.Core;
using FlowChartKit.Data;

namespace FlowChartKit.ViewModels;

/// <summary>流路图视图模型：部件、管路、选择、吸附、序列化。</summary>
public class DiagramViewModel : ObservableBase
{
    private bool _isEditable = true;
    private bool _showNumbers = true;
    private NodeViewModel? _selectedNode;
    private ConnectionViewModel? _selectedConnection;
    private PortViewModel? _highlightedPort;

    public const double SnapRadius = 26;

    public DiagramViewModel(double width = 1400, double height = 900)
    {
        CanvasWidth = width;
        CanvasHeight = height;

        SetDirectionCommand = new RelayCommand(p =>
        {
            if (SelectedConnection is null || p is null) return;
            SelectedConnection.Direction = p switch
            {
                FlowMode f => f,
                string s => Enum.TryParse<FlowMode>(s, true, out var fm) ? fm : SelectedConnection.Direction,
                _ => SelectedConnection.Direction
            };
        }, _ => SelectedConnection != null);

        DeleteSelectedConnectionCommand = new RelayCommand(_ =>
        {
            if (SelectedConnection is { } c) RemoveConnection(c);
        }, _ => SelectedConnection != null);

        DeleteSelectedCommand = new RelayCommand(_ =>
        {
            if (SelectedConnection is { } c) RemoveConnection(c);
            else if (SelectedNode is { } n) RemoveNode(n);
        }, _ => SelectedConnection != null || SelectedNode != null);
    }

    public double CanvasWidth { get; }
    public double CanvasHeight { get; }

    public ObservableCollection<NodeViewModel> Nodes { get; } = [];
    public ObservableCollection<ConnectionViewModel> Connections { get; } = [];

    /// <summary>编辑模式：true 允许拖动部件/端点并显示端点手柄；关闭时管路编号一并隐藏。</summary>
    public bool IsEditable
    {
        get => _isEditable;
        set
        {
            if (Set(ref _isEditable, value))
                Raise(nameof(ShowNumbers));
        }
    }

    /// <summary>是否在画布上显示管路编号（编号属编辑辅助信息，编辑模式关闭时恒为 false）。</summary>
    public bool ShowNumbers
    {
        get => _showNumbers && _isEditable;
        set => Set(ref _showNumbers, value);
    }

    public NodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (_selectedNode == value) return;
            if (_selectedNode != null) _selectedNode.IsSelected = false;
            _selectedNode = value;
            if (_selectedNode != null)
            {
                _selectedNode.IsSelected = true;
                if (_selectedConnection != null)
                {
                    _selectedConnection.IsSelected = false;
                    _selectedConnection = null;
                }
            }
            Raise();
            Raise(nameof(SelectedConnection));
        }
    }

    public ConnectionViewModel? SelectedConnection
    {
        get => _selectedConnection;
        set
        {
            if (_selectedConnection == value) return;
            if (_selectedConnection != null) _selectedConnection.IsSelected = false;
            _selectedConnection = value;
            if (_selectedConnection != null)
            {
                _selectedConnection.IsSelected = true;
                if (_selectedNode != null)
                {
                    _selectedNode.IsSelected = false;
                    _selectedNode = null;
                }
            }
            Raise();
            Raise(nameof(SelectedNode));
        }
    }

    /// <summary>清除管路选中状态（部件选中时调用）。</summary>
    public void ClearConnectionSelection() => SelectedConnection = null;

    public ICommand SetDirectionCommand { get; }
    public ICommand DeleteSelectedConnectionCommand { get; }
    public ICommand DeleteSelectedCommand { get; }

    // ---- 构建 ----

    /// <summary>分配下一个管路编号：现有最大编号 + 1（无则 1）。</summary>
    private int NextNumber()
    {
        int max = 0;
        foreach (var c in Connections)
            if (c.Number > max) max = c.Number;
        return max + 1;
    }

    public NodeViewModel AddNode(NodeKind kind, double x, double y, string? label = null, string? id = null)
    {
        var node = new NodeViewModel(kind, label, x, y, id);
        node.Diagram = this;
        node.PropertyChanged += OnNodePropertyChanged;
        Nodes.Add(node);
        return node;
    }

    public ConnectionViewModel AddConnection(
        NodeViewModel sourceNode, PortViewModel sourcePort,
        NodeViewModel targetNode, PortViewModel targetPort,
        FlowMode direction = FlowMode.Forward, string? id = null)
    {
        var conn = new ConnectionViewModel(this, id)
        {
            SourceNode = sourceNode,
            SourcePort = sourcePort,
            TargetNode = targetNode,
            TargetPort = targetPort,
            Direction = direction
        };
        conn.Number = NextNumber();
        Connections.Add(conn);
        conn.Recompute();
        return conn;
    }

    public ConnectionViewModel AddConnection(ConnectionViewModel conn)
    {
        if (conn.Number == 0)
            conn.Number = NextNumber();
        Connections.Add(conn);
        conn.Recompute();
        return conn;
    }

    /// <summary>添加一条两端均为自由点的管路，拖动端点即可吸附到部件端口。</summary>
    public ConnectionViewModel AddFreeConnection(Point start, Point end, FlowMode direction = FlowMode.Forward)
    {
        var conn = new ConnectionViewModel(this) { Direction = direction };
        conn.SetFreeEnd(true, start);
        conn.SetFreeEnd(false, end);
        conn.Number = NextNumber();
        Connections.Add(conn);
        conn.Recompute();
        return conn;
    }

    public void RemoveConnection(ConnectionViewModel conn)
    {
        if (!Connections.Contains(conn)) return;
        if (SelectedConnection == conn) SelectedConnection = null;
        Connections.Remove(conn);
    }

    /// <summary>移除部件及其所有相连管路。</summary>
    public void RemoveNode(NodeViewModel node)
    {
        if (!Nodes.Contains(node)) return;
        var conns = Connections.Where(c => c.SourceNode == node || c.TargetNode == node).ToList();
        foreach (var c in conns)
        {
            if (SelectedConnection == c) SelectedConnection = null;
            Connections.Remove(c);
        }
        if (SelectedNode == node) SelectedNode = null;
        node.PropertyChanged -= OnNodePropertyChanged;
        Nodes.Remove(node);
    }

    private void OnNodePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not NodeViewModel node) return;

        switch (e.PropertyName)
        {
            case nameof(NodeViewModel.Label):
                // 改名后刷新相关管路的显示名（#编号 源 → 目标）
                foreach (var conn in Connections)
                {
                    if (conn.SourceNode == node || conn.TargetNode == node)
                        conn.RaiseDisplayName();
                }
                break;

            // 位置变化或尺寸变化（缩放导致端口位移）都要重布相连的管路
            case nameof(NodeViewModel.X):
            case nameof(NodeViewModel.Y):
            case nameof(NodeViewModel.ShapeWidth):
            case nameof(NodeViewModel.ShapeHeight):
                foreach (var conn in Connections)
                {
                    if (conn.SourceNode == node || conn.TargetNode == node)
                        conn.Recompute();
                }
                break;
        }
    }

    // ---- 端口吸附 ----

    public (PortViewModel? Port, Point Position) FindNearestPort(Point p, PortViewModel? exclude)
    {
        PortViewModel? best = null;
        double bestDist = SnapRadius * SnapRadius;

        foreach (var node in Nodes)
        {
            foreach (var port in node.Ports)
            {
                if (port == exclude) continue;
                var pos = port.AbsolutePoint;
                double d = (pos.X - p.X) * (pos.X - p.X) + (pos.Y - p.Y) * (pos.Y - p.Y);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = port;
                }
            }
        }
        return (best, best?.AbsolutePoint ?? p);
    }

    public void SetSnapHighlight(PortViewModel? port)
    {
        if (_highlightedPort == port) return;
        if (_highlightedPort != null) _highlightedPort.IsHighlighted = false;
        _highlightedPort = port;
        if (_highlightedPort != null) _highlightedPort.IsHighlighted = true;
    }

    public PortViewModel? FindPort(NodeViewModel node, string portId)
        => node.Ports.FirstOrDefault(p => p.Id == portId);

    // ---- 序列化 ----

    public DiagramData ToData()
    {
        var data = new DiagramData { Width = CanvasWidth, Height = CanvasHeight };

        foreach (var n in Nodes)
        {
            data.Nodes.Add(new NodeData
            {
                Id = n.Id,
                Kind = n.Kind,
                X = n.X,
                Y = n.Y,
                Label = n.Label,
                W = n.ShapeWidth,
                H = n.ShapeHeight,
                Scale = n.Scale
            });
        }

        foreach (var c in Connections)
        {
            var cd = new ConnectionData
            {
                Id = c.Id,
                Number = c.Number,
                Direction = c.Direction,
                SourceNodeId = c.SourceNode?.Id,
                SourcePortId = c.SourcePort?.Id,
                TargetNodeId = c.TargetNode?.Id,
                TargetPortId = c.TargetPort?.Id
            };
            if (c.FreeSource is { } fs) { cd.SourceFreeX = fs.X; cd.SourceFreeY = fs.Y; }
            if (c.FreeTarget is { } ft) { cd.TargetFreeX = ft.X; cd.TargetFreeY = ft.Y; }
            data.Connections.Add(cd);
        }
        return data;
    }

    public void Clear()
    {
        SelectedConnection = null;
        SelectedNode = null;
        SetSnapHighlight(null);
        Connections.Clear();
        foreach (var n in Nodes) n.PropertyChanged -= OnNodePropertyChanged;
        Nodes.Clear();
    }

    public static DiagramViewModel FromData(DiagramData data)
    {
        var diagram = new DiagramViewModel(data.Width, data.Height);
        diagram.Load(data);
        return diagram;
    }

    public void Load(DiagramData data)
    {
        Clear();
        var map = new Dictionary<string, NodeViewModel>();

        foreach (var nd in data.Nodes)
        {
            var node = AddNode(nd.Kind, nd.X, nd.Y, nd.Label, nd.Id);
            node.Scale = nd.Scale > 0 ? nd.Scale : 1;
            map[nd.Id] = node;
        }

        foreach (var cd in data.Connections)
        {
            var conn = new ConnectionViewModel(this, cd.Id) { Direction = cd.Direction };
            conn.Number = cd.Number > 0 ? cd.Number : NextNumber();

            if (cd.SourceNodeId is { } snId && map.TryGetValue(snId, out var sn))
            {
                var sp = FindPort(sn, cd.SourcePortId ?? "R");
                if (sp != null) conn.SetEnd(true, sn, sp);
                else if (cd.SourceFreeX is { } sx && cd.SourceFreeY is { } sy)
                    conn.SetFreeEnd(true, new Point(sx, sy));
            }
            else if (cd.SourceFreeX is { } sfx && cd.SourceFreeY is { } sfy)
            {
                conn.SetFreeEnd(true, new Point(sfx, sfy));
            }

            if (cd.TargetNodeId is { } tnId && map.TryGetValue(tnId, out var tn))
            {
                var tp = FindPort(tn, cd.TargetPortId ?? "L");
                if (tp != null) conn.SetEnd(false, tn, tp);
                else if (cd.TargetFreeX is { } tx && cd.TargetFreeY is { } ty)
                    conn.SetFreeEnd(false, new Point(tx, ty));
            }
            else if (cd.TargetFreeX is { } tfx && cd.TargetFreeY is { } tfy)
            {
                conn.SetFreeEnd(false, new Point(tfx, tfy));
            }

            Connections.Add(conn);
            conn.Recompute();
        }
    }
}
