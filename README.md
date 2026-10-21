# FlowChartKit

WPF 流程图控件库，面向工业上位机/设备组态场景：部件 + 管路 + 流向的编辑与运行双模式画布。全部图形经 C# `OnRender` 矢量自绘，高 DPI 下清晰，无 ControlTemplate 依赖。

## 特性

- **部件定义自管理**：名称、基准尺寸、端点、外观集中在一个 `INodeDefinition` 实现里，经 `NodeDefinitionRegistry` 注册（内置层只读 + 宿主覆盖层）
- **内置部件**：LED 指示灯（四色循环）、开关（通/断），支持双击自切换与程序化状态下发
- **宿主扩展**：业务部件类型由宿主常量定义（数值落盘），定义类 + 一次注册即可入画布与右键菜单
- **自适应画布**：`AutoFitCanvas` 默认开启，画布尺寸自动跟随控件可视区，无水平/垂直滚动条；关闭则按存档尺寸显示并可滚动
- **管路系统**：正交布线、端口吸附、自由端、流向与流动画
- **编辑/运行双模式**：`IsEditable` 切换；运行模式点击经 `NodeClickCommand` 上抛宿主 VM（零控件引用）
- **命令桥双向**：`SetDirectionByNumberCommand` / `SetNodeStateByNumberCommand` 按编号下发，`NodeClickCommand` 上抛
- **布局存档**：JSON 序列化带版本字段，载入前预校验、失败时画布状态一致
- **现场控制**：`AllowContextMenu` / `AllowNodeInteraction` 独立开关，坐标全入口钳位，键盘支持（Delete / 方向键 / Esc）

## 安装

```bash
dotnet add package FlowChartKit
```

支持 .NET 6 / 7 / 8 / 9 / 10（`net6.0-windows` ~ `net10.0-windows`）、WPF。除 WPF 框架引用外零第三方依赖。

## 快速接入

```xml
<Window xmlns:fc="clr-namespace:FlowChartKit.Controls;assembly=FlowChartKit">
    <fc:DiagramCanvasControl IsEditable="{Binding EditMode}"
                             NodeClickCommand="{Binding NodeClickCommand}"
                             SetNodeStateByNumberCommand="{Binding SetNodeStateCommand, Mode=OneWayToSource}" />
</Window>
```

宿主自定义部件三步：实现 `INodeDefinition` → 启动时 `NodeDefinitionRegistry.Register` → 在 `NodeKind` 数值区间内定义部件类型常量。完整示例见仓库 Demo 工程。

除注册表外，全部 API 限 UI 线程调用。

## 文档

- [发布说明与版本历史](CHANGELOG.md)
- 公共 API 的 XML 文档随包分发（IDE 悬浮提示可直接查阅接入契约）

## License

[MIT](LICENSE)
