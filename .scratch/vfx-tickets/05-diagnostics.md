Part of #192.

## 目标

建立不猜阈值、不主动限流的 VFX 可观测层。

## 范围

- 记录 Request/Resolve/Create/Start/Complete/EndReason 及 Cue/State、BindingKey、playerId、variant、空间类型、域、owner/slot、覆盖参数和关联 ID。
- 按帧统计创建、完成、活跃数；保存会话累计与历史峰值。
- 明细使用有界环；累计和峰值不随明细淘汰。
- 接入现有 PerfTrace session/chain/batch 关联。
- 错误与正常结束分类：配置/播放器/域/后端失败为 issue；Suppressed、人工停用、Attached host 丢失、场景退出为非错误结果。
- 不设置实例预算、丢弃、自动降质、软告警阈值或“多少算多”的颜色判断。

## 验收

- 可从峰值钻取到构成峰值的 Binding/player/实例。
- 不记录每帧完整 Transform；监控本身保持有界。
- Release 不启动完整历史服务，只保留轻量错误与累计。
- 普通旧 FX 不被扫描、猜测或纳入新系统统计。

## 通用约束

- Part of #192。
- 遵守 ADR-0040、ADR-0001、ADR-0007、ADR-0018 与 `CONTEXT.md` 的 VFX 领域词汇。
- VFX 不成为规则权威，不占主线 ack；失败可见但不得阻断游戏流程。
- 不接管普通旧 FX 路径；仅金币是本 Spec 的明确迁移例外。
- 完成后同步实际受影响的 `docs/code-map/`，并满足 Unity recompile 后 Console 无本票新增 Error/Exception/Assert。
