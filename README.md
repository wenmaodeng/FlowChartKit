# FlowChartKit

WPF 流程图控件库，面向工业上位机/设备组态场景：部件 + 管路 + 流向的编辑与运行双模式画布。全部图形经 C# `OnRender` 矢量自绘，高 DPI 下清晰，无 ControlTemplate 依赖。

![FlowChartKit 效果预览](https://raw.githubusercontent.com/wenmaodeng/FlowChartKit/main/demo.png)

## 特性

- **部件定义自管理**：名称、基准尺寸、端点、外观集中在一个 `INodeDefinition` 实现里，经 `NodeDefinitionRegistry` 注册（内置层只读 + 宿主覆盖层）
- **内置部件**：LED 指示灯（四色循环）、开关（通/断），支持双击自切换与程序化状态下发
- **宿主扩展**：业务部件类型由宿主常量定义（数值落盘），实现一个 `INodeDefinition` + 一次注册即可入画布与右键菜单（见下文三通阀示例）
- **自适应画布**：`AutoFitCanvas` 默认开启，画布尺寸自动跟随控件可视区，无水平/垂直滚动条；关闭则按存档尺寸显示并可滚动
- **管路系统**：端口吸附、自由端、流向与流动画；连线布局可选「垂直水平（正交，可拖动中间线段调整绕行）/ 直线 / 弧线」（`PipeRouting`），与管路样式独立组合
- **拖动对齐吸附**：拖动部件靠近其它部件的左/右/居中、上/下/居中时自动吸附并显示橙色捕捉线
- **管路样式可扩展**：颜色、粗细、流动画（虚线图案/速度/是否流动）由 `IPipeDefinition` 提供，经 `PipeDefinitionRegistry` 注册（内置普通管路 + 宿主扩展），可分"内置/扩展"两类并逐条切换（见下文蒸汽管示例）
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

## 宿主自定义管路

管路的颜色、粗细与流动画同样可扩展，模式与部件完全一致（`PipeKind` + `IPipeDefinition` + `PipeDefinitionRegistry`）。以扩展一条**蒸汽管**为例：

### 1. 定义管路类型常量

`PipeKind` 仅含内置的 `Standard`（普通管路），业务管路由宿主用常量扩展：

```csharp
using FlowChartKit;

public static class HostPipeKinds
{
    public const PipeKind Steam = (PipeKind)1000;   // 蒸汽管
}
```

### 2. 实现 IPipeDefinition

```csharp
using System.Windows.Media;
using FlowChartKit;
using FlowChartKit.Rendering;

public sealed class SteamPipeDefinition : IPipeDefinition
{
    public PipeKind Kind => HostPipeKinds.Steam;
    public string DisplayName => "蒸汽管";

    public Brush Stroke { get; } = NodeRenderer.Frozen(0xD0, 0x3A, 0x2B);
    public double Thickness => 3.5;

    public Brush SelectedStroke { get; } = NodeRenderer.Frozen(0xFF, 0x8A, 0x00);
    public double SelectedThickness => 4.0;

    // 虚线图案 {6,3}、0.3s 一个循环（比默认更快、更长的虚线段）
    public PipeFlowSpec Flow { get; } = PipeFlowSpec.Create([6, 3], TimeSpan.FromSeconds(0.3));
}
```

- `Flow = PipeFlowSpec.None` 表示该类型**不流动**（即使设置了流向也恒为实线）。
- 画刷建议冻结后复用，可直接使用 `NodeRenderer.Frozen(r, g, b)`。

### 3. 启动时注册

```csharp
PipeDefinitionRegistry.Register(new SteamPipeDefinition());
```

### 使用方式

| 场景 | 做法 |
|---|---|
| 新建管路默认用该类型 | 画布设置 `DefaultPipeKind="{x:Static local:HostPipeKinds.Steam}"` |
| 右键菜单挑类型 | 空白处右键 →「添加管路」→「扩展管路」下按 `DisplayName` 列出 |
| 切换已有管路类型 | 选中管路 → 属性 →「管路类型」下拉 |
| 代码创建 | `diagram.AddFreeConnection(start, end, pipeKind: HostPipeKinds.Steam)` |

注册表同样是**内置只读层 + 宿主覆盖层**：

- 注册后立即出现在菜单与属性对话框，并随存档持久化（`ConnectionData.PipeKind`）。
- 用自定义定义覆盖 `PipeKind.Standard` 即可**全局改默认管路外观**（一行注册改掉所有普通管路的颜色/粗细/动画）。
- `Unregister(kind)` 后回退内置定义；载入存档时若宿主未注册该类型，自动回退为 `Standard`（不会解析失败）。

## 连线布局

每条管路的走线方式与管路样式（`PipeKind`）**独立组合**：既是"蒸汽管"也可以走弧线。

| 布局 | 说明 |
|---|---|
| `PipeRouting.Orthogonal`（默认） | 垂直水平：横平竖直折线 + 端口出线短直段，可拖动中间线段调整绕行 |
| `PipeRouting.Straight` | 直线：两端点直接连线（可能穿过部件） |
| `PipeRouting.Curved` | 弧线：三次贝塞尔，端口处切线方向与端口朝向一致 |

| 场景 | 做法 |
|---|---|
| 新建管路默认用该布局 | 画布设置 `DefaultPipeRouting="Curved"` |
| 切换已有管路 | 选中管路 → 属性 →「连线布局」下拉 |
| 代码创建 | `diagram.AddFreeConnection(start, end, routing: PipeRouting.Curved)` |
| 复位手动绕行 | 选中管路 → 属性 →「复位为自动走线」，或 `conn.SetDetour(null, null)` |

**垂直水平布局支持拖动中间线段**：水平段随鼠标上下移动、竖直段随鼠标左右移动（拖动中按 Esc 回退），路径始终保持横平竖直，绕行位置随存档持久化（`ConnectionData.DetourX / DetourY`）。旧存档无 `Routing` 字段按 `Orthogonal` 回读。

## 拖动对齐吸附

拖动部件靠近其它部件的**左 / 居中 / 右、上 / 居中 / 下**时自动吸附对齐，并显示橙色虚线捕捉线（覆盖两个部件的并集范围）：

```xml
<fc:DiagramCanvasControl AlignmentSnapEnabled="True"
                         AlignmentSnapDistance="6" />
```

| 开关 | 说明 |
|---|---|
| `AlignmentSnapEnabled` | 默认 `true`；设为 `false` 恢复自由拖动，不再生成捕捉线 |
| `AlignmentSnapDistance` | 默认 `6`px，进入该距离即吸附 |

代码侧：`DiagramViewModel.SnapNode(node, x, y)` 返回吸附后坐标与参考线集合（`AlignmentGuide`），当前参考线见 `DiagramViewModel.AlignmentGuides`，由画布的对齐线图层绘制（不参与命中测试）；拖动结束或退出编辑模式会自动清空。

## 文档

- [发布说明与版本历史](CHANGELOG.md)
- 公共 API 的 XML 文档随包分发（IDE 悬浮提示可直接查阅接入契约）

## License

[MIT](LICENSE)
