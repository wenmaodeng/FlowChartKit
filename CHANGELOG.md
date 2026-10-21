# FlowChartKit 发布说明

## 1.2.2（2026-10-21）

修复并设置 `DiagramCanvasControl` 的背景色：宿主设置 `Background` 即刻生效。

### 变更

- **`Background` 现在表示画布底色**：内部 `Canvas` 的背景由硬编码 `#FDFEFF` 改为绑定到控件自身的 `Background`，宿主设置后直接生效。
  ```xml
  <fc:DiagramCanvasControl Background="Red" ... />
  ```
- **默认值不变**：未设置 `Background` 时仍为原 `#FDFEFF`（通过 `OverrideMetadata` 设为属性默认值，冻结 Brush 共享），旧工程外观无变化。
- 支持任意 `Brush`（纯色、渐变、图片画刷等），而非仅颜色字符串。

### 说明

- 此前 `Background` 无效的原因：控件可视树为 `UserControl → ScrollViewer → Canvas`，后两者的背景在 XAML 中硬编码，覆盖了父级背景。
- 画布外衬底（mat，`#EEF2FA`）仍为固定值：仅在 `AutoFitCanvas="False"` 且画布小于控件时可见；自适应模式（默认）下画布铺满控件，该区域不可见。

## 1.2.1（2026-10-21）

`AutoFitCanvas` 默认值由 `false` 改为 **`true`**——默认即"画布自适应控件大小、无滚动条"。

### 变更

- **`DiagramCanvasControl.AutoFitCanvas` 默认改为 `true`**：宿主不写该属性即为自适应模式，画布尺寸跟随控件可视区，水平/垂直滚动条隐藏，越界部件自动钳位回画布内。
- 需要旧行为（按存档 `Width`/`Height` 显示、超出部分滚动查看）请显式设置：
  ```xml
  <fc:DiagramCanvasControl AutoFitCanvas="False" ... />
  ```
- 修复默认值不触发依赖属性回调导致的初始状态未应用问题（构造时显式应用滚动条显隐）。

### 兼容性说明

- 1.2.0 中显式写了 `AutoFitCanvas="True"` 的工程不受影响。
- **1.2.0 及更早版本未写该属性的工程，升级到 1.2.1 后默认行为会变化**（由"固定画布 + 滚动"变为"自适应 + 无滚动条"）；若依赖滚动查看大布局，请显式设为 `False`。

## 1.2.0（2026-10-21）

新增画布自适应宿主大小能力，解决画布逻辑尺寸大于控件时出现滚动条的问题。

### 新增

- **`DiagramCanvasControl.AutoFitCanvas`**（`bool`，默认 `false`）：开启后画布逻辑尺寸自动收敛为控件可视区大小，并隐藏水平/垂直滚动条；窗口缩放时实时跟随。
  ```xml
  <fc:DiagramCanvasControl AutoFitCanvas="True" ... />
  ```
- **`DiagramViewModel.Resize(width, height)`**：运行时调整画布尺寸，越界部件（`MoveTo`）与自由端（`SetFreeEnd`）同步钳位回新边界；宽高 ≤ 0 时忽略。宿主可自行调用以实现自定义尺寸策略。

### 行为说明

- 自适应模式下，`DiagramData` 的 `Width` / `Height`（默认 1400 × 900）不再决定显示范围；保存布局时写回当前实际尺寸。
- 收敛时机覆盖三种场景：控件尺寸变化、载入存档按存档值覆盖画布尺寸、属性切换。
- 默认 `false` 保持原有行为（按数据/存档尺寸显示，超出部分滚动查看），**1.1.0 及更早的工程升级无需任何改动**。

## 1.1.0（2026-10-21）

扩展目标框架，兼容 .NET 6 至 .NET 10，公共 API 与行为无变更。

### 变更内容

- **多目标框架**：库工程由 `net10.0-windows` 单目标改为多目标，包内同时提供：
  `net6.0-windows` / `net7.0-windows` / `net8.0-windows` / `net9.0-windows` / `net10.0-windows`。
- **消费方式不变**：宿主工程按自身 TFM 自动选配对应的 `lib` 目录，无需任何额外配置或条件引用。
- **依赖不变**：仍仅依赖 WPF 框架引用（`Microsoft.WindowsDesktop.App.WPF`），零第三方依赖；XML 文档随各 TFM 分发。
- **非 Windows 构建**：新增 `EnableWindowsTargeting=true`，便于在非 Windows 环境（如 Linux CI）构建 Windows TFM。

### 兼容性说明

- 本次仅为框架目标扩展，**无 API 增删、无行为变更**，1.0.0 的工程可直接升级，无需改代码。
- net6 / net7 已处于微软官方支持终止（EOL）状态，构建时相关 EOL 警告已抑制（`CheckEolTargetFramework=false`）；生产环境建议使用 net8 及以上。

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
