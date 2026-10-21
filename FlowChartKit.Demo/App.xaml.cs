using System.Windows;
using FlowChartKit.Demo.Nodes;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // 部件定义先注册再建窗：节点构造即按 Kind 解析定义，右键菜单经注册表枚举生效定义。
        // 历史内置部件已移入 Demo 作为宿主自定义部件（连同电磁阀），统一经 DemoNodeCatalog 注册
        DemoNodeCatalog.RegisterAll();
        base.OnStartup(e);
    }
}
