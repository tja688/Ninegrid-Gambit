Part of #192.

## 目标

建立应用会话级 `IVfxSystem`、类型化 `VfxCueRequest` 与播放器注册表，并在既有 `TriggerPulseHub` 增加新 VFX 通道。

## 范围

- 保留旧 `PulseFx(string)` 与 `PulseAudio(...)`，新增 `PulseVfx(VfxCueRequest)`；禁止第二个业务静态 Hub。
- Request 流水线：解析 Binding、enabled、变体、延迟/冷却/排期、空间上下文、播放器创建、自然完成与结构化 outcome。
- 程序化播放器以稳定 `playerId` 自治内部算法/参数/资源/完成条件；注册表只声明 Pulse/State、Attached/Independent 与域能力。
- Pulse 支持 Binding 延迟、最短间隔、显式可取消排期；表现 RNG 不消费 Core RNG。
- VFX Runtime 在 `PresentationSceneRoot` 生命周期安装；场景实例另行清理。
- 失败不播放占位或重试，不抛进玩法调用链，但记录明确错误。

## 验收

- 类型化 Cue 从 Hub 到假播放器端到端可测。
- Unbound/Suppressed/InvalidBinding/PlayerUnavailable/DomainUnavailable/BackendFailure/Played 可区分。
- Independent Pulse 开始后不因业务宿主消失或 Binding 后续停用而中断。
- 旧 FX 与 Audio 通道行为不回归。

## 通用约束

- Part of #192。
- 遵守 ADR-0040、ADR-0001、ADR-0007、ADR-0018 与 `CONTEXT.md` 的 VFX 领域词汇。
- VFX 不成为规则权威，不占主线 ack；失败可见但不得阻断游戏流程。
- 不接管普通旧 FX 路径；仅金币是本 Spec 的明确迁移例外。
- 完成后同步实际受影响的 `docs/code-map/`，并满足 Unity recompile 后 Console 无本票新增 Error/Exception/Assert。
