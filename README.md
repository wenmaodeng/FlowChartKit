# FlowChartKit

WPF 流程图控件库，面向工业上位机/设备组态场景：部件 + 管路 + 流向的编辑与运行双模式画布。全部图形经 C# `OnRender` 矢量自绘，高 DPI 下清晰，无 ControlTemplate 依赖。

## 特性

- **部件定义自管理**：名称、基准尺寸、端点、外观集中在一个 `INodeDefinition` 实现里，经 `NodeDefinitionRegistry` 注册（内置层只读 + 宿主覆盖层）
- **内置部件**：LED 指示灯（四色循环）、开关（通/断），支持双击自切换与程序化状态下发
- **宿主扩展**：业务部件类型由宿主常量定义（数值落盘），实现一个 `INodeDefinition` + 一次注册即可入画布与右键菜单（见下文三通阀示例）
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

## 宿主自定义部件

以扩展一个**三通阀**为例，三步接入：定义类型常量 → 实现 `INodeDefinition` → 启动时注册。

### 1. 定义部件类型常量

`NodeKind` 仅含库内置的 `Led`、`Switch`，业务部件由宿主用常量扩展（按数值落盘，升级库不影响已存档的数值）：

```csharp
using FlowChartKit;

public static class HostNodeKinds
{
    public const NodeKind ThreeWayValve = (NodeKind)1000;   // 三通阀
}
```

### 2. 实现 INodeDefinition

名称、基准尺寸、端口、外观集中在一个类里自管理：

```csharp
using System.Windows;
using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

public sealed class ThreeWayValveDefinition : INodeDefinition
{
    public NodeKind Kind => HostNodeKinds.ThreeWayValve;
    public string DisplayName => "三通阀";
    public (double W, double H) BaseSize => (56, 52);

    // 端口：Id + 相对位置（FX/FY 为宽高比例 0~1）+ 出口方向
    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec("L", 0,   0.5, PortDir.Left),
        new PortSpec("R", 1,   0.5, PortDir.Right),
        new PortSpec("B", 0.5, 1,   PortDir.Down),
    ];

    // 在基准尺寸坐标系内绘制（缩放由控件施加）；实例须无状态，画刷/几何建议冻结复用
    public void Render(DrawingContext dc)
    {
        // 圆形阀体
        dc.DrawEllipse(NodeRenderer.FillWhite, NodeRenderer.OutlinePen, new Point(28, 26), 16, 16);
        // 内部 Y 形流道指示（左 / 右 / 下三个端口方向）
        NodeRenderer.DrawPath(dc, "M14,26 L28,26 L42,26", null, NodeRenderer.AccentPen);
        NodeRenderer.DrawPath(dc, "M28,26 L28,40", null, NodeRenderer.AccentPen);
    }
}
```

- 四向端口可直接用内置预设 `NodePorts.Standard`（T/B/L/R），省去手写端口表。
- 需要按状态区分外观时，override `Render(DrawingContext dc, NodeViewModel node)`，从 `node.State` 读取宿主业务载荷自行解释。
- 需要自驱动交互（如双击切换自身状态）时，提供 `Events` 目录并实现 `InvokeEvent`。

### 3. 启动时注册

```csharp
NodeDefinitionRegistry.Register(new ThreeWayValveDefinition());
```

注册表为**内置只读层 + 宿主覆盖层**双层结构（注册/注销可在任意线程调用）：

- 注册后立即出现在右键菜单，并可入画布与存档。
- `NodeDefinitionRegistry.Unregister(HostNodeKinds.ThreeWayValve)` 移除宿主定义；内置类型自动回退到内置定义，宿主扩展类型注销后再解析会抛 `InvalidOperationException`（载入含该类型的存档会在预校验阶段被拒）。
- 宿主也可用自定义定义覆盖内置类型外观（`Register` 优先于内置层），`IsBuiltIn(kind)` 可判断当前是否仍由内置定义提供。

除注册表外，全部 API 限 UI 线程调用。

## 文档

- [发布说明与版本历史](CHANGELOG.md)
- 公共 API 的 XML 文档随包分发（IDE 悬浮提示可直接查阅接入契约）

## License

[MIT](LICENSE)
