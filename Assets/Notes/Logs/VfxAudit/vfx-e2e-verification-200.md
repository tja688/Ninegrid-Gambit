# #200 VFX 结构护栏、跨系统契约与端到端终验记录

生成时间：2026-08-11  
范围：#192 Spec 收口——结构护栏、Catalog/播放器卫生、Hub 三通道与既有契约回归、工作台/金币真实路径验收清单。

## 结论摘要

| 类别 | 状态 | 依据 |
|------|------|------|
| EditMode 结构护栏 / Catalog 卫生 / 跨系统契约 | 以本票测试跑通为准 | `VfxStructureGuardTests` / `VfxDeliveryHygieneTests` / `VfxCrossSystemContractTests` |
| MainScene / UITestSence Missing Script | **missing=0**（2026-08-11 Agent 扫描） | `CliEval/_scan_missing_scripts_200.cs` |
| Audio transport 回归 | EditMode Passed | `EditorWorkbenchTransportTests` 1/1 |
| Pulse Runtime / Workbench 回归 | EditMode Passed | `VfxSystemBehaviorTests` 14/14；金币迁移 3/3 |
| 正式 `vfx_bindings.json` / 声明 / player 注册 / 素材路径 | 已机械门禁 | `VfxDeliveryHygieneTests` |
| 完整真实 PlayMode 终验 | **待人工勾选** | 下表；不以 EditMode 冒充终验 |

## EditMode / 机械已覆盖（不得当作完整终验）

- 结构护栏源码扫描：禁第二静态 Pulse Hub、业务直调 `RequestCue`/`SetSlot`/`StartPulse`、动态 UID Binding Key / 动态 cue 拼接、程序化播放器绕过域宿主（`Camera.main` / `GameObject.Find` / `SortingGroup` / Canvas）、`GoldGainFxManagerSingleton` 复活
- Catalog 卫生：正式绑定无 critical findings；声明无重复 ID / 空中文说明；player 必须登记；素材型须有 material；路径禁止 `..` / 绝对/反斜杠
- Hub：生产装配源码三线（FX / debounce Audio / 类型化 VFX）；运行时三通道可独立脉冲；`ResetFxToNull` 保留 Audio/VFX
- 金币：`economy.gold_flight` → `gold-flight`；Resources `VFX/GoldFlightCoin`；Binder 经 `PulseVfx`；无旧壳类型
- 既有 #193–#204 行为契约测试仍为回归基线（见 `docs/code-map/tests.md`）

## 真实 PlayMode 人工勾选清单

在 Editor Play Mode（主场景）打开 `NineGrid/视觉特效/VFX 绑定调试工作台`，并保留 Audio 工作台可开以确认无串扰。勾选后在本文件追加日期与操作者。

### 工作台 / 生命周期

- [ ] 序列帧（sprite-sheet）Binding 预览可播；实例流可见 Request→Complete
- [ ] 持续状态页：预览 `SetSlot`、清除单槽、停用 State Binding 结束当前投影
- [ ] Binding 临时停用（未保存 `enabled=false`）抑制后续 Pulse；显式保存后永久 `enabled=false`
- [ ] 聚合钻取：实例 → Binding → Cue/State → Player/素材；峰值构成可读
- [ ] Audio 工作台仍可用；与 VFX 端口/DTO/页面不串扰

### 金币真实路径

- [ ] 击杀赏金：飞币自尸体/Impact 锚点飞向金币图标；数字自首达推进、末达收敛
- [ ] 帮助卡结算（未用帮助卡等）飞币与数字窗口正确
- [ ] 回收（道具/遗物）得金路径正确
- [ ] 商店 / 酒馆购刷等得金路径正确
- [ ] 扣金：数字与图标瞬时 Snap，无飞币
- [ ] Keypad9 DevTest 等价入口仍走 `PresentGainVisual`
- [ ] 首达前数字不变；末达后精确等于 AmountAfter

### 场景卫生

- [ ] MainScene / UITestSence 无 Missing Script（尤其旧 `GoldGainFxManagerSingleton`）
- [ ] Console 无本 Spec 新增 Error / Exception / Assert

## Agent 本票执行说明

- Agent 交付了结构护栏、卫生扩展、跨系统契约、code-map 收口与本终验记录。
- EditMode（2026-08-11）：`VfxStructureGuardTests` 3/3、`VfxCrossSystemContractTests` 7/7、`VfxDeliveryHygieneTests` 3/3、`EditorWorkbenchTransportTests` 1/1、`VfxSystemBehaviorTests` 14/14、`GoldGainPresentationMigrationTests` 3/3 Passed；`recompile` completed、errors=[]。
- 场景扫描（同日）：MainScene / UITestSence `missing=0`。
- 完整 PlayMode 依赖人手动勾选上表；完成后可将本段状态改为「已人工终验」并注明日期。
- #192 实现决策均有代码、测试或明确 Out of Scope 对应；本票关闭后可关 Spec。

## 相关产物

- ADR-0040 / `docs/code-map/presentation.md` / `docs/code-map/tests.md` / `docs/code-map/README.md`
- 对照音频终验体例：`Assets/Notes/Logs/AudioAssetAudit/audio-e2e-verification-179.md`
