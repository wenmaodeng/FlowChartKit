using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Pipes;

/// <summary>
/// 宿主自定义管路的统一注册入口：与 <c>HostNodeCatalog</c> 对应，App 启动时调用一次。
/// 注册后即出现在右键菜单「添加管路 → 扩展管路」与管路属性对话框的「管路类型」下拉中。
/// </summary>
public static class HostPipeCatalog
{
    private static bool _registered;

    public static void RegisterAll()
    {
        if (_registered) return;
        _registered = true;

        PipeDefinitionRegistry.Register(new FlowPathPipeDefinition());
    }
}
