# #179 音频全流程终验记录

生成时间：2026-08-09  
范围：主菜单 → 跑图 → 普通战斗 → 层主战 → 胜负 → 返回主菜单，以及交付护栏。

## 结论摘要

| 类别 | 状态 | 依据 |
|------|------|------|
| EditMode 契约（结构护栏 / Catalog 卫生 / 排期取消 PerfTrace / 快速切歌 / 工作台保存回撤 / 既有 #171–#178） | 以本票测试跑通为准 | `NineGrid.Presentation.Tests` |
| 正式 Catalog / 声明卫生（重复、覆盖冲突、断链、孤儿、空说明、隔离路径） | 已机械门禁 | `AudioDeliveryHygieneTests`；执行期 findings=0 |
| 完整真实 PlayMode 可听终验 | **待人工勾选** | 下表；不以 EditMode 冒充终验 |

## EditMode / 机械已覆盖（不得当作完整终验）

- 快速连续音乐状态切换：唯一当前 + 至多一个淡出；旧淡出回调记 `StaleCallback`
- 未认领 Music：审计报告 + 显式 Stop Unknown；Preview 暂停/恢复原位
- 0.8s 绑定延迟经 Adapter `InitialDelay`；蓄力 `ScheduleCue` / `CancelScheduledCue` 写入 History + PerfTrace（`AudioCueScheduled` / `AudioCueCancelled` + `scheduleKey`）
- 随机池变体变化、避免立即重复、History 记录 `VariantId` / 实际素材
- 工作台 `TrySave` / `TrySaveAll` / `Revert` / `RevertAllDirty`（无磁盘路径工作副本）
- 玩家 Master/BGM/SFX 即时、静音保留音量、持久化、Reset（FakeStore）
- 结构护栏源码扫描：禁 AudioKit / 业务 MMSoundManager / 非 Adapter Music 轨 / 裸 PulseAudio 字符串 / 动态 `sfx.effect.<uid>`
- Catalog 卫生：正式 `audio_bindings.json` 无 critical findings；声明无重复 ID / 空中文说明

## 真实 PlayMode 人工勾选清单

在 Editor Play Mode（主场景）按路径听检，并打开 `NineGrid/音频/声音绑定调音工作台` 对照 History / Music 诊断区。勾选后在本文件追加日期与操作者。

- [ ] 主菜单 BGM 可听；Start Hover / Press / 确认进入跑图
- [ ] 跑图探索 BGM 切换正确；连续进出房间无叠歌（合法淡化最多当前+一个淡出）
- [ ] 普通战斗 BGM；卡牌 Hover / 拖放 / 攻击命中 / 技能触发可听
- [ ] 层主战 BGM（`BossBattle`）；胜负 Notice SFX + Victory/Defeat BGM
- [ ] 返回主菜单：音乐回到 MainMenu，无幽灵 Music 来源；若人为注入未认领来源，工作台可报告并 Stop
- [ ] 技能绑定延迟 0.8s 听感对齐；宿主提前死亡后已接纳延迟仍播
- [ ] 蓄力/预备取消后无迟到声音
- [ ] 随机池条目多次触发有变化且不立即重复；History 显示实际素材
- [ ] 工作台：改一条 → Save；改多条 → Save All Dirty；脏改 → Revert / Revert All Dirty
- [ ] BGM Preview 不与游戏音乐重叠，结束后从原位置恢复
- [ ] 玩家设置面板：Master/BGM/SFX 与静音即时生效；退出 Play 再进后恢复；解除静音保留原音量
- [ ] PerfTrace / 工作台 History：可从请求追到排期、播放、冷却、取消或失败；音乐按代数还原切歌因果

## Agent 本票执行说明

- Agent 交付了诊断字段、结构护栏、卫生门禁、EditMode 契约与文档收口。
- EditMode（2026-08-09）：`NineGrid.Presentation.Tests` **44/44 Passed**（含 #179 新增结构护栏 / Catalog 卫生 / 工作台保存回撤 / 快速切歌 / 排期取消 History）。
- Play Mode 冒烟（同日，非终验）：进入主场景 Play 后 `IAudioSystem` / `IMusicSystem` / `IPlayerAudioSettingsSystem` 均已安装，`CurrentState=MainMenu`，MMSoundManager 就绪；Console 无本票新增 Error。**不代替**下表全流程可听勾选。
- 完整主菜单→胜负可听终验依赖人手动 Play 勾选上表；完成后可将本段状态改为「已人工终验」并注明日期。

## 相关产物

- `#178` AI 绑定审计：`audio-ai-initial-bind-178.json`（summary 含 `hygiene=0`；unused clips 为库存未绑定素材，不构成孤儿绑定）
- ADR-0036 / `docs/code-map/presentation.md` / `docs/code-map/tests.md`
