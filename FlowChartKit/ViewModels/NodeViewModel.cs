using System.Windows;
using FlowChartKit.Core;
using FlowChartKit.Rendering;

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
        // 名称/尺寸/端口由该部件的定义自管理（见 NodeDefinitionRegistry）
        var def = NodeDefinitionRegistry.Resolve(kind);
        _label = string.IsNullOrWhiteSpace(label) ? def.DisplayName : label!;
        (BaseWidth, BaseHeight) = def.BaseSize;
        _x = x;
        _y = y;
        Ports = def.Ports.Select(p => new PortViewModel(this, p.Id, p.FX, p.FY, p.Dir)).ToList();
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
                GeometryChanged?.Invoke(this);
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

    /// <summary>几何（位置/尺寸）发生一次"逻辑变化"时触发一次，供图模型合并重算相连管路。
    /// 与 <see cref="ObservableBase.PropertyChanged"/> 的区别：拖动过程中 X、Y 分开赋值只算一次。</summary>
    public event Action<NodeViewModel>? GeometryChanged;

    /// <summary>部件 X 坐标。单独赋值即触发一次 <see cref="GeometryChanged"/>。
    /// 若需同时改 X/Y，请使用 <see cref="MoveTo"/> 以避免两次重算。</summary>
    public double X
    {
        get => _x;
        set { if (Set(ref _x, value)) GeometryChanged?.Invoke(this); }
    }

    /// <summary>部件 Y 坐标。单独赋值即触发一次 <see cref="GeometryChanged"/>。
    /// 若需同时改 X/Y，请使用 <see cref="MoveTo"/> 以避免两次重算。</summary>
    public double Y
    {
        get => _y;
        set { if (Set(ref _y, value)) GeometryChanged?.Invoke(this); }
    }

    /// <summary>一次设置 X/Y，只触发一次 <see cref="GeometryChanged"/>（拖动路径专用，避免重复重算管路）。</summary>
    public void MoveTo(double x, double y)
    {
        if (_x == x && _y == y) return;
        if (_x != x) { _x = x; Raise(nameof(X)); }
        if (_y != y) { _y = y; Raise(nameof(Y)); }
        GeometryChanged?.Invoke(this);
    }

    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

    private int _kindNumber;
    /// <summary>同类部件内的序号：按 <see cref="DiagramViewModel.Nodes"/> 集合中同类型节点的先后顺序从 1 编号，
    /// 添加/移除时由图模型重排；保存布局时写入存档，载入时存档值优先（缺失时回退为按顺序推导）。</summary>
    public int KindNumber
    {
        get => _kindNumber;
        internal set => Set(ref _kindNumber, value);
    }

    /// <summary>所属图模型（由 DiagramViewModel.AddNode 设置）。</summary>
    public DiagramViewModel? Diagram { get; internal set; }

    private object? _state;
    /// <summary>宿主自定义的状态载荷：语义由宿主定义，控件不解释；
    /// 经画布控件的 SetNodeStateByNumberCommand 桥接设置（参照管路的 SetDirectionByNumberCommand），也可直接赋值。</summary>
    public object? State
    {
        get => _state;
        set => Set(ref _state, value);
    }

    /// <summary>端口列表（构造时按部件定义生成，运行期不增删——增删不会触发索引/重算，序列化也不感知）。</summary>
    public IReadOnlyList<PortViewModel> Ports { get; }
}
