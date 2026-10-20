using System.Windows;

namespace FlowChartKit.Demo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // ViewModel 在 XAML 里创建；"设置流向"走控件的 SetDirectionCommand 依赖属性，
        // 经 OneWayToSource 绑定灌入 VM，代码后置无需任何注入或桥接逻辑。
    }
}
