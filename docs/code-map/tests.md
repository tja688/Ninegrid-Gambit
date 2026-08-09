# 测试与验证（现状）

> **开发冲刺期（2026-08）**：仓库内原有的 EditMode / PlayMode 自动化测试套件已**整体清空**（含 `NineGrid.Core.Tests`、`NineGrid.Presentation.Tests`、`NineGrid.DevTest.Tests`、`NineGrid.LivingUI.Tests`）。这是有意的阶段性决定，**不要**从 git 历史或旧文档中「找回来」、补全或按旧 `tests.md` 地图复刻护栏。
>
> 若你当前任务需要写测试，可自行新增程序集与用例，不受本条限制；本页只描述**当下默认验证方式**。
>
#172 起恢复最小测试程序集：`NineGrid.Presentation.Tests`（`Assets/Scripts/NineGrid.Presentation/Tests/`，Editor-only）包含 `MusicDiagnosticsBehaviorTests`（Music 轨审计、未知来源、Preview 暂停/恢复）、#173/#174 `AudioSystemBehaviorTests`（起播点/绑定延迟、显式排期取消、按最终绑定冷却、随机池有效变体与避免立即重复、缺失素材后端失败、声音声明重复与未绑定扫描）、#171 `PlayerAudioSettingsBehaviorTests`（Master/BGM/SFX 三路即时应用、逐路静音保留原音量、持久化、Reset 回随包作者默认、UI 绑定通知）、#176 `CardLifecycleAndCombatAudioTests`（Impact 交战结果声音顺序/格挡与治疗、运动 seam 移换转区分、生命周期/战斗声明不重复）、#175 `SkillEffectTrapRelicAudioTests`（内容覆盖优先级、动态 UID 排除、TriggerEffect 路由与无双发、绑定延迟必播、蓄力显式取消、未覆盖回退/未绑定静默）与 #177 `FlowRoomEconomyAudioTests`（金币获得/消耗方向、上下楼与同层过场区分、进房 RoomId 上下文、余额不足原因识别、流程声明不重复）。跑法：`unity command run_tests --mode EditMode --filter NineGrid.Presentation.Tests --filter_type assembly --project-path "<repo>"`。
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

长期规则仍以 [`docs/adr/`](../adr/) 与 [`presentation.md`](./presentation.md) 为准；历史上由结构测试保护的约束（IntentIntake、卡面 Commit、指针输入等）现靠 ADR + code-map 文字约定，**无自动化护栏**。
