using System.Windows;
using FlowChartKit.Demo.Nodes;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // 自定义部件先注册再建窗：节点构造即按 Kind 解析定义，右键菜单经注册表枚举生效定义
        NodeDefinitionRegistry.Register(new SolenoidValveDefinition());
        base.OnStartup(e);
    }
}
