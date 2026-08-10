Part of #192.

## 目标

为 #192 的架构边界建立机械护栏并执行最终真实场景验收。

## 范围

- 结构护栏：业务不得绕过类型化 VFX Cue/State 入口；禁止动态运行时 UID Binding Key；禁止程序化 player 绕过域宿主修改共享视觉所有权。
- 正式 Catalog/声明/播放器注册/素材路径卫生测试。
- Hub 三通道、Audio transport 回归、Pulse/Persistent 生命周期、诊断累计/峰值与 Workbench 行为契约。
- 真实场景验证序列帧预览、持续状态预览、Binding 临时/永久停用、聚合钻取。
- 金币真实路径：击杀、帮助卡、回收、商店/酒馆、扣金、Keypad9、首达末达数字窗口。
- 更新 `docs/code-map/README.md`、`presentation.md`、`tests.md` 为最终已落地事实。

## 验收

- Unity recompile 后无本 Spec 新增 Error/Exception/Assert。
- MainScene/UITestSence 无 Missing Script。
- Audio 工作台与播放系统无回归。
- #192 中每条实现决策有代码、测试或明确的 Out of Scope 对应；提交终验记录并关闭 Spec。

## 通用约束

- Part of #192。
- 遵守 ADR-0040、ADR-0001、ADR-0007、ADR-0018 与 `CONTEXT.md` 的 VFX 领域词汇。
- VFX 不成为规则权威，不占主线 ack；失败可见但不得阻断游戏流程。
- 不接管普通旧 FX 路径；仅金币是本 Spec 的明确迁移例外。
- 完成后同步实际受影响的 `docs/code-map/`，并满足 Unity recompile 后 Console 无本票新增 Error/Exception/Assert。
