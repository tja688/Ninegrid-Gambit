Part of #192.

## 目标

在新金币链路稳定后彻底清理旧实现，避免双轨与 Missing Script。

## 范围

- 删除 `GoldGainFxManagerSingleton`、旧 `GoldGainFxManagerDevKeys` 与旧 DevTest layer。
- 清理 `PresentationSceneRoot` / `PresentationSceneBindings` 字段、构造参数和告警。
- 清理 MainScene、UITestSence 的组件、序列化引用和旧 DevTest 接线。
- 仅在新 player 仍使用时保留金币 prefab；否则做引用审计后回收。
- 清理全部生产引用、注释和 code-map 单例记录。

## 验收

- 项目自有源码无 `GoldGainFxManagerSingleton` 生产引用。
- 两处场景无 Missing Script，金币演出和 HUD 数字仍通过新链路工作。
- Keypad9 等价验证保留。

## 通用约束

- Part of #192。
- 遵守 ADR-0040、ADR-0001、ADR-0007、ADR-0018 与 `CONTEXT.md` 的 VFX 领域词汇。
- VFX 不成为规则权威，不占主线 ack；失败可见但不得阻断游戏流程。
- 不接管普通旧 FX 路径；仅金币是本 Spec 的明确迁移例外。
- 完成后同步实际受影响的 `docs/code-map/`，并满足 Unity recompile 后 Console 无本票新增 Error/Exception/Assert。
