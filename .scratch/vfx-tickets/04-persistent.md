Part of #192.

## 目标

完整实现持续视觉状态运行时，不制造虚构业务内容。

## 范围

- API 采用 `SetSlot(owner, slot, desiredState|null)`；同值 no-op，新值原子替换，空值清除。
- 提供条件式 `ClearSlotIf(owner, slot, expectedState)`，防止旧流程误清后来状态。
- State Binding 与 Cue Binding 分轨，共享 Catalog 与 player 注册表；持续状态不使用冷却。
- 支持 immediate 与有限 exit 段；新状态开始时旧状态按声明退出。
- owner 释放、Attached host 丢失、场景/战斗退出清理；不引入音乐代数、孤儿轮询或自动纠偏。
- 工作台/测试可建立预览 owner/slot；不得增加“蓄势待发”等假玩法内容。

## 验收

- 幂等、替换、条件清除、退出段、owner 释放和场景清理可测。
- 状态投影失败不改变业务期望或 Core 状态。
- 同 owner 多 slot、多个 owner 同 state 可并存。
- 无任何虚构业务发射点进入生产代码。

## 通用约束

- Part of #192。
- 遵守 ADR-0040、ADR-0001、ADR-0007、ADR-0018 与 `CONTEXT.md` 的 VFX 领域词汇。
- VFX 不成为规则权威，不占主线 ack；失败可见但不得阻断游戏流程。
- 不接管普通旧 FX 路径；仅金币是本 Spec 的明确迁移例外。
- 完成后同步实际受影响的 `docs/code-map/`，并满足 Unity recompile 后 Console 无本票新增 Error/Exception/Assert。
