using System.Windows;
using System.Windows.Input;
using FlowChartKit;

namespace FlowChartKit.Demo;

public partial class MainWindow : Window
{
    private NodeKind? _pendingCreateKind;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    /// <summary>点击部件库条目：进入"点击画布添加部件"模式。</summary>
    private void PaletteItem_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is PaletteItem pi)
        {
            _pendingCreateKind = pi.Kind;
            if (DataContext is MainViewModel vm)
                vm.StatusText = "点击画布添加部件：" + pi.DisplayName;
        }
    }

    /// <summary>画布点击：若处于添加模式则放置部件，否则清除选中。</summary>
    private void DiagramCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        if (_pendingCreateKind is NodeKind kind)
        {
            var pos = e.GetPosition(DiagramCanvas);
            vm.AddNodeAt(kind, pos.X, pos.Y);
            _pendingCreateKind = null;
            vm.StatusText = "就绪";
            e.Handled = true;
            return;
        }

        // 点击空白处清除选中
        vm.Diagram.SelectedNode = null;
        vm.Diagram.SelectedConnection = null;
    }
}
