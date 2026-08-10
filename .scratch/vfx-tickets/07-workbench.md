Part of #192.

## 目标

交付 VFX 的实时观察与作者闭环。

## 范围

- 三模式：实时 Pulse 实例流、静态 Binding 库、持续状态观察。
- 聚合层级：实例→Binding→Cue/State→Player/素材；默认实例冒泡，可切 Binding 合并。
- 复用工作副本、显式 Save/Revert、Save All、SessionState 恢复与 DiskChanged 冲突门禁。
- Play Mode 热应用：Pulse 改动只影响后续实例；持续状态按最新 Binding 更新或安全重建。
- Pulse 不提供强杀当前实例；停用 Binding 只抑制后续请求。
- 持续状态可清单个 owner+slot；全局停用 State Binding 并结束当前持续投影。
- 未保存 enabled=false 为临时停用；保存后永久写盘。

## 验收

- 能从实例定位 Binding，从 Binding 查看累计/峰值和失败原因。
- 程序化播放器可观察、预览触发和停用，但工作台不伪造其内部调参表。
- Audio/VFX 页面、DTO、命令和作者状态互不污染。
- Release 不启动工作台。

## 通用约束

- Part of #192。
- 遵守 ADR-0040、ADR-0001、ADR-0007、ADR-0018 与 `CONTEXT.md` 的 VFX 领域词汇。
- VFX 不成为规则权威，不占主线 ack；失败可见但不得阻断游戏流程。
- 不接管普通旧 FX 路径；仅金币是本 Spec 的明确迁移例外。
- 完成后同步实际受影响的 `docs/code-map/`，并满足 Unity recompile 后 Console 无本票新增 Error/Exception/Assert。
