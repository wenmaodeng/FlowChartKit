using System.Windows;

namespace FlowChartKit.Demo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // 宿主定义已在 App.OnStartup 注册；此处把画布的 Diagram 交给 VM，
        // 便于 Demo 直接用库 API 编排示例（生成管路示例、添加无名称部件等）
        Loaded += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
                vm.Diagram = Canvas.Diagram;
        };
    }
}
