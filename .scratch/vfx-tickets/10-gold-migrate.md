Part of #192.

## 目标

将所有金币获得路径切到新 gold-flight Binding，并把金币数字职责收回 HUD。

## 范围

- `GoldGainPresentationBinder` 改发稳定 VFX Cue，保留声音提示与 FlowTrace。
- 未用帮助卡结算的直连调用改发 VFX 请求，保留卸视图前 origin 快照。
- 回收、商店、酒馆、道具、击杀等奖励路径覆盖同一请求模型。
- HUD 独占 goldText；扣金即时对齐，增金按首达→末达窗口使用 HUD 自有单调整数曲线推进并末端精确收敛到 AmountAfter。
- 播放器创建失败时 HUD 立即收敛并记录 VFX issue；不等待主线。
- Keypad9 等价入口改发新 VFX Cue。

## 验收

- 击杀、帮助卡、回收、商店/酒馆、金币卡与 DevTest 均走新 Binding。
- 数字不由 VFX 直接写、不逐金币回调、不双写。
- 第一枚抵达前数字不开始，最后一枚抵达时精确到目标值。

## 通用约束

- Part of #192。
- 遵守 ADR-0040、ADR-0001、ADR-0007、ADR-0018 与 `CONTEXT.md` 的 VFX 领域词汇。
- VFX 不成为规则权威，不占主线 ack；失败可见但不得阻断游戏流程。
- 不接管普通旧 FX 路径；仅金币是本 Spec 的明确迁移例外。
- 完成后同步实际受影响的 `docs/code-map/`，并满足 Unity recompile 后 Console 无本票新增 Error/Exception/Assert。
