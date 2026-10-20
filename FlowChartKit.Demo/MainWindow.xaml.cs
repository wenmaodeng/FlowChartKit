using System.Windows;

namespace FlowChartKit.Demo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // ViewModel 在 XAML 里创建，回调由 OneWayToSource 绑定注入；
        // 代码后置只剩"载入后触发一次初始流向"这一件事
        Loaded += (_, _) => ((MainViewModel)DataContext).ApplyInitialFlow();
    }
}
