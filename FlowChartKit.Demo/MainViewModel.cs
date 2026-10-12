using System.Windows;
using FlowChartKit;
using FlowChartKit.Core;
using FlowChartKit.Data;
using FlowChartKit.ViewModels;

namespace FlowChartKit.Demo;

public record PaletteItem(NodeKind Kind, string DisplayName);

public class MainViewModel : ObservableBase
{
    private DiagramViewModel _diagram;
    private string _statusText = "就绪";
    private PaletteItem? _selectedPalette;
    private int _addCascade;

    public MainViewModel()
    {
        Palette =
        [
            new PaletteItem(NodeKind.SampleCup, "样品杯"),
            new PaletteItem(NodeKind.ReagentBottle, "试剂瓶"),
            new PaletteItem(NodeKind.Waste, "废液"),
            new PaletteItem(NodeKind.ReactionCup, "反应杯"),
            new PaletteItem(NodeKind.Cuvette, "2cm比色皿"),
            new PaletteItem(NodeKind.Centrifuge, "离心位"),
            new PaletteItem(NodeKind.NeedleWash, "洗针位"),
            new PaletteItem(NodeKind.PumpModule, "注射泵"),
        ];
        _selectedPalette = Palette[1];

        SaveCommand = new RelayCommand(_ => Save());
        ResetDemoCommand = new RelayCommand(_ =>
        {
            Diagram = BuildDemo();
            StatusText = "已恢复示例布局";
        });
        AddNodeCommand = new RelayCommand(_ =>
        {
            if (_selectedPalette is null) return;
            double x = 560 + (_addCascade % 6) * 24;
            double y = 420 + (_addCascade % 6) * 24;
            _addCascade++;
            Diagram.AddNode(_selectedPalette.Kind, x, y, _selectedPalette.DisplayName);
            StatusText = $"已添加部件：{_selectedPalette.DisplayName}";
        });

        AddConnectionCommand = new RelayCommand(_ =>
        {
            double cx = 640 + (_addCascade % 6) * 18;
            double cy = 360 + (_addCascade % 6) * 18;
            _addCascade++;
            var conn = Diagram.AddFreeConnection(new Point(cx, cy), new Point(cx + 120, cy));
            Diagram.SelectedConnection = conn;
            StatusText = "已添加管路，拖动两端端点吸附到部件端口";
        });

        _diagram = TryLoadFile() ?? BuildDemo();
    }

    public DiagramViewModel Diagram
    {
        get => _diagram;
        private set
        {
            if (Set(ref _diagram, value))
                Raise(nameof(DeleteSelectedCommand));
        }
    }

    /// <summary>委派给 Diagram.DeleteSelectedCommand（删除选中的部件或管路）。</summary>
    public System.Windows.Input.ICommand DeleteSelectedCommand => Diagram.DeleteSelectedCommand;

    /// <summary>在指定画布坐标处创建部件。</summary>
    public void AddNodeAt(NodeKind kind, double x, double y)
    {
        var display = NodeKindDefaults.DisplayName(kind);
        Diagram.AddNode(kind, x, y, display);
        StatusText = $"已添加部件：{display}";
    }

    public bool IsEditable
    {
        get => _diagram.IsEditable;
        set { _diagram.IsEditable = value; Raise(); }
    }

    public List<PaletteItem> Palette { get; }
    public PaletteItem? SelectedPalette { get => _selectedPalette; set => Set(ref _selectedPalette, value); }
    public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

    public RelayCommand SaveCommand { get; }
    public RelayCommand ResetDemoCommand { get; }
    public RelayCommand AddNodeCommand { get; }
    public RelayCommand AddConnectionCommand { get; }

    private static string FilePath => DiagramSerializer.DefaultFilePath;

    private void Save()
    {
        try
        {
            DiagramSerializer.Save(Diagram.ToData(), FilePath);
            StatusText = $"已保存：{FilePath}";
        }
        catch (Exception ex) { StatusText = $"保存失败：{ex.Message}"; }
    }

    private static DiagramViewModel? TryLoadFile()
    {
        try { var data = DiagramSerializer.Load(FilePath); return data is null ? null : DiagramViewModel.FromData(data); }
        catch { return null; }
    }

    private static DiagramViewModel BuildDemo()
    {
        var d = new DiagramViewModel(1400, 900);
        var pump = d.AddNode(NodeKind.PumpModule, 300, 60, "进样注射泵");
        var sample = d.AddNode(NodeKind.SampleCup, 60, 110);
        var wash = d.AddNode(NodeKind.NeedleWash, 55, 290);
        var cuvette = d.AddNode(NodeKind.Cuvette, 55, 500);
        var waste = d.AddNode(NodeKind.Waste, 240, 520);
        var reaction = d.AddNode(NodeKind.ReactionCup, 400, 520);
        d.AddNode(NodeKind.Centrifuge, 560, 520);

        NodeViewModel B(double x, double y, string label) => d.AddNode(NodeKind.ReagentBottle, x, y, label);
        var r1 = B(660, 40, "纳氏试剂");
        var r2 = B(780, 40, "酒石酸钾钠");
        var r3 = B(900, 40, "4-氨基安替比林");
        var r4 = B(1020, 40, "硝普钠");
        var r5 = B(660, 210, "纯水");
        var r6 = B(780, 210, "缓冲溶液");
        var r7 = B(900, 210, "标液");
        var r8 = B(1020, 210, "蒸馏仪");

        PortViewModel Port(NodeViewModel n, string id)
            => n.Ports.First(p => p.Id == id);

        d.AddConnection(sample, Port(sample, "R"), pump, Port(pump, "L1"), FlowMode.Forward);
        d.AddConnection(pump, Port(pump, "L2"), wash, Port(wash, "R"), FlowMode.Forward);
        d.AddConnection(pump, Port(pump, "B1"), cuvette, Port(cuvette, "R"), FlowMode.Forward);
        d.AddConnection(pump, Port(pump, "B2"), waste, Port(waste, "T"), FlowMode.Forward);
        d.AddConnection(pump, Port(pump, "B3"), reaction, Port(reaction, "T"), FlowMode.Forward);
        foreach (var reagent in new[] { r1, r2, r3, r4 })
            d.AddConnection(reagent, Port(reagent, "T"), pump, Port(pump, "R1"), FlowMode.Forward);
        foreach (var reagent in new[] { r5, r6, r7, r8 })
            d.AddConnection(reagent, Port(reagent, "T"), pump, Port(pump, "R2"), FlowMode.Forward);

        return d;
    }
}