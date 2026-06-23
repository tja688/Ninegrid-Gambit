# 表现层 · Status / Effect 适配器蓝图笔记

> 权威蓝图：`九宫牌局表现层.canvas` → 泳道 C · `adp_status` / `adp_effect` 及其下游表演节点  
> 对照表：`Assets/Notes/表现层蓝图落地对照表.md`  
> 内核契约：`Assets/Notes/Core表现层Command与Event消费清单-2026-06-20.md`

---

## 1. 总览

| 适配器 | 蓝图节点 | 代码路径 | 状态 |
|:--|:--|:--|:--|
| `TableNineStatusAdaptor` | `adp_status` | `Adaptors/TableNineStatusAdaptor.cs` | ★已落地 |
| `TableNineEffectAdaptor` | `adp_effect` | `Adaptors/TableNineEffectAdaptor.cs` | 部分（最小壳） |

二者均由 `PresentationBatchPlayer` 路由，挂于 MainScene **`NineGrid Battle`**（与 `TableNineViewRegistry` / 其他域适配器同 GO）。

**路由顺序（批内每条指令）**：

1. `TableNineStatusAdaptor`（若 `CanHandle`）— **可与 Board 等同指令并行**（如 `ShowDamage` 先飘字、再战斗动画）
2. `TableNineCardDeckAdaptor`
3. `TableNineItemAdaptor`
4. `TableNineEffectAdaptor`
5. `TableNineBoardAdaptor`

批末：`statusAdaptor.AlignFromSnapshot(snapshot)` 对齐 HUD 权威终态。

---

## 2. TableNineStatusAdaptor · 玩家状态域

### 2.1 蓝图职责

翻译内核 **伤害 / 属性 / 经济 / 互动** 类指令 → 飘字 + HUD 面板；**不含**战斗位移/击杀（归 `TableNineBoardAdaptor`）。

### 2.2 认领指令

| InstructionKind | CoreEventType | 常用字段 | 表演 |
|:--|:--|:--|:--|
| `ShowDamage` | `DamageDealt` | `TargetUid`, `Amount`, `Delta` | `DamagePopupPerformance`（Damage）· **非阻塞** |
| `UpdateHp` | `HpChanged` / `Healed` | `CardUid`, `Delta`, `RemainingHp` | 面板同步；`Healed` 且 `Delta>0` 额外 Heal 飘字 |
| `UpdateArmor` | `ArmorChanged` | `CardUid`, `RemainingArmor` | 面板同步（化身） |
| `UpdateGold` | `GoldModified` | `Amount`, `Delta` | 面板 + Gold 飘字（`Delta≠0`） |
| `UpdateInteractionCount` | `InteractionChanged` | `Amount` | 面板同步 |

**未认领（蓝图规划有、代码暂无）**：

- `ModifyBaseStat`（`BaseStatModified`）— 对照表待补

### 2.3 管理的表演黑盒

| 表演 | 路径 | 状态 | 说明 |
|:--|:--|:--|:--|
| `DamagePopupPerformance` | `Performance/DamagePopupPerformance.cs` | ★已落地 | DNP_2D `Spawn` + `SetFollowedTarget` |
| `StatusPanelUpdatePerformance` | `Performance/StatusPanelUpdatePerformance.cs` | 占位 | 0s 即时写 TMP；动效槽待补 |
| `TableNineStatusPanelView` | `Visuals/TableNineStatusPanelView.cs` | ★已落地 | HUD 视图绑定（非表演，被 Panel 表演驱动） |

#### DamagePopupPerformance

- **模板预制体**（自 DNP_2D 提取）：
  - `Assets/Arts/Prefabs/Presentation/DamageNumbers/DNP_RedGlow.prefab` — 伤害
  - `DNP_PixelHeal.prefab` — 治疗
  - `DNP_Gold.prefab` — 金币变化
- **演员解析**：`ViewRegistry.TryGetActor(targetUid)` → 失败且为化身则回退 `AvatarSlot` 锚点
- **预览**：组件 ContextMenu `Preview/Damage|Heal|Gold Popup`

#### StatusPanelUpdatePerformance

- 当前：`TableNineStatusPanelView.ApplyEvent` / `ApplySnapshot` 直写数字
- 后期：在此黑盒内替换 DOTween 计数/弹跳/颜色脉冲，适配器接口不变

#### TableNineStatusPanelView · 场景接线

- MainScene：`TableNine Overlay UI / TableNineStatusPanel`
- TMP：`HpText` / `ArmorText` / `GoldText` / `InteractionText`（左上 HUD）

### 2.4 与 Board 域的协作

`ShowDamage` **双消费**：

| 域 | 行为 |
|:--|:--|
| Status | 目标身上 DNP 飘字（瞬时，不 yield 阻塞） |
| Board | 攻击/反击/震屏等战斗表演（阻塞至 `TotalDuration`） |

批内 `HpChanged` 常与 `DamageDealt` 同 Action 出现：飘字走 `ShowDamage`，HUD 数字走 `UpdateHp`，互不重复飘字。

### 2.5 缺口

- [ ] `ModifyBaseStat` 指令认领
- [ ] `StatusPanelUpdatePerformance` 真实动效
- [ ] 护甲变化独立飘字（可选）
- [ ] HUD 美术排版（当前极简 TMP 四行）

---

## 3. TableNineEffectAdaptor · 效果域

### 3.1 蓝图职责

翻译内核 **效果触发 / 修正应用** 类指令 → 轻量反馈表演。蓝图规划还含 `DeactivateEffect` / `GrantSkill` / `GrantRelic` / `LoadContent`，**当前最小 MVP 仅接前两项**。

### 3.2 认领指令（已实现）

| InstructionKind | CoreEventType | 常用字段 | 表演 |
|:--|:--|:--|:--|
| `TriggerEffect` | `EffectTriggered` | `CardUid`(owner), `Message`(effectId), `SourceDefId`, `Cause` | `EffectTriggerPerformance` |
| `ApplyModifier` | `EffectModifierApplied` | `CardUid`, `Amount`(stat/rule), `Delta`, `Message` | `ModifierApplyPerformance` |

### 3.3 蓝图规划、尚未认领

| InstructionKind | CoreEventType | 备注 |
|:--|:--|:--|
| `DeactivateEffect` | `EffectDeactivated` | 效果熄灭/卸载 |
| `GrantSkill` | `SkillGranted` | 技能获得 |
| `GrantRelic` | `RelicGranted` | 遗物获得 |
| `LoadContent` | `ContentLoaded` | 内容加载提示 |

### 3.4 管理的表演黑盒

| 表演 | 路径 | 状态 | 说明 |
|:--|:--|:--|:--|
| `EffectTriggerPerformance` | `Performance/EffectTriggerPerformance.cs` | 占位 0s | `Play(owner, effectId, …)` 立即 `onComplete` |
| `ModifierApplyPerformance` | `Performance/ModifierApplyPerformance.cs` | 占位 0s | `Play(cardUid, statOrRule, delta, …)` 立即完成 |

**注意**：战斗主路径的 `CardAttack` / `CardKill` / 闪白等归 **Board 域 + `TableNineBoardAdaptor`**，不是 Effect 适配器编排对象；蓝图旧注释「待 TableNineEffectAdaptor」指未来 **效果触发的额外叠层**，非当前 MVP 范围。

### 3.5 缺口

- [ ] `EffectTriggerPerformance` 真实视觉（图标弹出/粒子/音效）
- [ ] `ModifierApplyPerformance` 真实视觉（数值条/图标抖动）
- [ ] 扩展认领 `DeactivateEffect` / `Grant*` / `LoadContent`
- [ ] 按 `effectId` / `Message` 路由不同表演变体（Profile 或表驱动）

---

## 4. 蓝图注释节点索引（黄色 ★已落地）

| 注释节点 ID | 指向蓝图节点 | 摘要 |
|:--|:--|:--|
| `c3d4e5f6a7b80911` | `adp_status` | Status 适配器落地 |
| `d4e5f6a7b8091223` | `p_dmg` | DamagePopup 落地 |
| `e5f6a7b809123344` | `p_status` | StatusPanelUpdate 占位落地 |
| `f6a7b80912334455` | `adp_effect` | Effect 适配器最小落地 |
| `a7b8091233445566` | `p_efftrig` | EffectTrigger 占位 |
| `b809123344556677` | `p_modapply` | ModifierApply 占位 |

---

## 5. 变更记录

| 日期 | 摘要 |
|:--|:--|
| 2026-06-23 | 初版：Status 全链路 + Effect 最小壳；蓝图注释 + 本笔记 |
