using System.Runtime.CompilerServices;
using FlowChartKit.Demo.Nodes;

namespace FlowChartKit.Tests;

/// <summary>
/// 测试程序集加载时把宿主全部部件定义注册进注册表（幂等）。
/// 核心库内置层现仅含 LED 与开关，注射泵等定义由宿主（Demo 工程）以自定义部件提供；
/// 各测试用例依赖这些类型可解析（如 HostNodeKinds.SyringePump）。
/// 变更类注册表测试仍遵循"限定特定类型 + try/finally 即时恢复"的约定，与此初始化互不干扰。
/// </summary>
internal static class TestModuleInitializer
{
    [ModuleInitializer]
    internal static void Initialize() => HostNodeCatalog.RegisterAll();
}
