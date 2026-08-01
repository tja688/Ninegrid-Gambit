---
status: accepted
---

# 机关卡 `CardKind.Trap` 与双桶交战规则

## 决策

1. **一等公民 Kind**：新增 `CardKind.Trap`（枚举末尾追加，不插中间），同步 `CardPresentationKind.Trap = 7`、`EffectContainerType.Trap`、编辑器用 `ContentVisualKind.Trap`。内容 id 前缀 `trap.*`；卡组 `deck.trap`；效果模板 `tpl.trap.*`。
2. **双桶 helper（集中入口）**：`CardCombatRules`
   - **可交战 / 可受伤**：`IsBoardCombatTarget` = `Monster | Trap`（普攻门禁、`TargetResolver.MonstersOnBoard` / AllMonsters、场地战斗点击、拒拾取）。
   - **真怪物**：`IsTrueMonster` = 仅 `Monster`（清关、击杀赏金、遭遇「怪物」语义、`targetKind=Monster` 精确匹配）。
3. **清关不含 Trap**：`IDeckSystem.IsNodeCleared` 只看真怪物是否仍在抽牌堆 / 敌池 / 场上；残留 Trap 不挡关。
4. **无击杀赏金**：`EconomySystem` 移除赏金与 `KillAction` 显式 `GoldReward` follow-up 均跳过 Trap。
5. **静默 `CounterAttackBanned`**：`ContentSystem.ActivateCardEffects` 成功路径末尾，对 `CardKind.Trap` 向 `IStatSystem.RuleModifiers` 加永久 `CounterAttackBanned`（`TargetUidCondition`，Source `intrinsic.trap:{defId}`）；不挂远程武器技能、不进检视技能列表。
6. **卡面五套**：底盘 + 玩家 / 怪物 / 道具 / 遗物 / **机关**（`CardChassisPaths.TrapFacePrefab`）；编辑器效果池增加「机关技能」桶。
7. **前缀豁免退役**：死亡之主 / 刺客领袖等模板去掉 `excludeTargetDefPrefix=trap.`，依赖 `targetKind=Monster` 自然排除 Trap；Spawn 复活石 `kind: Trap`。

## 为什么

机关曾以 `kind: Monster` + `trap.*` 前缀伪装，导致清关/赏金/全盘伤/选壳/编辑器桶四处特例。产品上机关可交战但不是真怪；集中双桶避免散落 `if Trap`，并与牌面朝向背面惰性（[ADR-0016](0016-card-face-orientation.md)）正交：Trap 正面可进 AllMonsters，背面仍按 0016 过滤。

## 考虑过的替代

- **继续前缀伪装**：否决——编辑器/选壳/清关语义分叉，维护成本高。
- **Trap 不可交战、仅互动触发**：否决——与策划「机关可打」及爆弹等全盘伤诉求冲突。
- **清关把 Trap 当敌**：否决——残留复活石等会卡关。
- **显式挂 `skill.ranged_weapon`**：否决——检视会当技能；静默 Rule 即可。

## 后果

- 模板 `targetKind=Monster` 保持精确匹配；新机关技能走 `tpl.trap.*` + `EffectContainerType.Trap`。
- Help 重叠卡（滚石等）本 ADR **不**强制删除；迁徙另票。
- QuickTest `trapContentIds` 注入属批次2（`AddEnemyCard` 入敌池，与 skillIds 并存）；开局随机机关入组仍属后续票。
- 卡面 Kind→模版表与 [ADR-0002](0002-card-chassis-and-face-templates.md) 对齐为五套。

## 相关

- [ADR-0002](0002-card-chassis-and-face-templates.md) — 底盘 + 卡面模板（现含机关）
- [ADR-0016](0016-card-face-orientation.md) — 背面惰性与 AllMonsters FaceUp 过滤
