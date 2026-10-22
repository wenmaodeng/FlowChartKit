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
            case TextBlockViewModel text:
                dialog.Title = $"文本属性 — #{text.Number}";
                dialog.BuildTextBlock(text);
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
        AddNumber("标签字号", () => node.LabelFontSize, v => node.LabelFontSize = v);
        AddSize(node);
        AddReadOnly("端口", string.Join("、", node.Ports.Select(p => $"{p.Id}({p.Dir})")));
        AddState(node);
    }

    /// <summary>文本模块属性：内容 / 字号 / 位置（文本即"名称"，回车或失焦提交并即时刷新画布）。</summary>
    private void BuildTextBlock(TextBlockViewModel text)
    {
        AddReadOnly("Id", text.Id);
        AddReadOnly("编号", text.Number.ToString());
        AddText("内容", () => text.Content, v =>
        {
            text.Content = v;
            Title = $"文本属性 — #{text.Number}";
        });
        AddNumber("字号", () => text.FontSize, v => text.FontSize = v);
        AddNumber("位置 X", () => text.X, v => text.X = v);
        AddNumber("位置 Y", () => text.Y, v => text.Y = v);
    }

    /// <summary>
    /// 状态行：宿主经 SetNodeStateByNumberCommand / 部件事件写入的业务状态（object 载荷，控件不解释语义），
    /// 此处仅作字符串化展示。宿主在对话框打开期间变更状态（如双击切换、命令下发）时实时刷新。
    /// </summary>
    private void AddState(NodeViewModel node)
    {
        var text = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };

        Refresh();
        // 具名处理器并在 Closed 退订（与尺寸行同一模式），防对话框生命周期被节点根住
        PropertyChangedEventHandler handler = (_, e) =>
        {
            if (e.PropertyName == nameof(NodeViewModel.State))
                Refresh();
        };
        node.PropertyChanged += handler;
        Closed += (_, _) => node.PropertyChanged -= handler;

        AddRow("状态", text);

        void Refresh() => text.Text = node.State?.ToString() ?? "（未设置）";
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
        AddPipeKind(connection);
        AddRouting(connection);
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

    /// <summary>
    /// 管路类型下拉框：列出注册表中全部管路定义（内置 + 宿主扩展），
    /// 选中即赋值 <see cref="ConnectionViewModel.PipeKind"/>，颜色/粗细/流动画即时生效。
    /// </summary>
    private void AddPipeKind(ConnectionViewModel connection)
    {
        var box = new ComboBox { Padding = new Thickness(4, 3, 4, 3) };

        foreach (var def in PipeDefinitionRegistry.All)
        {
            var header = PipeDefinitionRegistry.IsBuiltIn(def.Kind)
                ? def.DisplayName
                : $"{def.DisplayName}（扩展）";
            var item = new ComboBoxItem { Content = header, Tag = def.Kind };
            box.Items.Add(item);
            if (connection.PipeKind == def.Kind) box.SelectedItem = item;
        }

        // 当前类型未注册（宿主换了程序集）：补一项占位，避免下拉框显示为空
        if (box.SelectedItem is null && PipeDefinitionRegistry.IsAvailable(connection.PipeKind))
        {
            var current = new ComboBoxItem
            {
                Content = PipeDefinitionRegistry.Resolve(connection.PipeKind).DisplayName,
                Tag = connection.PipeKind
            };
            box.Items.Add(current);
            box.SelectedItem = current;
        }

        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedItem is ComboBoxItem { Tag: PipeKind kind })
                connection.PipeKind = kind;
        };

        AddRow("管路类型", box);
    }

    /// <summary>
    /// 连线布局下拉框：垂直水平（可拖中间线段调整绕行）/ 直线 / 弧线，
    /// 选中即赋值 <see cref="ConnectionViewModel.Routing"/>，路径即时重算。
    /// </summary>
    private void AddRouting(ConnectionViewModel connection)
    {
        var box = new ComboBox { Padding = new Thickness(4, 3, 4, 3) };

        foreach (var routing in new[] { PipeRouting.Orthogonal, PipeRouting.Straight, PipeRouting.Curved })
        {
            var item = new ComboBoxItem
            {
                Content = PipeRoutingDefaults.DisplayName(routing),
                Tag = routing
            };
            box.Items.Add(item);
            if (connection.Routing == routing) box.SelectedItem = item;
        }

        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedItem is ComboBoxItem { Tag: PipeRouting routing })
                connection.Routing = routing;
        };

        AddRow("连线布局", box);

        // 垂直水平布局才用得到绕行位置：复位后该管路回到"按端口方向自动走线"
        var reset = new Button
        {
            Content = "复位为自动走线",
            Padding = new Thickness(6, 3, 6, 3),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        reset.Click += (_, _) => connection.SetDetour(null, null);
        AddRow("绕行位置", reset);
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
            // 空串是合法值（如清空部件名称）：照常提交并回填
            commit(text);
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
