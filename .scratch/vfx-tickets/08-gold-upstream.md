Part of #192.

## 目标

在迁移播放器前修复金币表现的上游空间因果断线。

## 范围

- `ModifyGoldAction` 支持可选来源卡，并在 `GoldModified` 写入 CardUid。
- `KillAction` 显式赏金与 `EconomySystem` 默认移除赏金传递正确来源 uid。
- `GoldModified → UpdateGold` 从 `Settled` 改为尸体 Vacate 前的 `Impact`。
- 加固 `GoldGainPresentationBinder.EnsureInstalled`：已有实例也确保订阅，架构暂不可用时不静默永久丢失。
- 保持 BattleBeatScheduler 唯一出口，不恢复 EventLog 生产旁路，不把金币演出放进 ack。
- 对齐 ADR-0005、ADR-0007 与 code-map 的金币锚点事实。

## 验收

- 击杀赏金能在 Impact 解析死亡卡位置；非战斗 EventLog slice 行为不回归。
- FlowTrace `GoldGained/GoldSpent` 与音效 Cue 仍由权威 Binder 发射。
- 未用帮助卡结算继续保留卸视图前采 origin 的既有时序。

## 通用约束

- Part of #192。
- 遵守 ADR-0040、ADR-0001、ADR-0007、ADR-0018 与 `CONTEXT.md` 的 VFX 领域词汇。
- VFX 不成为规则权威，不占主线 ack；失败可见但不得阻断游戏流程。
- 不接管普通旧 FX 路径；仅金币是本 Spec 的明确迁移例外。
- 完成后同步实际受影响的 `docs/code-map/`，并满足 Unity recompile 后 Console 无本票新增 Error/Exception/Assert。
