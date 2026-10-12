using System.Windows;
using FlowChartKit.Core;

namespace FlowChartKit.ViewModels;

/// <summary>连接点：位置按所属部件形状的宽高比例（FX/FY）定义。</summary>
public class PortViewModel : ObservableBase
{
    private bool _isHighlighted;

    public PortViewModel(NodeViewModel owner, string id, double fx, double fy, PortDir dir)
    {
        Owner = owner;
        Id = id;
        FX = fx;
        FY = fy;
        Dir = dir;
    }

    public NodeViewModel Owner { get; }
    public string Id { get; }
    public double FX { get; }
    public double FY { get; }
    public PortDir Dir { get; }

    /// <summary>绝对坐标（画布坐标系）。</summary>
    public Point AbsolutePoint => new(Owner.X + FX * Owner.ShapeWidth, Owner.Y + FY * Owner.ShapeHeight);

    /// <summary>高亮圈（吸附提示）左上角位置。</summary>
    public double HighlightX => FX * Owner.ShapeWidth - 8;
    public double HighlightY => FY * Owner.ShapeHeight - 8;

    public bool IsHighlighted { get => _isHighlighted; set => Set(ref _isHighlighted, value); }
}
