using System.Collections.ObjectModel;
using System.Windows;
using FlowChartKit.Core;

namespace FlowChartKit.ViewModels;

/// <summary>流路图上的部件（样品杯、试剂瓶、注射泵等）。</summary>
public class NodeViewModel : ObservableBase
{
    private double _x;
    private double _y;
    private string _label = "";
    private bool _isSelected;

    public NodeViewModel(NodeKind kind, string? label = null, double x = 0, double y = 0, string? id = null)
    {
        Kind = kind;
        Id = id ?? Guid.NewGuid().ToString("N");
        _label = string.IsNullOrWhiteSpace(label) ? NodeKindDefaults.DisplayName(kind) : label!;
        (ShapeWidth, ShapeHeight) = NodeKindDefaults.Size(kind);
        _x = x;
        _y = y;
        Ports = new ObservableCollection<PortViewModel>(CreateDefaultPorts(kind));
    }

    public string Id { get; }
    public NodeKind Kind { get; }
    public double ShapeWidth { get; }
    public double ShapeHeight { get; }

    public string Label { get => _label; private set => Set(ref _label, value); }

    public double X { get => _x; set => Set(ref _x, value); }
    public double Y { get => _y; set => Set(ref _y, value); }

    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

    /// <summary>所属图模型（由 DiagramViewModel.AddNode 设置）。</summary>
    public DiagramViewModel? Diagram { get; internal set; }

    public ObservableCollection<PortViewModel> Ports { get; }

    private List<PortViewModel> CreateDefaultPorts(NodeKind kind)
    {
        if (kind == NodeKind.PumpModule)
        {
            return
            [
                new PortViewModel(this, "T",  0.50, 0.02, PortDir.Up),
                new PortViewModel(this, "L1", 0.00, 0.10, PortDir.Left),
                new PortViewModel(this, "L2", 0.00, 0.24, PortDir.Left),
                new PortViewModel(this, "R1", 1.00, 0.10, PortDir.Right),
                new PortViewModel(this, "R2", 1.00, 0.24, PortDir.Right),
                new PortViewModel(this, "B1", 0.30, 1.00, PortDir.Down),
                new PortViewModel(this, "B2", 0.45, 1.00, PortDir.Down),
                new PortViewModel(this, "B3", 0.60, 1.00, PortDir.Down),
                new PortViewModel(this, "B4", 0.75, 1.00, PortDir.Down),
            ];
        }
        return
        [
            new PortViewModel(this, "T", 0.5, 0, PortDir.Up),
            new PortViewModel(this, "B", 0.5, 1, PortDir.Down),
            new PortViewModel(this, "L", 0,   0.5, PortDir.Left),
            new PortViewModel(this, "R", 1,   0.5, PortDir.Right),
        ];
    }
}
