using FlowChartKit.Core;

namespace FlowChartKit.ViewModels;

/// <summary>
/// 流路图上的文本模块：与部件 / 管路平级的第三种画布元素——有编号、文本内容与字号，
/// 可拖动、随存档持久化，宿主可按编号远程修改文本（见 SetTextByNumberCommand 命令桥）。
/// </summary>
public class TextBlockViewModel : ObservableBase
{
    /// <summary>字号下限（px）。</summary>
    public const double MinFontSize = 6;

    /// <summary>字号上限（px）。</summary>
    public const double MaxFontSize = 48;

    /// <summary>字号默认值（px）。</summary>
    public const double DefaultFontSize = 14;

    private double _x;
    private double _y;
    private string _content;
    private bool _isSelected;
    private double _fontSize;

    public TextBlockViewModel(string? content = null, double x = 0, double y = 0,
        double fontSize = DefaultFontSize, string? id = null)
    {
        _content = content ?? "文本";
        _x = x;
        _y = y;
        _fontSize = Math.Clamp(fontSize, MinFontSize, MaxFontSize);
        Id = id ?? Guid.NewGuid().ToString("N");
    }

    public string Id { get; }

    private int _number;
    /// <summary>
    /// 全图唯一的编号：添加时由图模型分配（现有最大编号 + 1），保存 / 载入存档时持久化；
    /// 宿主按编号定位文本模块（如 SetTextByNumberCommand），0 表示未编号。
    /// </summary>
    public int Number
    {
        get => _number;
        internal set => Set(ref _number, value);
    }

    public double X
    {
        get => _x;
        set => Set(ref _x, value);
    }

    public double Y
    {
        get => _y;
        set => Set(ref _y, value);
    }

    /// <summary>一次设置 X/Y（拖动路径专用，与 NodeViewModel.MoveTo 同一模式）。</summary>
    public void MoveTo(double x, double y)
    {
        if (_x == x && _y == y) return;
        if (_x != x) { _x = x; Raise(nameof(X)); }
        if (_y != y) { _y = y; Raise(nameof(Y)); }
    }

    /// <summary>文本内容；改动即时刷新画布显示并随存档持久化。</summary>
    public string Content
    {
        get => _content;
        set => Set(ref _content, value ?? "");
    }

    /// <summary>字号（px），钳制在 <see cref="MinFontSize"/> ~ <see cref="MaxFontSize"/> 之间。</summary>
    public double FontSize
    {
        get => _fontSize;
        set => Set(ref _fontSize, Math.Clamp(value, MinFontSize, MaxFontSize));
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    /// <summary>所属图模型（由 DiagramViewModel.AddTextItem 设置）。</summary>
    public DiagramViewModel? Diagram { get; internal set; }
}
