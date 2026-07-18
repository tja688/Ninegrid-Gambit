# Living Text 操作指南：UICamera 换域 + 背板绑定

> 日期：2026-07-18  
> 场景：`Assets/Scenes/UITestSence.unity`  
> 参考落地：[Selective Look 方案笔记](./SelectiveLook-扫描线与PixelSnap方案-2026-07-18.md) · 会话 [Selective Look PixelSnap](4080d7b8-4527-4fe0-b7f2-8ca3b975b630)  
> 绑定跳变修复：`LivingTextWorldBinder` 已改为「父节点局部投影 + Play 时捕获编辑器偏移」

本文面向 **AI / 人工批量处理**：把各「大盘构型」下的文字，做成与主菜单相同的效果——

1. **UICamera 渲染**（只吃扫描线，不吃 PixelSnap）  
2. **绑定对应背板**，运行时跟随背板运动，且 **编辑器摆位 = Play 摆位**（无需手调 `worldOffset`）

---

## 0. 目标验收标准

对某个大盘构型，完成后必须同时满足：

| # | 标准 |
|---|------|
| A | 该构型 `Text Overlay UI` 为 `Screen Space Camera`，`worldCamera = UICamera`，整棵 Canvas 在 `NoPixelSnap` 层 |
| B | Main Camera → `TableNineLookRig.livingTextCanvases` 已包含该 Canvas（可与其它已处理 Canvas 并存） |
| C | 每个需要跟随的文字在 `LivingTextWorldBinder` 中有一条 **enabled** 绑定，`worldTarget` 指向正确背板 |
| D | Play 后文字位置相对编辑器 **无跳变**；移动背板时文字同步位移 |
| E | 截图可见文字清晰（未被 PixelSnap 糊掉），扫描线观感与世界侧一致 |

---

## 1. 技术背景（读懂再动手）

```
Main Camera (Base)                 UICamera (Overlay)
  cull: ~NoPixelSnap                 cull: NoPixelSnap only
  Selective Look:                    Canvas = Screen Space Camera → UICamera
    PixelSnap（世界）                TMP 扫描线材质参数由 Rig 同步
    Scanlines（世界）
         └──────── camera stack ────────┘
```

- **世界背板**：`SpriteRenderer`，子物体名多为数字（`1`…`12`），保持 Default（或非 `NoPixelSnap`）层 → 吃 PixelSnap + 扫描线。  
- **文字**：原 `ScreenSpaceOverlay` 已脱域；必须换到 UICamera，才能和背板同一「舞台」做跟随。  
- **跟随**：`LivingTextWorldBinder` 在 Play 首帧捕获「文字相对背板中心」的父节点局部偏移，之后每帧投影跟随。**不要**再靠调 `worldOffset` 对齐；`worldOffset` 仅作运行时微调，默认 `0`。

关键资产：

| 资产 | 路径 / 位置 |
|------|-------------|
| LookRig | Main Camera 上 `TableNineLookRig` |
| Binder | 各构型 `Text Overlay UI` 上 `LivingTextWorldBinder` |
| Feature | `Renderer2D` → `TableNine Selective Look`（旧 Pixel Snap Post 已禁用） |
| Layer | `NoPixelSnap`（slot 8） |
| 脚本 | `Assets/Scripts/UI/VisualLook/` |

---

## 2. 场景清单（UITestSence）

| 大盘构型 | 典型状态 | Overlay 默认 | 文字数（约） |
|----------|----------|--------------|--------------|
| `大盘构型0-主菜单` | 常激活；**参考完成态** | 激活 | 5（已绑 4：标题→4 / 开始→12 / 设置→11 / 结束→2；版本信息可同绑 4 或暂不跟） |
| `大盘构型1-人物选择` | 常失活 | **常失活** | ~4 |
| `大盘构型2-核心战斗面板` | 常失活 | **常失活** | ~8 |
| `大盘构型2-1-选择奖励` | 常失活 | **常失活** | ~6 |
| `大盘构型2-2-打开卡组视图` | 常失活 | **常失活** | ~5 |
| `大盘构型3-房间基础面板` | 常失活 | **常失活** | ~7 |
| `大盘构型4-路线展示` | 常失活 | **常失活** | ~8 |

> 每个构型根下通常有约 12 个数字命名背板 + 一个 `Text Overlay UI`。

---

## 3. AI 强制规矩（防绑错 / 防串台）

### 3.1 一次只激活一个大盘构型

处理构型 `X` 时：

1. **关掉**所有其它 `大盘构型*`（`SetActive(false)`）。  
2. **只打开**目标构型根节点。  
3. **打开**该构型下的 `Text Overlay UI`（很多默认 `activeSelf=false`，不打开则看不见、也绑不准）。  
4. 处理完再关回失活（或按产品需要保留当前页），再开下一个。

原因：多构型叠在一起时，屏幕投影会互相污染，**最近邻 / 截图都会绑错背板**。

### 3.2 禁止手改 `.unity`

场景改动一律走 **Unity MCP**（`execute_code` / `manage_gameobject` / `manage_components` 等），改完 `manage_scene(action=save)`。

### 3.3 禁止只凭物体名猜绑定

背板叫 `2`/`4`/`12`，与文案无关。必须以 **屏幕空间几何 + 截图目视** 确认。主菜单反例：标题「九宫牌局」绑的是大板 `4`，不是名字相近的物体。

---

## 4. 单构型标准作业流程（SOP）

对目标构型名 `LAYOUT`（例：`大盘构型3-房间基础面板`）逐步执行。

### Step A — 独占激活

```text
1. FindObjects：所有名以「大盘构型」开头的根
2. 全部 SetActive(false)
3. LAYOUT.SetActive(true)
4. LAYOUT/Text Overlay UI.SetActive(true)
5. 断言：仅 LAYOUT.activeInHierarchy==true；其 Overlay.activeInHierarchy==true
```

### Step B — 挂上 Look 管线（换域）

1. 取 `Canvas` = `LAYOUT/Text Overlay UI`。  
2. 确保 Main Camera 有 `TableNineLookRig`、子物体 `UICamera` 已在 stack。  
3. 把该 `Canvas` **追加**进 `TableNineLookRig.livingTextCanvases`（勿覆盖已处理好的其它 Canvas 引用；失活构型的引用可保留）。  
4. 调用 `rig.ApplyRig()`（或 ContextMenu `Apply Look Rig Now`）。  
5. 断言：
   - `canvas.renderMode == ScreenSpaceCamera`
   - `canvas.worldCamera.name == "UICamera"`
   - Canvas 子树 layer 均为 `NoPixelSnap`
6. 若无 `LivingTextWorldBinder`：AddComponent；`canvas` 字段可留空（Awake GetComponent）或拖自己。

### Step C — 自动配对文字 ↔ 背板（可靠算法）

**候选背板**：`LAYOUT` 直接子物体上带 `SpriteRenderer` 的 Transform（一般即 `1`…`12`）。  
**候选文字**：`Text Overlay UI` 下每个带 `TMP_Text` / `RectTransform` 的子节点（含失活子节点时用 `includeInactive`）。

对每个文字：

1. 取文字矩形中心世界坐标 → `WorldToScreenPoint(MainCamera 或 UICamera)` 得 `textScreen`。  
2. 对每个背板：用 `SpriteRenderer.bounds` 的 min/max 角点投到屏幕，得到屏幕 AABB。  
3. **优先**选「`textScreen` 落在 AABB 内」的背板；若多个命中，选 **屏幕面积最小** 的（避免大背景板吞掉按钮字）。  
4. 若无一命中：选屏幕中心距离 `textScreen` 最近的背板，但必须标记为 `uncertain`，进入 Step D 强制截图确认。  
5. 记录配对表：`{ textPath, textContent, panelName, method: contain|nearest, confidence }`。

主菜单校验样例（算法应复现）：

| 文字 | 内容 | 背板 |
|------|------|------|
| 标题 | 九宫牌局 | `4` |
| 开始 | 开始游戏 | `12` |
| 设置 | 设置 | `11` |
| 结束游戏 | 结束游戏 | `2` |

> 「版本信息」也落在 `4` 内；是否跟随由产品决定——跟随则另加 binding，不跟则不要写入 binder。

### Step D — 截图目视（防绑错）

1. `manage_camera(action=screenshot, include_image=true)` 或 GameView 截屏。  
2. AI **必须**对照截图核对：每个字是否落在所绑背板视觉区域内。  
3. 有疑问时：临时把候选背板 `SpriteRenderer.color` 闪成高对比色再截一张，或 Solo 只显示该背板。  
4. 修正错误配对后再写序列化。

### Step E — 写入 LivingTextWorldBinder

对每个确认配对：

```text
bindings[i].text        = 文字 RectTransform
bindings[i].worldTarget = 背板 Transform
bindings[i].worldOffset = (0,0,0)
bindings[i].enabled     = true
```

- `captureAuthoredOffsetOnPlay` 保持 **true**（默认）。  
- **不要**为了「对齐」去改文字 `anchoredPosition` / `worldOffset`；编辑器里已摆好的相对位置会在 Play 时自动捕获。  
- 若编辑器中途改过摆位：组件右键 `Recapture Authored Offsets Now`（Play 中也可）。

### Step F — Play 验收

1. 进入 Play。  
2. 读各 binding：文字 `localPosition` 相对进 Play 前应 **跳变 ≈ 0**。  
3. 短暂移动某一 `worldTarget.position`（如 `+= (0, 0.5, 0)`），确认对应文字同步移动；再还原。  
4. 截图确认观感（字清晰、有扫描线）。  
5. 退出 Play；`manage_scene(save)`。

### Step G — 收尾 / 下一构型

1. 按需将本构型重新失活。  
2. 对下一 `LAYOUT` 从 Step A 重来（再次 **独占激活**）。

---

## 5. 推荐 MCP 检查脚本要点

处理时用 `execute_code` 做断言，避免「看起来做了其实没挂上」：

```text
断言清单：
- 唯一 active 的大盘构型名 == LAYOUT
- Overlay.activeInHierarchy
- canvas.worldCamera == UICamera
- layer(NoPixelSnap) 覆盖 Canvas 子树
- bindings.Length == 预期跟随文字数
- 每条 enabled 的 text/worldTarget 非空
- 配对表无 uncertain（或 uncertain 已人工/截图确认）
```

投影配对伪代码（与主菜单验证过的逻辑一致）：

```csharp
// textScreen = 文字 Rect 中心投屏
// 对每个 SpriteRenderer 背板：
//   若 textScreen 在 bounds 的屏幕 AABB 内 → 记为 contain 候选
// 若 contain 候选非空 → 取屏幕面积最小者
// 否则 → nearest，标记 uncertain
```

---

## 6. 人工 / 单条微调

- 编辑器摆好字 → 填 `text` + `worldTarget` → Play 即跟，无需调 offset。  
- 仅当「捕获后仍想再偏一点」才改 `worldOffset`。  
- Look 强度：Main Camera → `TableNineLookRig`（resolution / snap / scanline）。

---

## 7. 常见失败模式

| 现象 | 原因 | 处理 |
|------|------|------|
| Play 后字飞走 | 旧 binder 写错 `anchoredPosition`；或未开捕获偏移 | 确认已是新版 `LivingTextWorldBinder`；`captureAuthoredOffsetOnPlay=true` |
| 字绑到错误背板 | 多构型同时激活；或只用最近邻 | 独占激活；优先 AABB contain + 最小面积；截图复核 |
| 看不见字 | Overlay 仍失活；或未进 UICamera / 层不对 | 激活 Overlay；`ApplyRig`；检查 `NoPixelSnap` + stack |
| 字被 snap 糊掉 | 仍在 MainCamera / Default 层 | 必须 UICamera + `NoPixelSnap` |
| 跟随不同步 | binder 未 enabled；或绑错 target | 检查 bindings；Play 里挪背板验证 |
| LookRig 覆盖丢引用 | `livingTextCanvases` 被写成只含当前一个 | **追加**而非整表替换 |

---

## 8. 批量任务给 AI 的一句话指令模板

```text
按 Assets/Notes/LivingText-UICamera换域与背板绑定操作指南-2026-07-18.md：
依次处理 UITestSence 中尚未完成的大盘构型。
每次只激活一个构型及其 Text Overlay UI，ApplyRig 换到 UICamera，
用屏幕 AABB 包含（多命中取最小面积）配对文字与背板，截图确认后写入
LivingTextWorldBinder；Play 验证无跳变且跟随；保存场景后再处理下一个。
禁止手改 .unity；禁止多构型同时激活；禁止只凭名字绑定。
```

---

## 9. 与方案笔记的关系

- **原理 / 渲染管线 / 资产表**：见 `SelectiveLook-扫描线与PixelSnap方案-2026-07-18.md`。  
- **本文**：场景操作、绑定可靠性、给 AI 的 SOP 与验收。  
- 绑定实现细节以当前 `LivingTextWorldBinder.cs` 为准（捕获编辑器偏移，避免跳变）。
