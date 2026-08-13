---
status: accepted
---

# 一次性遗物效果：消费标记持久化 + 战斗内授卡洗入抽牌堆

## 决策

**遗物容器的一次性效果（`OnActivate` + `DeactivateSelfEffect`，如黄金鱼竿 `relic.golden_coffer`「获得时将 2 张金色宝箱卡加入卡组」）确立三条不变量：**

1. **`DeactivateSelfEffect` 对遗物容器默认只停用效果，不撤持有。** `DeactivateEffectAction` 仅当原子显式配置 `"removeRelic":true`（凤凰羽毛 `tpl.relic.phoenix_feather.fatal`「永久移除本遗物」）才 `PlayerModel.RemoveRelic`；缺省路径把 `definition.Id` 写入 `PlayerModel` 的**一次性消费标记**，遗物留在装备栏。
2. **消费标记随 run 持久，重挂统一在 `ContentSystem.ActivateRelic` 收口。** `ActivateEffectIds`（Relic 容器）跳过已消费效果 id——授予、存档恢复（`RunSaveGame.RestoreAfterCreate`）、跨层重装（`BattleSessionExecutor.RestoreRunInventory`）、StartNode 自愈（`ReactivateMissingRelicEffectsAction`）四条重挂路径全部经此，不再重复发放。标记已入 `RunSaveSnapshot.consumedRelicEffectIds` 与跨层 `RunInventorySnapshot`，且**先于遗物重装写回**。遗物离开装备栏（`DiscardRelicAction` / `removeRelic` 消耗）时按目录 `EffectIds` 清标记（`RelicConsumedEffectMarks`），重获后可再次触发。
3. **战斗互动期（InteractionLoop）经 `Spawn` 向 `PlayerCardPool` 授予帮助卡改走 `ShuffleIntoDrawPileAction`（洗入抽牌堆）。** `PlayerCardPool` 是仅供开局发牌（`OpeningDealAction`）消费的暂存池，互动期投入会滞留死区、下节点 `ClearBattleZones` 直接吞卡。局外授予维持既有 `HelpCardGrantRouting` 语义（直写道具卡格，满格静默丢弃）；`OnNodeStart` 的 `nodeStartDrawPileGrant` 路径不变。

## 为什么

**事故（2026-08-13，corelog-084545）**：战斗内宝箱三选一拿黄金鱼竿，`RelicGranted` + `RelicMountAudit mounted=1` 全部正常，但两张宝箱卡从未出现、遗物栏也不显示该遗物。两个断点：

- `Spawn` 把宝箱卡投进 `PlayerCardPool` 暂存池——互动期无人消费，卡静默滞留至下节点被清；
- 旧 `DeactivateEffectAction` 对遗物容器**无条件** `RemoveRelic`（为凤凰羽毛「自毁式遗物」而写）——黄金鱼竿模板末尾的 `DeactivateSelfEffect` 本意只是「停用本效果防重复发放」，却把遗物本体一并删掉。事实上模板作者被迫用 DeactivateSelfEffect 防重发，正说明缺一个「发过了」的持久事实。

**为什么不是只删 RemoveRelic**：删掉后遗物留在栏里而效果实例已卸载，StartNode 自愈（装配级核对）每关重挂 → OnActivate 每关重发宝箱卡；存档恢复与跨层重装同样重发。一次性语义必须落成可持久、可审查的模型事实，而非依赖「实例还挂着」这类易失状态。

**为什么标记键用目录效果 id**：装配解析（`EffectAssemblyResolver.Resolve`）把 `mountId` 写入 body `id`，`instance.Definition.Id` ≡ 目录效果 id ≡ 遗物 `EffectIds` 条目——发放方（DeactivateEffectAction）、跳挂方（ActivateEffectIds）、清除方（按目录 EffectIds）三方天然同键，无需新增映射。

## 考虑过的替代

- **模板去掉 DeactivateSelfEffect，靠触发器只发一次**：否决——OnActivate 每次挂载都发，重挂场景（读档/跨层/自愈）全部重复发放，仍需持久标记。
- **Avatar 计数器当标记**：否决——跨层重引导会重建 Avatar，`RunInventorySnapshot` 不含 Avatar 计数器，标记会丢。
- **发放逻辑移入 `GrantRelicAction` 硬编码**：否决——绕开效果 DSL，内容侧失去组合能力；且 GrantRelic 并非唯一授予口。
- **战斗内投放改内容层（模板换 `ShuffleInto` 原子）**：否决——黄金鱼竿也可在局外获得（商店/房间），`ShuffleInto` 在局外会把卡洗进即将被清的牌堆；落点按相位路由是 `SpawnCardAction` 的既有职责（`HelpCardGrantRouting`），在动作层补齐 InteractionLoop 缺口对所有内容生效。

## 后果

- **行为变化**：黄金鱼竿战斗内拾取 → 2 张宝箱卡洗入抽牌堆（有「入牌堆」演出），遗物留在装备栏；局外拾取 → 宝箱卡进道具卡格（满格静默丢弃，沿用帮助卡授予约定）。凤凰羽毛行为不变（模板显式 `removeRelic:true`）。
- **新持久态检查单项**：一次性消费标记属跨战斗持久态，已按 ADR-0041 检查单同步 `RunSaveSnapshot`（新增字段 `consumedRelicEffectIds`，JsonUtility 缺字段回退空数组，旧存档兼容）与 `RunInventorySnapshot`。
- **内容作者契约**：写「遗物获得时一次性发放」类模板，结尾挂 `DeactivateSelfEffect`（不带 removeRelic）即可获得完整的防重发语义；写「自毁式遗物」必须显式 `"removeRelic":true`。
- **回归**：`RelicGoldenCofferGrantTests`——战斗内发放落抽牌堆且遗物留栏、重挂/次节点自愈不重发、局外发放进道具卡格、丢弃后重获再发、凤凰羽毛带新字段正常挂载。

## 相关

- [ADR-0009](0009-parameterized-effect-templates.md) — 参数化效果模板（removeRelic 为原子级配置）
- [ADR-0025](0025-item-slots-run-persistent-hold.md) — 道具卡格跨局持有（局外授予落点）
- [ADR-0041](0041-run-save-battle-start-checkpoint.md) — 存档检查点（新持久态必须同步快照）
- [ADR-0047](0047-pipeline-fault-containment.md) — 事故存案模式参照（corelog 定位断点）
- 事故存案：2026-08-13 corelog-084545 黄金鱼竿拾取后宝箱卡消失 + 遗物栏不显示
