# 测试与验证（现状）

> **开发冲刺期（2026-08）**：仓库内原有的 EditMode / PlayMode 自动化测试套件已**整体清空**（含 `NineGrid.Core.Tests`、`NineGrid.Presentation.Tests`、`NineGrid.DevTest.Tests`、`NineGrid.LivingUI.Tests`）。这是有意的阶段性决定，**不要**从 git 历史或旧文档中「找回来」、补全或按旧 `tests.md` 地图复刻护栏。
>
> 若你当前任务需要写测试，可自行新增程序集与用例，不受本条限制；本页只描述**当下默认验证方式**。
>
#172 起恢复最小测试程序集：`NineGrid.Presentation.Tests`（`Assets/Scripts/NineGrid.Presentation/Tests/`，Editor-only）包含 `MusicDiagnosticsBehaviorTests`（Music 轨审计、未知来源、Preview 暂停/恢复、**#179 快速切歌唯一当前+至多一个淡出**）、#173/#174/#186 `AudioSystemBehaviorTests`（起播点/绑定延迟、显式排期取消、**排期/取消 History**、按最终绑定冷却、随机池有效变体与避免立即重复、缺失素材后端失败、声音声明重复与未绑定扫描、**热应用保旧 catalog / Suppressed / Sequence+聚合 / Preview 不改冷却历史 / 排期回调用最新 catalog**）、#171 `PlayerAudioSettingsBehaviorTests`（Master/BGM/SFX 三路即时应用、逐路静音保留原音量、持久化、Reset 回随包作者默认、UI 绑定通知）、#176 `CardLifecycleAndCombatAudioTests`（Impact 交战结果声音顺序/格挡与治疗、运动 seam 移换转区分、生命周期/战斗声明不重复）、#175 `SkillEffectTrapRelicAudioTests`（内容覆盖优先级、动态 UID 排除、TriggerEffect 路由与无双发、绑定延迟必播、蓄力显式取消、未覆盖回退/未绑定静默）、#177 `FlowRoomEconomyAudioTests`（金币获得/消耗方向、上下楼与同层过场区分、进房 RoomId 上下文、余额不足原因识别、流程声明不重复）、#178 `AudioAiInitialBinderTests`（AI 草稿填充、人工确认默认保留、强制重绑覆盖、Catalog 卫生：重复键/缺失素材/孤儿绑定）与 **#179/#186/#187/#188** `AudioStructureGuardTests` / `AudioDeliveryHygieneTests` / `AudioBindingEditorSessionTests` / `AudioDiagnosticsBehaviorTests` / `AudioWorkbenchServerTests`（结构护栏源码扫描、正式 Catalog/声明卫生门禁、工作台单条与全部保存回撤/补丁夹紧与键冲突/快照恢复、**StopSfxSource 与 Suppressed trace**、loopback 鉴权/拒绝面/revision/**delta 推送**/**`/api/events` 长轮询回退**/**`AudioWorkbenchLauncher` 启动** /瞬态冲突门禁）与 **#195** `EditorWorkbenchTransportTests`（假 host 独立端口/协议/静态页与 Audio 并存不串扰）与 **#193** `VfxBindingCatalogTests` / `VfxDeliveryHygieneTests`（五维选择器解析顺序、严格 `TryFromJson`、BindingKey 不含运行时 UID、声明扫描、Editor Session 保存/覆盖冲突门禁、正式 `vfx_bindings.json` 与 `NineGrid.Presentation` 声明卫生、**#200 player 登记与素材路径卫生**）与 **#197** `VfxSpriteSheetPlayerContractTests`（`spritesheet_N` 帧序、有限/无限循环结束、scaled/unscaled 时间基、池化 Reset、Attached 宿主丢失 / Independent 自然播完、State 立即退出）与 **#201** `VfxPersistentStateBehaviorTests`（`SetSlot` 幂等/替换/清除、`ClearSlotIf` 条件清除、segment 退出段、`ReleaseOwner`、附着型场景清理、多 owner 多 slot 并存、结构化 outcome 与后端失败不抛）与 **#194** `VfxDiagnosticsBehaviorTests`（Request→Complete 生命周期、帧统计、峰值钻取 Binding/player/实例、issue/非 issue 分类、有界明细环保留累计聚合、PerfTrace session/batch 关联）与 **#202** `VfxWorkbenchServerTests` / `VfxSystemBehaviorTests` 工作台热应用/聚合/预览/冲突门禁覆盖 VFX 契约与 **#203/#199/#204/#200** `VfxGoldFlightPlayerContractTests` / `GoldHudNumberWindowTests` / `GoldGainPresentationMigrationTests` / `VfxStructureGuardTests` / `VfxCrossSystemContractTests`（gold-flight 时间窗与域宿主、HUD 数字窗口、调用迁移与旧壳删除、结构护栏、Hub 三通道与正式绑定/素材路径）。跑法：`unity command run_tests --mode EditMode --filter NineGrid.Presentation.Tests --filter_type assembly --project-path "<repo>"`。完整真实 PlayMode 终验记录见 `Assets/Notes/Logs/AudioAssetAudit/audio-e2e-verification-179.md`（音频）与 `Assets/Notes/Logs/VfxAudit/vfx-e2e-verification-200.md`（VFX/#192），不以 EditMode 冒充终验。
## 默认验证门槛（普通实施票）

1. **硬要求**：`unity command recompile` 后 Console 无**由本票改动导致**的新增 Error / Exception / Assert
2. **手动验证**：Play 模式、QuickTest 通道（`\0`–`\9`）、场景目视检查——按任务需要自行执行
3. **内容卫生**（改 Catalog / JSON 时）：Editor 菜单 `NineGrid/Content/…` 校验与 `ContentHygieneValidator` 汇总（见 [`README.md`](./README.md) `NineGrid.Content.Editor` 行）

**两击放弃**：同一验证动作（recompile / 查 Console）2 次尝试仍无果（超时 / 卡死 / 状态不明）即停，不换命令绕路；直接汇报改动结果，并建议人手动验证。

## 跑编译与 Console（Unity CLI）

```bash
unity command recompile --project-path "<repo>"
unity command recompile_status --project-path "<repo>"   # 轮询至 completed
unity command console --project-path "<repo>" --format json
```

细则见 [`docs/agents/unity-cli.md`](../agents/unity-cli.md)。

## 行为不变量去哪找

长期规则仍以 [`docs/adr/`](../adr/) 与 [`presentation.md`](./presentation.md) 为准；历史上由结构测试保护的约束（IntentIntake、卡面 Commit、指针输入等）现靠 ADR + code-map 文字约定。**例外**：音频绕过护栏（#179 `AudioStructureGuardTests`）与 VFX 绕过护栏（#200 `VfxStructureGuardTests`）已恢复为 EditMode 源码扫描自动化。
