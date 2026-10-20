using System.Collections.ObjectModel;
using System.Windows;
using FlowChartKit.Core;

namespace FlowChartKit.ViewModels;

/// <summary>流路图上的部件（样品杯、试剂瓶、注射泵等）。</summary>
public class NodeViewModel : ObservableBase
{
    /// <summary>缩放系数下限。</summary>
    public const double MinScale = 0.2;

    /// <summary>缩放系数上限。</summary>
    public const double MaxScale = 5.0;

    private double _x;
    private double _y;
    private string _label = "";
    private bool _isSelected;
    private double _scale = 1;

    public NodeViewModel(NodeKind kind, string? label = null, double x = 0, double y = 0, string? id = null)
    {
        Kind = kind;
        Id = id ?? Guid.NewGuid().ToString("N");
        _label = string.IsNullOrWhiteSpace(label) ? NodeKindDefaults.DisplayName(kind) : label!;
        (BaseWidth, BaseHeight) = NodeKindDefaults.Size(kind);
        _x = x;
        _y = y;
        Ports = new ObservableCollection<PortViewModel>(CreateDefaultPorts(kind));
    }

    public string Id { get; }
    public NodeKind Kind { get; }

    /// <summary>该类型部件的默认宽高（未缩放）。</summary>
    public double BaseWidth { get; }
    public double BaseHeight { get; }

    /// <summary>比例缩放系数：1 为原始尺寸，改动会联动 <see cref="ShapeWidth"/> / <see cref="ShapeHeight"/>。</summary>
    public double Scale
    {
        get => _scale;
        set
        {
            if (Set(ref _scale, Math.Clamp(value, MinScale, MaxScale)))
            {
                Raise(nameof(ShapeWidth));
                Raise(nameof(ShapeHeight));
            }
        }
    }

    public double ShapeWidth => BaseWidth * _scale;
    public double ShapeHeight => BaseHeight * _scale;

    /// <summary>部件名称；可在属性窗口修改，改动会即时刷新画布显示。</summary>
    public string Label
    {
        get => _label;
        set => Set(ref _label, value ?? "");
    }

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
