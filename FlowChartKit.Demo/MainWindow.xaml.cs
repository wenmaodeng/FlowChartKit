using System.Windows;

namespace FlowChartKit.Demo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // ViewModel 在 XAML 里创建；"设置流向"走 VM 的 SetDirectionRequested 事件，
        // 由画布控件订阅并落到图上，代码后置无需任何注入或桥接逻辑。
    }
}
