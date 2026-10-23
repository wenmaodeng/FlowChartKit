# FlowChartKit 发布说明

## 1.8.0（未发布）

新增**叠放次序（Z 序）**：管路 / 文本 / 部件现在位于**同一画布层**，可自由调整前后关系。

### 变更：统一画布层（管路 / 文本 / 部件同层）

- 原先三层固定叠放（管路 → 文本 → 部件）合并为**单一 ItemsControl**，元素前后关系由各自的 **`Z`** 决定（`Panel.ZIndex`）：
  - 三类元素（含管路）可**互相叠放**——文本可置于部件之上，管路也可置于顶层；
  - 同 `Z` 时按默认层级排列（管路 → 文本 → 部件），**旧存档行为不变**；
  - 命中优先级与视觉一致（WPF 按 ZIndex 命中），不再依赖类型硬编码顺序。
- **API**：`DiagramViewModel.CanvasItems`（`CompositeCollection`，供控件绑定）；`NodeViewModel.Z` / `TextBlockViewModel.Z` / `ConnectionViewModel.Z`；`DiagramViewModel.GetZ(item)` 读取。
- 新增字段随存档持久化（`NodeData.Z` / `TextBlockData.Z` / `ConnectionData.Z`）；旧存档无该字段按 0 回读，**`Version` 仍为 1，无需迁移**。

### 新增：叠放次序操作

- 右键选中任意元素 → 「**叠放次序**」子菜单：置于顶层 / 上移一层 / 下移一层 / 置于底层。
- 「**置于…之上 / 之下**」：进入目标选择模式，再点一个元素即完成（Esc 取消，状态经 `StatusChanged` 提示）。
- 画布命令（可自行接线到宿主 UI）：`ArrangeFrontCommand` / `ArrangeForwardCommand` / `ArrangeBackwardCommand` / `ArrangeBackCommand` / `ArrangeAboveCommand` / `ArrangeBelowCommand`（仅编辑模式且有选中项时可用）。
- **图模型 API**（不依赖控件）：
  `BringToFront(item)` / `SendToBack(item)` / `BringForward(item)` / `SendBackward(item)` / `MoveAbove(item, target)` / `MoveBelow(item, target)`；
  每次操作后统一重写 Z（100、200…），不会累积相同值或耗尽间隔。

### 新增：部件分方向缩放（右侧改宽 / 下方改高）

- 部件除右下角的**等比**手柄外，新增**右侧**（只改宽）与**下方**（只改高）两只拖动手柄：
  - 右侧手柄 → 只改 `ScaleX`（左边锚定，高度不变）；
  - 下方手柄 → 只改 `ScaleY`（上边锚定，宽度不变）；
  - 右下角手柄仍是等比；后两只**不保持宽高比**，可自由拉成任意比例。
- **API**：`NodeViewModel.ScaleX` / `ScaleY`（各自钳制 `MinScale`~`MaxScale`）、`SetScale(x, y)`（一次几何变更）、`DiagramViewModel.ResizeNodeWidthTo(node, canvasX)` / `ResizeNodeHeightTo(node, canvasY)`。
- `NodeViewModel.Scale` 保留为**等比**入口（赋值同时应用到两向，读取返回 `ScaleX`），旧代码与旧存档行为不变。
- 存档新增 `NodeData.ScaleX` / `ScaleY`；旧存档只有 `Scale` 时两向都按它回读，**`Version` 仍为 1，无需迁移**；新存档被旧版库读取时按 `Scale`（X 方向）等比还原。

## 1.7.0（2026-10-22）

编辑体验增强与画布元素扩展：新增**文本模块**（第三种画布元素）与配套的**按编号改文本命令桥**，正交管路拐角改为圆角，管路按流向区分颜色，管路线段可与自身段落对齐，拖动部件时显示四边距离，对齐吸附可随时开关。

### 新增：拖动部件时显示四边距离

- 拖动（移动）部件时，部件四周实时显示到画布**上 / 下 / 左 / 右**边界的距离（单位 px，贴边为 0），读数为白底橙框小标签，拖动结束即消失。
- 贴边时读数自动压回画布内，不会被画布边缘裁掉；下方读数避开部件标签带。
- **API**：`DiagramViewModel.GetNodeMargins(node)` 返回 `(Left, Top, Right, Bottom)` 四向剩余距离。

### 变更：管路按流向区分颜色（内置普通管路）

- 正向 / 反向：**#3161FF**（主色）；停止（无流向）：**#B1C4FF**（浅色）；两者均 2.0px，选中态仍为 #E67E22 / 2.6px。
- 停止时**不流动**（恒实线），与既往"无流向不启动动画"的行为一致。
- `IPipeDefinition` 新增两个**带默认实现的成员**（宿主不关心时无需改动）：
  - `Brush? StoppedStroke`：停止态颜色，返回 `null` 表示沿用 `Stroke`；
  - `double StoppedThickness`：停止态线宽，默认与 `Thickness` 相同。
- ⚠️ 视觉变更：内置普通管路的流动色由 `#6C9BD2` 改为 `#3161FF`；需要旧外观请宿主注册覆盖 `PipeKind.Standard` 的定义（把 `Stroke` 设回 `#6C9BD2`、`StoppedStroke` 设为 `null`）。

### 变更：管路线段拖动可与自身段落对齐

- 拖动正交管路的水平 / 竖直段时，除其它管路外，**该管路自身的其它水平 / 竖直段也参与吸附**——可把中间段拖到与起点侧 / 终点侧走线齐平（Z 形拉成 L 形）。
- 正在拖动的那一段自动排除（它跟随鼠标，参与会造成零偏差自吸附）；拖动会话会记住被拖段，快速移动也不会误判。

### 新增：对齐吸附可随时开关

- 右键菜单新增可勾选的「**对齐吸附**」项（对应已有的 `AlignmentSnapEnabled`），运行中即可开关，无需改 XAML。
- 拖动部件 / 管路线段时**按住 Alt** 可临时关闭当次拖动的吸附（用于像素级精细调整），只作用于这一次拖动，不改变宿主配置。
- **API**：`DiagramViewModel.SnapSuppressed`——由控件在拖动中置位，`SnapNode` / `SnapConnectionSegment` 都会跳过吸附并不生成捕捉线。

### 变更：正交管路拐角圆角

- 垂直水平（正交）布线的直角拐角按相邻段长自动加**圆角**（二次贝塞尔，圆弧与两邻段相切）：
  目标半径 10px，实际半径钳到相邻段长的一半——段越短圆角越小，短段不变形、首尾端点保持原位。
- 直线 / 弧线布局不受影响；编号徽章位置与命中判定仍按原折线计算。
- ⚠️ 视觉变更：正交管路的拐角由直角变为圆角；需要直角外观请修改 `PipeControl.CornerRadius`（当前 internal，可提为可配置项）。

### 新增：文本模块（第三种画布元素）

- 与部件 / 管路平级的「文本模块」：有**编号**（全图自增，随存档持久化）、**文本内容**、**字号**（6~48，默认 14），可拖动（编辑模式），图层层级为管路之上、部件之下。
- 入口：右键画布空白处 → **添加文本模块**；右键文本 → **属性** 编辑内容 / 字号 / 位置；选中 / 删除与部件、管路同一套交互（三者选中互斥）。
- 编辑模式且开启编号显示（`ShowNumbers`）时，文本左上角显示与管路同风格的**编号徽章**，便于与"按编号改文本"的编号对应。
- **API**：
  - `DiagramViewModel.Texts` 集合 + `AddTextItem(x, y, content?, fontSize?, id?)` / `RemoveTextItem` / `FindTextBlock(number)` / `SetTextContent(number, content)`
  - 存档新增 `Texts` 段（`TextBlockData`：Id/Number/X/Y/Content/FontSize）；旧存档无此字段自动为空表，**无需版本升级**

### 新增：SetTextByNumberCommand 命令桥（按编号改文本）

- 与 `SetDirectionByNumberCommand` 同一套模式：控件 Loaded 时灌入 `SetTextByNumberCommand`（DP），宿主 VM 以 `OneWayToSource` 绑定取走，参数传 **`SetTextRequest(Number, Content)`**，执行结果回填 `Result`（true=已修改 / false=编号不存在 / null=未执行）。
- 也可直接调用画布控件的 `SetTextContent(int number, string content)` 公共方法。

### 兼容性说明

- **接口兼容**：`IPipeDefinition` 的新成员带默认实现，现有宿主定义（含宿主扩展的流路 / 热水汽管等）无需任何改动即可编译；文本模块为纯新增 API，不改动既有成员签名。
- **存档兼容**：新增 `Texts` 段；旧存档（无该字段）反序列化为空表，**`DiagramData.Version` 仍为 1，无需迁移**。新存档被旧版库读取时 `Texts` 被忽略（文本丢失，其余内容正常）。
- **行为 / 视觉变更**：
  - 内置普通管路的颜色随流向变化（流动色 `#6C9BD2` → `#3161FF`），如依赖旧配色请覆盖 `PipeKind.Standard` 的定义；
  - 正交管路拐角由直角变为圆角；
  - 管路线段吸附新增"自身段落"候选。

## 1.6.0（2026-10-22）

宿主自定义部件绘制 API 的可空标注修正：「只填充不描边」的画法不再产生编译警告。

### 变更：DrawPath / DrawRoundRect 的 pen 参数可空

- `NodeRenderer.DrawPath(dc, data, fill, pen)` 与 `NodeRenderer.DrawRoundRect(dc, x, y, w, h, r, fill, pen)` 的 `pen` 参数由 `Pen` 改为 `Pen?`，与底层 `DrawingContext.DrawGeometry` 及同一签名的 `fill`（`Brush?`）保持一致。
- 自定义部件绘制**只填充不描边**的图形（字形、色块、接口方块等）时可直接传 `null`，不再触发 CS8625 警告，也无需写 `null!`。
- 两者仍按 `data` / `(x,y,w,h,r)` 缓存并冻结几何，重复渲染零开销。

### 兼容性说明

- **源码兼容**：仅放宽可空标注，现有调用方无需改动；传 `null` 的运行时行为与之前一致（表示不描边）。
- **无破坏性变更**：不涉及方法名、参数顺序或行为变化，旧版本代码可直接升级。
- Demo 工程同步新增 10 个宿主扩展示例部件（纯水桶、废液桶、样品位、洗针槽、1cm/3cm 比色皿、试剂瓶、排气阀、消解锅、水泵）与 2 条扩展管路（流路、热水汽管），仅作扩展示例，**不随 NuGet 包发布**。

## 1.5.0（2026-10-22）

部件缩放与标签字号、正交管路拖动与对齐吸附的编辑体验增强。

### 新增：按住部件右下角拖动缩放

- 编辑模式下选中（或悬停）部件时，图形**右下角**显示缩放手柄；按住它拖动即**等比**缩放（以左上角为锚点，X / Y 不变）。
- 缩放系数仍为 `NodeViewModel.Scale`（0.2 ~ 5 倍，随存档持久化）；拖动中受画布右下边界约束，不会把部件拖出画布。
- **API**：`DiagramViewModel.ResizeNodeTo(node, canvasX, canvasY)` 按画布坐标等比缩放（取宽、高比例较大者，并钳到画布与缩放上下限内）。
- 拖动中按 **Esc** 回退到拖动前的缩放系数；缩放不参与对齐吸附。

### 新增：部件标签字号可设置

- `NodeViewModel.LabelFontSize`：部件名称的字号（px，**不随部件缩放变化**，默认 11，钳制在 `MinLabelFontSize` 6 ~ `MaxLabelFontSize` 48）。
- 属性对话框新增「标签字号」；随存档持久化（`NodeData.LabelFontSize`），旧存档无该字段按 11 回读。
- 部件下方标签带高度随字号自动调整，大字号不会被截断。

### 新增：拖动管路线段的对齐捕捉

- 拖动正交管路的线段时，与**其它管路的水平 / 竖直段**共线对齐（不考虑部件）：**水平段**按 Y 吸附到其它管路的水平段，**竖直段**按 X 吸附到竖直段；被拖管路自身不参与。
- 命中阈值内取最近的一个候选，并显示对应的捕捉线（水平段横线、竖直段竖线，跨度覆盖两条管路的并集范围）。
- 沿用部件吸附的同一套开关与阈值：`AlignmentSnapEnabled` / `AlignmentSnapDistance`；拖动结束、Esc 取消、退出编辑模式都会清除捕捉线。
- **API**：`DiagramViewModel.SnapConnectionSegment(conn, horizontal, pos)` 返回 `SegmentSnapResult`（吸附后坐标 + 捕捉线）；`UpdateConnectionSegmentDrag` 内部先吸附再下发。

### 变更：正交管路的可拖动区域扩展到 L 形

- 原先只有 Z 形（4 点以上）的中间段可拖动；现在按**整条路径的弧长**判定：除去两端端口出线短直段（各 `OrthogonalRouter.DefaultStub` = 16px）以外的部分都可拖动。
- 因此 **L 形管路**（单折点）的两条边在让开端口出线后也能拖动，拖动后自动转为带绕行的 Z 形；拖动结束前按 Esc 回退，属性对话框「复位为自动走线」或 `SetDetour(null, null)` 可恢复原 L 形。
- **API**：`ConnectionViewModel.TryHitDraggableSegment(canvasPoint, out horizontal)` 供控件判定命中与拖动方向（水平段按 Y 移动、竖直段按 X 移动）。

### 兼容性说明

- **存档兼容**：新增字段均为缺省值（`NodeData.LabelFontSize` 缺省 11、`Scale` 缺省 1、`ConnectionData.Routing` 缺省 `Orthogonal`），1.4.0 及更早的存档可直接载入，无需迁移。
- **部件右下角新增缩放手柄**：仅在编辑模式且部件「已选中 / 悬停」时出现；该 8px 区域**优先于管路端点**响应，若自定义部件的端口落在右下角附近，该端口将无法直接拖动（改为先取消选中该部件即可）。
- **拖动吸附仍是同一套开关**：管路吸附沿用 `AlignmentSnapEnabled` / `AlignmentSnapDistance`，不需要吸附的工程设 `AlignmentSnapEnabled="False"` 即可整体关闭（部件与管路同时生效）。
- **正交路径的可拖区域扩大**：原只能拖 Z 形的中间段，现 L 形两条边也让开端口后可拖；不拖动时路径计算结果与旧版一致。

## 1.4.0（2026-10-22）

部件与管路的编辑体验增强：拖动部件时的对齐吸附捕捉线，管路连线布局可选（垂直水平 / 直线 / 弧线）。

![效果预览](https://raw.githubusercontent.com/wenmaodeng/FlowChartKit/main/demo.png)

### 新增：拖动对齐吸附

- 拖动部件靠近其它部件的**左/右/居中、上/下/居中**时自动吸附，并显示橙色虚线捕捉线（覆盖两部件的并集范围）。
- 触发距离 `AlignmentSnapDistance`（默认 6px）可调；画布设 `AlignmentSnapEnabled="False"` 可整体关闭，恢复自由拖动。
- 图模型侧对应 `DiagramViewModel.SnapNode / AlignmentGuides`，捕捉线由专用图层绘制（不参与命中测试）。

### 新增：管路连线布局可选

- 新枚举 `PipeRouting`：`Orthogonal`（垂直水平，默认，行为与旧版一致）/ `Straight`（直线）/ `Curved`（弧线，端口处切线方向与端口朝向一致）。
- 每条管路可在属性对话框「连线布局」下拉随时切换，与管路样式（`PipeKind`）独立组合。
- **垂直水平布局支持拖动中间线段**：水平段可上下移动、竖直段可左右移动，调整绕行路径（Esc 取消），绕行位置随存档持久化（`ConnectionData.DetourX / DetourY`）。
- **API**：`ConnectionViewModel.Routing` / `SetDetour(x, y)`（复位手动绕行）、`AddConnection(...)` / `AddFreeConnection(...)` 新增可选 `routing` 参数、画布 `DefaultPipeRouting`（新建管路默认布局）。
- **UI 入口**：管路属性对话框新增「连线布局」下拉与「复位为自动走线」按钮。
- **持久化**：`ConnectionData` 新增 `Routing`；旧存档无该字段按 `Orthogonal` 回读。

### 兼容性说明

- 新建管路默认仍为垂直水平布局，内置普通管路外观不变，未做任何扩展的工程升级后视觉与交互一致。
- 对齐吸附**默认开启**：已有工程升级后拖动手感会变化（自动吸附对齐），如需旧行为设 `AlignmentSnapEnabled="False"`。

## 1.3.0（2026-10-22）

管路的颜色、粗细与流动画改为**外部可扩展**，管路同部件一样分为内置与外置（宿主注册）两类；同时允许部件名称为空。

### 新增：管路样式可扩展

- **`PipeKind` 枚举**：内置 `Standard`（普通管路），宿主以常量扩展（如 `(PipeKind)1000`）。
- **`IPipeDefinition`**：一种管路的颜色/粗细/流动画集中在一个类里自管理。
  ```csharp
  public sealed class SteamPipeDefinition : IPipeDefinition
  {
      public PipeKind Kind => HostPipeKinds.Steam;
      public string DisplayName => "蒸汽管";
      public Brush Stroke { get; } = NodeRenderer.Frozen(0xD0, 0x3A, 0x2B);
      public double Thickness => 3.5;
      public Brush SelectedStroke { get; } = NodeRenderer.Frozen(0xFF, 0x8A, 0x00);
      public double SelectedThickness => 4.0;
      public PipeFlowSpec Flow { get; } = PipeFlowSpec.Create([6, 3], TimeSpan.FromSeconds(0.3));
  }
  ```
- **`PipeFlowSpec`**：流动画规格——虚线图案 `Dash`、一个循环时长 `Duration`、是否流动 `Enabled`；`PipeFlowSpec.None` 表示恒实线。
- **`PipeDefinitionRegistry`**：与部件注册表同构（内置只读层 + 宿主覆盖层），支持 `Register` / `Unregister` / `Resolve` / `IsAvailable` / `IsBuiltIn` / `All`。
- **`ConnectionViewModel.PipeKind`**：逐条管路的类型，变更即时重绘并重启动画。
- **`DiagramCanvasControl.DefaultPipeKind`**（依赖属性，默认 `PipeKind.Standard`）：新建管路使用的类型。
- **API**：`AddConnection(...)` / `AddFreeConnection(...)` 新增可选 `pipeKind` 参数。
- **UI 入口**：
  - 右键菜单「添加管路」改为子菜单：默认项 +「内置管路」/「扩展管路」分组。
  - 管路属性对话框新增「管路类型」下拉，可随时切换单条管路的类型。
- **持久化**：`ConnectionData` 新增 `PipeKind`；旧存档无该字段按 `Standard` 回读，宿主未注册的类型载入时自动回退 `Standard`。

### 变更：允许部件名称为空

- `NodeViewModel` 仅"未指定名称"（`null`）时回退类型默认名；显式传入空字符串表示"该部件不显示名称"，予以保留（原先空串/空白会被顶成默认名）。
- 属性对话框「名称」清空后不再被视为取消，空串合法提交，可正确存档与回读。

### 兼容性说明

- 内置普通管路的外观（`#6C9BD2` / 2.0px，选中 `#E67E22` / 2.6px，`{4,4}` 0.6s 流动）**完全不变**，未做任何扩展的工程升级后外观一致。
- 新建部件仍自动使用类型默认名（仅 `null` 回退），旧行为不变。
- 新增 API 均为可选扩展，无破坏性变更。

## 1.2.4（2026-10-22）

继续开放 `NodeRenderer` 中宿主自定义部件可能用到的颜色与工具，公共绘制面现已完整。

### 新增（原 internal / private → public）

- **画刷**：`PumpCoreFill`（0xCF,0xE0,0xF6 泵体阀芯浅蓝）、`PlungerFill`（0xB9,0xCD,0xE9 柱塞色）。
- **画笔**：`HighlightPen`（2.0px 橙色虚线，端口吸附高亮）。
- **工厂方法**：`Frozen(byte r, byte g, byte b)` 按 RGB 创建冻结纯色画刷，自定义部件可用它构造自己的配色；与 `MakePen(brush, thickness)` 配套使用。

### 当前公共绘制面

| 类别 | 成员 |
|---|---|
| 画刷 | `Outline`、`FillLight`、`FillWhite`、`Accent`、`PumpCoreFill`、`PlungerFill` |
| 画笔 | `OutlinePen`(1.6)、`OutlinePen14`(1.4)、`OutlinePen12`(1.2)、`AccentPen`(1.2)、`AccentPen14`(1.4)、`AccentPen2`(2.0)、`HighlightPen`(2.0 橙虚线) |
| 绘制 | `Render(dc, kind)`、`Render(dc, node)`、`RenderPort`、`RenderPortHighlight`、`DrawPath`、`DrawRoundRect` |
| 工具 | `BuildGlyph`、`MakePen`、`Frozen` |

### 说明

- 仅开放 API，无行为变更；内置部件外观不受影响。
- 路径/圆角矩形的内部几何缓存（`CachedPath` / `CachedRoundRect`）仍为私有：外部经 `DrawPath` / `DrawRoundRect` 即可享用缓存。

## 1.2.3（2026-10-22）

修复宿主自定义部件无法使用绘制工具的问题：`NodeRenderer` 的画笔与绘制辅助此前为 `internal`，跨程序集不可见。

### 变更

- **开放画笔（原 `internal` → `public`）**：`OutlinePen`（1.6）、`OutlinePen14`（1.4）、`OutlinePen12`（1.2）、`AccentPen`（1.2）、`AccentPen14`（1.4）、`AccentPen2`（2.0），均已冻结可跨实例共享。
- **开放绘制辅助**：
  - `DrawPath(DrawingContext dc, string data, Brush? fill, Pen pen)`：SVG 风格路径绘制，几何按 data 缓存冻结。
  - `DrawRoundRect(DrawingContext dc, double x, double y, double w, double h, double r, Brush? fill, Pen pen)`。
  - `BuildGlyph(string text, double size, Point origin)`：短文本转冻结矢量轮廓（高 DPI 不糊）。
  - `MakePen(Brush brush, double thickness)`：按自己的画刷生成圆头圆角的冻结画笔。
- 同步更新 `INodeDefinition` 的 XML 注释：原注明"Pen 需自行创建并冻结"，现改为列出可复用的公开画笔与辅助方法。

### 影响

- 此前宿主自定义部件只能用到 4 个公开**画刷**（`Outline` / `FillLight` / `FillWhite` / `Accent`），`NodeRenderer.DrawPath`、`DrawRoundRect`、`OutlinePen`、`AccentPen` 等均报"找不到"，1.2.2 及更早版本的宿主需升级到此版本。
- 仅新增/开放 API，无行为变更；内置部件外观不受影响。

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
  - 宿主扩展：`NodeKind` 仅含内置类型，业务部件类型由宿主以常量扩展（按数值落盘，升级库不影响已存档数值）：
    ```csharp
    public static class HostNodeKinds
    {
        public const NodeKind ThreeWayValve = (NodeKind)1000;   // 三通阀
    }
    ```
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

宿主自定义部件：实现 `INodeDefinition` → 启动时 `NodeDefinitionRegistry.Register`。以三通阀为例：

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
        dc.DrawEllipse(NodeRenderer.FillWhite, NodeRenderer.OutlinePen, new Point(28, 26), 16, 16);
        NodeRenderer.DrawPath(dc, "M14,26 L28,26 L42,26", null, NodeRenderer.AccentPen);
        NodeRenderer.DrawPath(dc, "M28,26 L28,40", null, NodeRenderer.AccentPen);
    }
}

// 启动时（注册/注销可在任意线程调用）
NodeDefinitionRegistry.Register(new ThreeWayValveDefinition());
```

四向端口可直接用内置预设 `NodePorts.Standard`（T/B/L/R）。注册表为内置只读层 + 宿主覆盖层：注册后即入右键菜单与画布；`Unregister` 后内置类型回退内置定义，宿主扩展类型再解析会抛 `InvalidOperationException`。

### 已知限制

- 暂无撤销/重做、多选框选、画布缩放（后续版本按需加入）。
- 载入失败时 `LoadAsync` 返回 `null`，不区分"文件不存在"与"内容损坏"；错误详情暂未对外暴露。
- 超大布局（500+ 部件）性能未做专项压测。

### 升级与兼容性说明

- 存档含 `Version` 字段（当前 1）。未来 schema 变更将依据此字段提供迁移；本版本为首次发布，无历史格式负担。
- `NodeKind` 枚举仅包含核心库内置类型（`Led`、`Switch`），扩展值由宿主常量自行定义——升级库不会影响宿主已落盘的业务部件数值。
