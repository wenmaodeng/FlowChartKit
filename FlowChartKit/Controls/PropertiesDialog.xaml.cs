using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FlowChartKit.Rendering;
using FlowChartKit.ViewModels;

namespace FlowChartKit.Controls;

/// <summary>部件 / 管路属性窗口，由画布右键菜单的"属性"项弹出。</summary>
public partial class PropertiesDialog : Window
{
    public PropertiesDialog()
    {
        InitializeComponent();
    }

    /// <summary>显示指定部件或管路的属性。</summary>
    public static void Show(Window? owner, object? target)
    {
        var dialog = new PropertiesDialog { Owner = owner };

        switch (target)
        {
            case NodeViewModel node:
                dialog.Title = $"部件属性 — {node.Label}";
                dialog.BuildNode(node);
                break;
            case ConnectionViewModel connection:
                dialog.Title = $"管路属性 — {connection.DisplayName}";
                dialog.BuildConnection(connection);
                break;
            default:
                return;
        }

        dialog.ShowDialog();
    }

    private void BuildNode(NodeViewModel node)
    {
        AddReadOnly("Id", node.Id);
        AddReadOnly("类型", NodeDefinitionRegistry.Resolve(node.Kind).DisplayName);
        AddText("名称", () => node.Label, v =>
        {
            node.Label = v;
            Title = $"部件属性 — {node.Label}";
        });
        AddNumber("位置 X", () => node.X, v => node.X = v);
        AddNumber("位置 Y", () => node.Y, v => node.Y = v);
        AddNumber("缩放", () => node.Scale, v => node.Scale = v);
        AddSize(node);
        AddReadOnly("端口", string.Join("、", node.Ports.Select(p => $"{p.Id}({p.Dir})")));
    }

    /// <summary>尺寸行：缩放系数改动后实时刷新。</summary>
    private void AddSize(NodeViewModel node)
    {
        var text = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };

        Refresh();
        // 具名处理器并在 Closed 退订：匿名订阅会捕获 dialog，关闭后仍被节点根住无法回收
        PropertyChangedEventHandler handler = (_, e) =>
        {
            if (e.PropertyName is nameof(NodeViewModel.ShapeWidth) or nameof(NodeViewModel.ShapeHeight))
                Refresh();
        };
        node.PropertyChanged += handler;
        Closed += (_, _) => node.PropertyChanged -= handler;

        AddRow("尺寸", text);

        void Refresh() => text.Text =
            $"{node.ShapeWidth:0.##} × {node.ShapeHeight:0.##}（基准 {node.BaseWidth:0.##} × {node.BaseHeight:0.##}）";
    }

    private void BuildConnection(ConnectionViewModel connection)
    {
        AddReadOnly("Id", connection.Id);
        AddReadOnly("编号", $"#{connection.Number}");
        AddReadOnly("名称", connection.DisplayName);
        AddDirection(connection);
        AddReadOnly("起点", DescribeEnd(connection.SourceNode, connection.SourcePort, connection.FreeSource));
        AddReadOnly("终点", DescribeEnd(connection.TargetNode, connection.TargetPort, connection.FreeTarget));
        AddReadOnly("折线点数", connection.Points.Length.ToString());
    }

    private static string DescribeEnd(NodeViewModel? node, PortViewModel? port, Point? free)
    {
        if (node != null) return $"{node.Label} · 端口 {port?.Id ?? "-"}";
        if (free != null) return $"自由端 ({free.Value.X:0.##}, {free.Value.Y:0.##})";
        return "未连接";
    }

    /// <summary>流向下拉框：改动直接赋值 ConnectionViewModel.Direction。</summary>
    private void AddDirection(ConnectionViewModel connection)
    {
        var box = new ComboBox { Padding = new Thickness(4, 3, 4, 3) };

        (string Text, FlowMode Mode)[] options =
        [
            ("正向", FlowMode.Forward),
            ("反向", FlowMode.Reverse),
            ("停止", FlowMode.None),
        ];

        foreach (var (text, mode) in options)
        {
            var item = new ComboBoxItem { Content = text, Tag = mode };
            box.Items.Add(item);
            if (connection.Direction == mode) box.SelectedItem = item;
        }

        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedItem is ComboBoxItem { Tag: FlowMode mode })
                connection.Direction = mode; // 直接赋值 Direction setter 会触发 Recompute()
        };

        AddRow("流向", box);
    }

    private void AddReadOnly(string name, string text)
        => AddRow(name, new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        });

    /// <summary>可编辑的数值行：回车或失去焦点时提交，提交后回填实际生效值（缩放会被夹取到取值区间）。</summary>
    private void AddNumber(string name, Func<double> get, Action<double> commit)
    {
        var box = new TextBox { Text = get().ToString("0.##"), Padding = new Thickness(4, 3, 4, 3) };
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) Commit(); };
        box.LostKeyboardFocus += (_, _) => Commit();
        AddRow(name, box);

        void Commit()
        {
            if (double.TryParse(box.Text, out var v)) commit(v);
            box.Text = get().ToString("0.##");
        }
    }

    /// <summary>可编辑的文本行：回车或失去焦点时提交，提交后回填实际生效值。</summary>
    private void AddText(string name, Func<string> get, Action<string> commit)
    {
        var box = new TextBox { Text = get(), Padding = new Thickness(4, 3, 4, 3) };
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) Commit(); };
        box.LostKeyboardFocus += (_, _) => Commit();
        AddRow(name, box);

        void Commit()
        {
            string text = box.Text.Trim();
            // 空白视为取消：回填原值。部件名称为空时载入会回退到类型默认名，留空没有意义
            if (text.Length > 0) commit(text);
            box.Text = get();
        }
    }

    private void AddRow(string name, UIElement editor)
    {
        var label = new TextBlock
        {
            Text = name,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x60, 0x70)),
        };

        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
        row.ColumnDefinitions.Add(new ColumnDefinition());

        Grid.SetColumn(label, 0);
        Grid.SetColumn(editor, 1);
        row.Children.Add(label);
        row.Children.Add(editor);

        Rows.Children.Add(row);
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
