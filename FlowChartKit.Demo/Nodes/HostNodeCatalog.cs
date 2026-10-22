using FlowChartKit.Rendering;

namespace FlowChartKit.Demo.Nodes;

/// <summary>
/// 宿主全部自定义部件的统一注册入口：App 启动与测试程序集初始化共用。
/// 幂等——重复调用直接跳过（注册表 Register 会覆盖，但保持"只注册一次"避免缓存反复失效）。
/// </summary>
public static class HostNodeCatalog
{
    private static bool _registered;

    public static void RegisterAll()
    {
        if (_registered) return;
        _registered = true;

        NodeDefinitionRegistry.Register(new SyringePumpDefinition());
        NodeDefinitionRegistry.Register(new PureWaterBucketDefinition());
        NodeDefinitionRegistry.Register(new WasteBucketDefinition());
        NodeDefinitionRegistry.Register(new SampleSlotDefinition());
        NodeDefinitionRegistry.Register(new WashSlotDefinition());
        NodeDefinitionRegistry.Register(new Cuvette1CmDefinition());
        NodeDefinitionRegistry.Register(new Cuvette3CmDefinition());
        NodeDefinitionRegistry.Register(new ReagentBottleDefinition());
        NodeDefinitionRegistry.Register(new ExhaustValveDefinition());
        NodeDefinitionRegistry.Register(new DigestionPotDefinition());
        NodeDefinitionRegistry.Register(new WaterPumpDefinition());
        NodeDefinitionRegistry.Register(new FanDefinition());
        NodeDefinitionRegistry.Register(new Cuvette2CmDefinition());
        NodeDefinitionRegistry.Register(new ReactionCupDefinition());
        NodeDefinitionRegistry.Register(new SampleCupDefinition());
    }
}
