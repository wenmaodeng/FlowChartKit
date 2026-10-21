using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Nodes;

/// <summary>
/// Demo 全部自定义部件的统一注册入口：App 启动与测试程序集初始化共用。
/// 幂等——重复调用直接跳过（注册表 Register 会覆盖，但保持"只注册一次"避免缓存反复失效）。
/// </summary>
public static class DemoNodeCatalog
{
    private static bool _registered;

    public static void RegisterAll()
    {
        if (_registered) return;
        _registered = true;

        NodeDefinitionRegistry.Register(new SampleCupDefinition());
        NodeDefinitionRegistry.Register(new ReagentBottleDefinition());
        NodeDefinitionRegistry.Register(new WasteDefinition());
        NodeDefinitionRegistry.Register(new ReactionCupDefinition());
        NodeDefinitionRegistry.Register(new CuvetteDefinition());
        NodeDefinitionRegistry.Register(new CentrifugeDefinition());
        NodeDefinitionRegistry.Register(new NeedleWashDefinition());
        NodeDefinitionRegistry.Register(new ThreeWayValveDefinition());
        NodeDefinitionRegistry.Register(new PumpModuleDefinition());
        NodeDefinitionRegistry.Register(new SolenoidValveDefinition());
    }
}
