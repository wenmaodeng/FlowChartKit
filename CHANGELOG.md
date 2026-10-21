# FlowChartKit 发布说明

## 1.0.0（2026-10-21）

首个正式版本。WPF 流程图控件库，面向工业上位机/设备组态场景：部件 + 管路 + 流向的编辑与运行双模式画布。

### 平台要求

| 项目 | 要求 |
|---|---|
| 框架 | .NET 10（`net10.0-windows`） |
| UI 框架 | WPF（`Microsoft.WindowsDesktop.App.WPF` 框架引用，零第三方依赖） |
| 线程模型 | 除 `NodeDefinitionRegistry` 外，全部 API 限 **UI 线程**调用 |

### 核心特性

- **部件定义自管理**：名称、基准尺寸、端点、外观集中在一个 `INodeDefinition` 实现里，经 `NodeDefinitionRegistry` 注册（双层结构：内置只读层 + 宿主覆盖层，注销覆盖后自动回退内置）。
  - 内置部件：LED 指示灯（`LedColorState` 四色）、开关（`SwitchState` 通/断），均支持双击自切换与 `SetNodeStateByNumberCommand` 程序化下发。
  - 宿主扩展：`NodeKind` 仅含内置类型，业务部件类型由宿主以常量扩展（数值落盘，参考 Demo 的 `DemoNodeKinds` 1000-1009 区间约定）。
- **管路系统**：正交布线（`OrthogonalRouter`）、端口吸附、自由端、流向（正向/反向/无）与流动画（卸载时自动停止，无时钟泄漏）。
- **编辑/运行双模式**：`IsEditable` 控制编辑能力；编辑模式点击仅选中/拖拽，运行模式点击经 `NodeClickCommand` 冒泡给宿主 VM；运行模式下端点拖拽与模式切换互斥，中途切换自动终止。
- **命令桥双向**：
  - 下发：`SetDirectionByNumberCommand` / `SetNodeStateByNumberCommand`（部件类型 + 同类序号定位，与画布序号徽章一致）。
  - 上抛：`NodeClickCommand`（VM 零控件引用）。
- **布局存档**：`DiagramSerializer` JSON 序列化（带 `Version` 字段）；载入前整体预校验未知部件类型，失败时画布状态一致（无半载入）。
- **现场控制**：`AllowContextMenu` 禁用右键菜单；坐标全入口钳位；键盘支持（Delete 删除、方向键微调、Esc 取消）。

### 快速接入

```xml
<Window xmlns:fc="clr-namespace:FlowChartKit.Controls;assembly=FlowChartKit">
    <fc:DiagramCanvasControl IsEditable="{Binding EditMode}"
                             NodeClickCommand="{Binding NodeClickCommand}"
                             SetNodeStateByNumberCommand="{Binding SetNodeStateCommand, Mode=OneWayToSource}" />
</Window>
```

宿主自定义部件三步：实现 `INodeDefinition` → 启动时 `NodeDefinitionRegistry.Register` → （可选）以 `internal` 之外的公共类型放宿主程序集。完整示例见仓库 Demo 工程。

### 已知限制

- 暂无撤销/重做、多选框选、画布缩放（后续版本按需加入）。
- 载入失败时 `LoadAsync` 返回 `null`，不区分"文件不存在"与"内容损坏"；错误详情暂未对外暴露。
- 超大布局（500+ 部件）性能未做专项压测。

### 升级与兼容性说明

- 存档含 `Version` 字段（当前 1）。未来 schema 变更将依据此字段提供迁移；本版本为首次发布，无历史格式负担。
- `NodeKind` 枚举仅包含核心库内置类型（`Led`、`Switch`），扩展值由宿主常量自行定义——升级库不会影响宿主已落盘的业务部件数值。
