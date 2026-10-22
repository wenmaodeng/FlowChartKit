using System.Windows;
using FlowChartKit.Demo.Nodes;
using FlowChartKit.Demo.Pipes;
using FlowChartKit.Rendering;

namespace FlowChartKit.Demo;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // 部件定义先注册再建窗：节点构造即按 Kind 解析定义，右键菜单经注册表枚举生效定义。
        // 宿主自定义部件（注射泵）统一经 HostNodeCatalog 注册
        HostNodeCatalog.RegisterAll();

        // 管路定义同理：内置 Standard 之外，宿主管路「流路」在此注册，
        // 注册后进入右键菜单「添加管路 → 扩展管路」与管路属性对话框的「管路类型」下拉
        HostPipeCatalog.RegisterAll();

        base.OnStartup(e);
    }
}
