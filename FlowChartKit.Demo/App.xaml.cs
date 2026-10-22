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
        // 历史内置部件已移入 Demo 作为宿主自定义部件（连同电磁阀），统一经 DemoNodeCatalog 注册
        DemoNodeCatalog.RegisterAll();

        // 管路定义同理：内置 Standard 之外，蒸汽/废液/采样三种宿主管路在此注册，
        // 注册后进入右键菜单「添加管路 → 扩展管路」与管路属性对话框的「管路类型」下拉
        DemoPipeCatalog.RegisterAll();

        base.OnStartup(e);
    }
}
