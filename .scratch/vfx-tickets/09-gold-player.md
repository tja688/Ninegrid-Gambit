Part of #192.

## 目标

把旧飞币与图标吞噬算法迁成新系统的程序化 Independent Pulse。

## 范围

- 注册稳定 `gold-flight` playerId；内部参数、资源、算法和完成条件由播放器自治。
- 迁移金币生成、数量分配、散布、错峰、飞行、缩放、排序、软/硬时窗压缩和 HUD 图标吞噬/复位。
- 通过专用金币 HUD Domain Host 获得父级、起终点转换、图标反馈与允许排序，不搜索或任意修改场景。
- 批次创建成功时一次性返回 `firstArrivalDelay`/`lastArrivalDelay` 表现计划；不建立逐金币回调。
- Independent 实例开始后自然播完；停用 Binding 只阻止后续批次。

## 验收

- 不同金币数量的首达/末达窗口与实际算法一致且末达有限。
- 宿主死亡不取消已开始飞币；场景退出按应用关闭规则清理。
- 图标缩放结束后精确恢复基础值。
- 运行时生命周期与峰值进入新诊断。

## 通用约束

- Part of #192。
- 遵守 ADR-0040、ADR-0001、ADR-0007、ADR-0018 与 `CONTEXT.md` 的 VFX 领域词汇。
- VFX 不成为规则权威，不占主线 ack；失败可见但不得阻断游戏流程。
- 不接管普通旧 FX 路径；仅金币是本 Spec 的明确迁移例外。
- 完成后同步实际受影响的 `docs/code-map/`，并满足 Unity recompile 后 Console 无本票新增 Error/Exception/Assert。
