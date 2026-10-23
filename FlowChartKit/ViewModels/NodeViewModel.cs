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

    /// <summary>标签字号下限。</summary>
    public const double MinLabelFontSize = 6;

    /// <summary>标签字号上限。</summary>
    public const double MaxLabelFontSize = 48;

    /// <summary>标签字号默认值（与库内置字号一致）。</summary>
    public const double DefaultLabelFontSize = 11;

    private double _x;
    private double _y;
    private string _label = "";
    private bool _isSelected;
    private double _scaleX = 1;
    private double _scaleY = 1;
    private double _labelFontSize = DefaultLabelFontSize;

    public NodeViewModel(NodeKind kind, string? label = null, double x = 0, double y = 0, string? id = null)
    {
        Kind = kind;
        Id = id ?? Guid.NewGuid().ToString("N");
        // 名称/尺寸/端口由该部件的定义自管理（见 NodeDefinitionRegistry）
        var def = NodeDefinitionRegistry.Resolve(kind);
        // 仅"未指定名称"（null）时回退到类型默认名；显式传入空字符串表示"该部件不显示名称"，予以保留
        _label = label ?? def.DisplayName;
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

    /// <summary>**水平（宽度）方向**的缩放系数：1 为原始宽度，改动联动 <see cref="ShapeWidth"/>。</summary>
    public double ScaleX
    {
        get => _scaleX;
        set => SetScale(value, _scaleY);
    }

    /// <summary>**垂直（高度）方向**的缩放系数：1 为原始高度，改动联动 <see cref="ShapeHeight"/>。</summary>
    public double ScaleY
    {
        get => _scaleY;
        set => SetScale(_scaleX, value);
    }

    /// <summary>
    /// **等比**缩放系数（<see cref="ScaleX"/> = <see cref="ScaleY"/>，兼容旧 API 与便捷入口）：
    /// 赋值时同时应用到两个方向（只触发一次几何变更）；读取返回 <see cref="ScaleX"/>——
    /// 两方向不一致（非等比缩放）时请以 <see cref="ScaleX"/> / <see cref="ScaleY"/> 为准。
    /// </summary>
    public double Scale
    {
        get => _scaleX;
        set => SetScale(value, value);
    }

    /// <summary>
    /// 一次设置两个方向的缩放系数（钳制到 <see cref="MinScale"/> ~ <see cref="MaxScale"/>）：
    /// 等比缩放、存档载入、Esc 回滚都走这里，保证只触发一次
    /// <see cref="GeometryChanged"/>（相连管路每帧只重算一次）。
    /// </summary>
    public void SetScale(double scaleX, double scaleY)
    {
        double x = Math.Clamp(scaleX, MinScale, MaxScale);
        double y = Math.Clamp(scaleY, MinScale, MaxScale);
        if (_scaleX == x && _scaleY == y) return;

        _scaleX = x;
        _scaleY = y;
        Raise(nameof(ScaleX));
        Raise(nameof(ScaleY));
        Raise(nameof(Scale));
        Raise(nameof(ShapeWidth));
        Raise(nameof(ShapeHeight));
        GeometryChanged?.Invoke(this);
    }

    public double ShapeWidth => BaseWidth * _scaleX;
    public double ShapeHeight => BaseHeight * _scaleY;

    /// <summary>
    /// 部件名称的字号（px）：只影响标签文字大小，**不随部件缩放变化**，
    /// 改动会即时刷新画布显示并随存档持久化（钳制在
    /// <see cref="MinLabelFontSize"/> ~ <see cref="MaxLabelFontSize"/> 之间）。
    /// </summary>
    public double LabelFontSize
    {
        get => _labelFontSize;
        set => Set(ref _labelFontSize, Math.Clamp(value, MinLabelFontSize, MaxLabelFontSize));
    }

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

    private int _z;
    /// <summary>
    /// 叠放次序（Z 序）：值越大越靠上；由 <see cref="DiagramViewModel"/> 的 BringToFront / SendToBack /
    /// MoveAbove 等统一分配并持久化。同值时按默认层级（管路 → 文本 → 部件）排列。
    /// </summary>
    public int Z { get => _z; set => Set(ref _z, value); }

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
