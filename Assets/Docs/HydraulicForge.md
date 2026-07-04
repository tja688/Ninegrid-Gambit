# 液压锻造子场景（Hydraulic Forge）

战斗中的独立锻造台：管道出料、桌面落位、三台磁悬浮堆叠与拖拽布置。  
宿主物体：`MainScene` → `液压场景`。

战斗侧入口与面板联动见 `Battle.md`；材料选中描边复用 `SceneElementSelection.md`。

---

## 组件一览

| 组件 | 挂载 | 职责 |
|------|------|------|
| `HydraulicSceneController` | `液压场景` | 入场 / 常规退场 / 锤击完成退场状态机 |
| `HydraulicMaterialLane` | 同上 | 管道倾倒、10 桌面槽、材料池、管道遮罩 |
| `HydraulicMaterialBoard` | 同上 | 三锻造台拖拽、磁悬浮堆叠、桌面取回 |
| `HydraulicMaterialPiece` | 运行时 `Materials/material_*` | 单份材料状态、落位、漂浮微动 |

命名空间：`NineGrid.GameFlow`。

---

## 状态机（HydraulicSceneController）

```
Hidden ──Enter()──► Entering ──► Active
                      ▲              │
                      │         Exit() / PlayHydraulic()
                      │              ▼
                      └──── Exiting / Hydraulic ──► Hidden
```

| 状态 | 含义 |
|------|------|
| `Hidden` | 视觉收起；退场结束后宿主可失活 |
| `Entering` | BG → 显示屏 → 管道错峰入场 |
| `Active` | 可出料、拖拽布置 |
| `Exiting` | 常规退场：管道 → 显示屏 → BG |
| `Hydraulic` | 锤头下压 → 其它元素退场 → 抬起 → 整场景 Hidden |

### 对外 API / 事件

| API / 事件 | 用途 |
|------------|------|
| `Enter()` / `Exit()` / `PlayHydraulic()` | 入场、常规退场、锤击完成退场 |
| `TryDeliverMaterial()` | Active 时沿管道出一份材料 |
| `EnterStarted` | 宿主已激活，视觉尚未就位（战斗侧藏 Enemy Info） |
| `EnterCompleted` | 停留在 stay，可交互 |
| `ExitCompleted` / `HydraulicCompleted` | 退场完毕（战斗侧恢复面板） |

入场完成与任意退场都会 `ResetMaterials()`：清空锻造台堆叠 + 材料回池。

---

## 场景层级与标记点

`液压场景` 下关键子物体：

| 名称 | 作用 |
|------|------|
| `液压场景BG` / `液压场景BG in/out` | 背景 stay / 入出场点 |
| `显示屏` / `显示屏 in/out` | 显示屏 |
| `管道` / `管道 in/out` | 管道精灵；子物体 `管道遮盖图` |
| `锤头` / `锤头下压落点` | 液压完成演出 |
| `MaterialSpawn` | 材料生成点（管内右上） |
| `MaterialPipeExit` | 管道出口 / 桌面入口拐角 |
| `MaterialSlot_0` … `MaterialSlot_9` | 桌面 10 槽；**0=左端首，9=右端末**，中间由 Lane 按两端均匀插值 |
| `锻造台台面左` / `中` / `右` | 台面锚点 + `BoxCollider2D`（Trigger，放置检测区） |
| `Materials`（运行时） | 材料池父节点 `material_0` … `material_9` |

### 管道遮盖图

- 单张管道图无法遮挡管内材料，故用子物体 `管道遮盖图`（长条方块对齐管内区域）。
- **玩家不可见**：`SpriteRenderer` 关闭；挂 `SpriteMask`，sprite 取自原渲染器。
- 材料滑出阶段 `maskInteraction = VisibleOutsideMask`（在遮罩内被裁切，滑出后可见）；落桌后改为 `None`。

---

## 材料产出（HydraulicMaterialLane）

### 流程

1. 小键盘 **5**（经 `BattleController` → `TryDeliverMaterial()`）或外部调用。
2. 从池中取 `Home == Pool` 的 piece，预占一个空桌面槽。
3. `MaterialSpawn` →（管道段，InQuad）→ `MaterialPipeExit` →（桌面段，OutCubic）→ 对应 `MaterialSlot_*`。
4. 落位后静止，可拖拽；**不是**持续物理滑动。

### 容量与尺寸

| 项 | 值 |
|----|-----|
| 材料池 / 桌面槽 | **10** |
| 桌面 scale | `0.325`（原 0.25 × **1.3**，为拖上锻造台做准备） |
| 锻造台 scale | `0.21`（视角缩小） |
| 精灵 | Inspector `materialSprites`（10 种锭图循环） |
| 描边材质 | `Assets/Arts/VisualProfiles/TableNineSpriteSilhouette.mat` |

槽位世界坐标：`Lerp(MaterialSlot_0, MaterialSlot_9, i/9)`。改两端标记即可整体重排；运行时也会同步中间 `MaterialSlot_1`～`8` 位置。

### Piece 状态（HydraulicMaterialHome）

| 状态 | 含义 |
|------|------|
| `Pool` | 隐藏在池中 |
| `Sliding` | 管道/桌面入场动画中（不可拖） |
| `Table` | 桌面静止 |
| `Anvil` | 锻造台漂浮堆叠 |
| `Dragging` | 指针拖拽中 |

---

## 拖拽与锻造台（HydraulicMaterialBoard）

仅在液压 `Active` 时响应。左键按下命中可交互材料开始拖；松开结算。

### 放置规则

| 松手位置 | 行为 |
|----------|------|
| 某锻造台 Trigger 内，且该台 `< 10` | 按松手 **Y** 插入堆叠（可上可下），缓动挤开重排，缩放到 `anvilScale`，开启微浮动 |
| 锻造台已满（≥10） | 回拖拽前位置（原台或原桌面槽） |
| 未落在任何锻造台 | 尝试占桌面**空槽**，恢复 `tableScale`，关闭浮动 |
| 桌面无空槽 | **不能取回**，弹回原锻造台（或原位兜底） |

台间切换：从 A 台拖到 B 台等同「从 A 移除 → 插入 B」。

### 磁悬浮堆叠

- 每台独立列表，**上限 10**（与桌面槽数一致，整池可堆在一台）。
- `stack[0]` = 最底（靠近台面），索引增大向上。
- 布局：台面 `surface.y + anvilBaseLift` 起，在 Trigger 上界内按 `preferredStackSpacing` 均分；超高则压缩间距。
- 重排用 DOTween（`rearrangeDuration` / `OutCubic`），到位后 `Bobbing`：小幅 `sin` 上下浮动。
- 桌面材料 **不** 浮动。

### 选中描边

- 每份材料挂 `SelectableSceneElement`，`ConfigureOutline(silhouetteMat)`。
- 悬停由全局 `SceneElementPointerSelector` 驱动；拖拽中 `LateUpdate` 强制保持 outline，避免与指针选择抢状态。

---

## 调试热键

监听在始终激活的 `BattleController`（`DebugHotkeyInput`）。液压宿主失活时自身 `Update` 不跑。

| 键 | 行为 |
|----|------|
| 小键盘 **1** | 液压入场 / 常规退场 |
| 小键盘 **2** | 锤击完成退场 |
| 小键盘 **5** | 管道出一份材料（需 `Active`，池与桌面槽均有空位） |

---

## 调参入口（Inspector）

### HydraulicMaterialLane

| 字段 | 说明 |
|------|------|
| `slotStart` / `slotEnd` | 桌面首末槽（默认 Slot_0 / Slot_9） |
| `spawnPoint` / `pipeExitPoint` | 管道路径 |
| `pipeMask` | 管道遮盖图 |
| `tableScale` / `anvilScale` | 桌面 / 锻造台尺寸 |
| `pipeSlideDuration` / `platformSlideDuration` | 两段滑动时长 |
| `materialSprites` / `outlineMaterial` | 外观与描边 |

### HydraulicMaterialBoard

| 字段 | 说明 |
|------|------|
| `anvils[]` | 左/中/右：`surface` + `zone`（Collider2D） |
| `anvilBaseLift` / `anvilTopPadding` | 堆叠离台面高度 / 触顶留白 |
| `preferredStackSpacing` | 理想层间距（满员时自动压缩） |
| `rearrangeDuration` | 挤开动画时长 |

路径与台面范围以场景空物体为准，优先拖 `MaterialSpawn` / `MaterialPipeExit` / `MaterialSlot_0&9` / 三台面，而不是改代码常量。

---

## 与战斗的衔接

| 时机 | 战斗侧 |
|------|--------|
| 点玩家 / 调试入场 | `TryEnterForgeMode()` → `Enter()` |
| `EnterStarted` | `EnemyInfo.SuspendImmediate()` |
| `ExitCompleted` / `HydraulicCompleted` | `ResumeImmediate()`（战斗已 `Exiting` 时不 Resume） |
| 战斗 `ExitBattle` | 若液压 `Active` 则 `Exit()` |

当前锤击 `PlayHydraulic()` 只做演出退场，**未**读取锻造台材料列表做数值结算。

---

## 后续缺口

1. **锤击结算**：`PlayHydraulic` 时读取三台堆叠顺序/种类 → 战斗数值 / 卡牌效果  
2. **正式输入**：非小键盘的出料与开关锻造  
3. **音效 / 镜头**：出料、落台、堆叠挤开、锤击  
4. **材料数据**：piece 绑定逻辑 id / 品质，而非仅换皮精灵  
5. **桌面区域判定**：取回桌面目前靠「未命中锻造台 Trigger」；若需更严可加桌面专用 Trigger  
6. **满池提示**：桌面 10 满或池耗尽时的 UI 反馈  
