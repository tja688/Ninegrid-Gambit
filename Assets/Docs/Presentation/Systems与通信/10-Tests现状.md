# Tests 现状（NineGrid.Presentation.Tests）

> 覆盖范围：`Tests/` 目录，共 **33 个 .cs**（根目录 8 + `Tests/Flow/` 25），全部 EditMode / NUnit。
> 背景澄清：`docs/code-map/tests.md` 记载的「冲刺期测试套件整体清空」**不适用于本程序集**——音频/VFX 主题的行为测试、结构护栏、交付卫生门禁都**仍在且生效**。`AudioStructureGuardTests`、`VfxStructureGuardTests` 等护栏测试没有被清掉；本目录其他篇章若提"护栏只靠文字纪律"，以本篇为准（已同步修正）。
> 2026-08-12 变更：三个工作台 web 层测试（`AudioWorkbenchServerTests` / `VfxWorkbenchServerTests` / `EditorWorkbenchTransportTests`）已删除——跑测试不再拉起回环服务器与网页；根目录新增六个规则回归测试（对账缝 / 位移挂起 / 神圣决斗 / 清关残留重置 / 遗物开局自愈 / 借甲图腾）。

程序集：`Tests/NineGrid.Presentation.Tests.asmdef`（Editor-only，NUnit；部分文件 `#if UNITY_EDITOR || DEVELOPMENT_BUILD`）。

## 跑法

```bash
unity command run_tests --mode EditMode --filter NineGrid.Presentation.Tests --filter_type assembly --project-path "<repo>"
```

---

## 根目录（8）

| 文件 | 验证行为 | 守护的不变量 |
|------|---------|-------------|
| `AvatarVitalityInvariantTests` | 6 用例：zone 异常但 HP>0 时战场指令仍合法；HP=0 时合法指令收缩为槽位管理；Avatar 失注册/HP=0 时 `DefeatIfAvatarDeadAction` 必须收束 Defeat；StartNode 自愈异常 zone、补跑 Defeat | ADR-0039 判死谓词统一：合法指令裁决 ≡ 战败收束，杜绝"僵尸 Avatar 永久软锁"。断言主体是 Core 侧行为，放本程序集属历史归置；夹具 `NineGridArchitecture.ResetForTests()` |
| `CardFaceReconciliationRegressionTests` | 随机动作风暴等场景下按表现层消费语义重放事件日志得到的卡面值 == oracle 重算值；对账事件紧邻因果动作、diff 后才发 | ADR-0045 统一对账缝：新增数值源零手工接线，卡面显示与结算源头同源 |
| `DeferredBoardMotionRegressionTests` | 交战窗 / 敌方行动阶段内效果 DSL 位移挂起到收尾锚点复验后落地（先打再转）；击杀落地前尸体不跟转；齐射盘面冻结成立 | ADR-0044 位移锁定窗口与锚点排水 |
| `HolyDuelMarkRegressionTests` | 双持有者连打时旧标记惩罚先结算、标记后转移，不因新目标同为持有者而静默 | 神圣决斗（skill.holy_duel）标记结算语义 |
| `NodeEndTransientResetTests` | 清关（节点完成）即清四档临时修正 + 当前甲回落有效甲，不拖到下局 StartNode | 「对战残留不出局」（`ClearNodeTransientModifiersAction`） |
| `RelicCompositeArmorNodeStartTests` | 装备栏遗物效果实例缺失时 StartNode 自愈重挂——复合盔甲开局按攻击加当前甲不再哑火 | `ReactivateMissingRelicEffectsAction` 幂等自愈 |
| `RelicGoldenCofferGrantTests` | 黄金鱼竿战斗内拾取：2 张宝箱卡洗入抽牌堆且遗物留装备栏；重挂/次节点自愈/读档语义不重发；局外拾取进道具卡格；丢弃后重获再发；凤凰羽毛 `removeRelic:true` 正常挂载 | ADR-0049 一次性遗物效果消费标记 + 战斗内 `Spawn` 帮助卡洗入抽牌堆 |
| `TrapArmorTotemBorrowedArmorTests` | 护甲图腾邻接怪 +1 借甲不叠加、借甲被消耗后刷新补回、离邻只流失未耗借甲（不扣自有甲）、远处怪不涨甲 | 借甲光环基线记账 + 维持补满 + 自然流失（`SyncAdjacentBorrowedArmorAction`，ADR-0050 补记） |

## Tests/Flow/（25）

### 音频系统行为（4）

| 文件 | 验证行为 | 守护的不变量 |
|------|---------|-------------|
| `AudioSystemBehaviorTests` | `RequestCue` 区分 startOffset / bindingDelay / cooldown 三参数语义；`ScheduleCue` 只能显式取消、不订阅发射者生存期；变体池跳过坏行、避免立即重复、记录实际变体；后端失败回传所选变体；`ApplyWorkbenchCatalog` 非法 JSON 不换 Catalog、成功则 revision+1 并影响后续请求、清绑定冷却记忆；排期 cue 触发时按**最新** Catalog 解析；禁用绑定记 Suppressed 并带全 selector 上下文；History 单调序号；工作台快照含历史/在播源/聚合；`PreviewWorkbenchBinding` 无视 enabled、不动冷却与 History；声明扫描器报重复/未绑定 | `AudioSystem` 是 SFX 唯一策略模块（ADR-0036）：冷却/变体/排期/热调音的全部裁决语义 |
| `MusicDiagnosticsBehaviorTests` | 快速三连切歌只保 1 当前 + ≤1 淡出中，过期淡出回调判 StaleCallback 不改状态；`AuditMusicTrack` 对未知来源产出带 chainId/代数关联的异常但**不自动停止**；已认领来源不算 unknown；`StopUnknownMusic` 只停未知、不碰已认领；Editor 试听替换旧 Preview、暂停当前+淡出中的游戏音乐、结束按原位置恢复且不改 DesiredState；同 clip 死来源重播而非 no-op；`ResetPlaySessionState` 清所有权使下次请求重播 | BGM 期望状态机的代数收口与"审计只报不停"纪律；Music 轨永远单一权威来源 |
| `PlayerAudioSettingsBehaviorTests` | 三总线（Master/Bgm/Sfx）音量即时生效且不动作者默认值；静音用静音值不丢原音量、解除后恢复；偏好落库重载、Reset 回作者默认并清库；`Changed` 通知稳定快照供 UI 绑定 | 玩家偏好（PlayerPrefs）与作者默认（包内）两层分离，静音可逆 |
| `AudioDiagnosticsBehaviorTests` | 快速重复 cue 达阈值发 1 条 `AudioCueBurstAnomaly`；SFX 轨闲置持续源三次巡检发 1 条 PersistAnomaly；无在播源不发快照；`StopSfxSource` 停已知源、拒空/未知/已死；`StopAllSfxSources` 清空在播快照；禁用绑定发 `AudioCueSuppressed` PerfTrace 且 payload 带 reason/bindingKey/selector | Dev 诊断（PerfTrace `AudioCue*` / `AudioSfx*`）的触发条件与去重语义（`#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`） |

### 音频结构护栏与交付卫生（3）

| 文件 | 验证行为 | 守护的不变量 |
|------|---------|-------------|
| `AudioStructureGuardTests` | **扫描四个源码根**（Presentation/Content/Core×2，剔除 Tests/Editor、剥注释）：禁引用 `AudioKit`；禁业务直调 `MMSoundManager`（Adapter/Bootstrap/Settings 三文件白名单）；禁非音乐 Adapter 选 Music 轨；禁裸字符串 `PulseAudio("...")`（TriggerPulseHub/AudioTriggerPulseSink 白名单）；禁动态 UID 声音键 `sfx.effect.<uid>` 拼接 | #179 结构护栏：**这是活的自动化约束，不是文字纪律**——《04》"业务禁直调 MMSoundManager"等条目由本测试强制执行，违规即挂测试 |
| `AudioDeliveryHygieneTests` | 正式 `audio_bindings.json` 过 `AudioBindingCatalogHygieneValidator` 无重复键/覆盖冲突/孤儿绑定/断链 clip/空说明/隔离根引用等 critical 项；全程序集 `[AudioCue]` 声明 id 唯一、note 非空 | 正式音频 Catalog 与声明清单的交付卫生门禁（#179）——改 JSON 或加 cue 声明后这条测试就是审计员 |
| `AudioAiInitialBinderTests` | `AudioAiInitialBinder.Run` 未绑定 cue 填 AiDraft、保留 HumanConfirmed（含音量等人工参数）；`forceRebindAll` 降级人工行为草稿；卫生校验器报 duplicate-binding-key / missing-clip / orphan-binding / coverage-conflict（同 cue 同特异度重叠） | #178 AI 初始绑定不覆盖人工确认；authoringStatus 生命周期（aiDraft→humanConfirmed）单向由人推进 |

### 各域声音落地（3）

| 文件 | 验证行为 | 守护的不变量 |
|------|---------|-------------|
| `SkillEffectTrapRelicAudioTests` | 绑定解析优先级 卡+技 > 技 > 卡 > 基础；动态 UID cueId（`sfx.effect.12`）判 legacy 拒作正式键；`EffectTriggerPulseBeatHandler` 对怪技/机关/遗物/帮助卡分别发 skill/trap/relic/effect 稳定 cue 且**单脉冲无重复**；bindingDelay 走 Adapter 必播（不进可取消排期）；蓄力音显式取消防迟到播放；无匹配内容覆盖回退基础 cue、全未绑定静默 Unbound；技能/机关 cue 声明唯一 | #175 技能/效果/机关/遗物声音的选择器语义与"延迟必播 vs 显式取消"分界 |
| `CardLifecycleAndCombatAudioTests` | `DamageFloaterBeatHandler` 出伤序列 Hit→ArmorAbsorb→HpDamage 无重复；帮助卡伤害跳过近战 Hit 保留分账；全甲格挡发 Hit+Block 不发 Hp/Armor；Healed 走旁路装饰（不认领指令）只发一次 Heal；`PulseMotion` Rotate/Move/Swap 三 cue 区分；生命周期/战斗 cue 声明唯一 | #176 从高层表演 seam 断言声音请求顺序与结果，不断言私有动画——战斗声音的"每事件恰好一次" |
| `FlowRoomEconomyAudioTests` | 金币演出按方向发 GoldGain/GoldSpend 恰一次；过场区分跨层 FloorCross 与同层 RunTransition；`IsInsufficientGoldReason` 精确匹配 Core 拒绝文案；进房脉冲带 RoomId 上下文；流程/经济 cue 声明唯一 | #177 房间/经济/跑图声音的公开 seam 契约 |

### 工作台会话（1）

| 文件 | 验证行为 | 守护的不变量 |
|------|---------|-------------|
| `AudioBindingEditorSessionTests` | Session 保存标 HumanConfirmed 清脏、Revert 回磁盘快照不动兄弟脏行、SaveAll/RevertAllDirty 往返；`TryReplaceWorkingDto` 负值夹紧为 0、保留 cueId/note/module/authoringStatus 权威字段、拒空补丁与 BindingKey 冲突；工作 JSON 快照往返恢复脏状态；Music Session 同样夹紧/拒空 | #187 工作台编辑会话：磁盘快照 = 回撤真相；权威字段不可经补丁改写 |

> 工作台 **web 传输层**测试（`AudioWorkbenchServerTests` / `VfxWorkbenchServerTests` / `EditorWorkbenchTransportTests`）已于 2026-08-12 删除——跑测试不再拉起回环服务器与网页；工作台本体（`EditorWorkbench/` transport 与各 `*WorkbenchServer`）仍在，其传输安全面（鉴权/拒绝面/revision 协议）现无自动化护栏。

### VFX 系统行为与诊断（3）

| 文件 | 验证行为 | 守护的不变量 |
|------|---------|-------------|
| `VfxSystemBehaviorTests` | `RequestCue` 六类结局：Unbound（无绑定）/InvalidBinding（同特异度歧义）/Suppressed（禁用）/PlayerUnavailable（工厂造不出）/DomainUnavailable（attached 缺宿主）/BackendFailure（播放器启动失败）；正常播经工厂→StartPulse；`ScheduleCue` 显式取消；independent 脉冲在宿主丢失与绑定禁用后不被追杀；`ClearSceneInstances` 只清 attached 留 independent；`TriggerPulseHub.PulseVfx` 端到端；工作台 Catalog 热应用 revision+1；快照含历史/聚合/活跃脉冲；Preview 无视 enabled 不动冷却 | ADR-0040：VFX Cue 管线的结局分类学与 attached/independent 空间所有权语义 |
| `VfxPersistentStateBehaviorTests` | `SetSlot` 同态 NoOp、换态旧投影按 exitMode 退出（immediate 立即 / segment 带 exitLoopLimit 排空）后新投影接管；attached 宿主丢失自动释放槽、可重放；`ClearSlotIf` 只清匹配态；`SetSlot(null)` 清槽；`ReleaseOwner` 清该 owner 全部槽不误伤他人；多 owner×多槽并存；Unbound/Suppressed/DomainUnavailable/BackendFailure 结局；投影失败不抛 | #201 持续状态槽：期望态收敛模型（slot 是期望，播放器是投影），退出模式契约 |
| `VfxDiagnosticsBehaviorTests` | 请求→解析→创建→启动→自然完成全阶段入诊断记录与帧统计；Unbound 记 Issue 并进 ReleaseCounters；Suppressed 不算 Issue；并发实例更新峰值并带 bindingKey/playerId/instanceId 下钻贡献者；有界环淘汰明细但聚合不丢；PerfTrace payload 带 batchId/sessionId/correlationId | #194 VFX 生命周期诊断：Issue 分类学与诊断关联层（ADR-0003）字段齐全 |

### VFX 播放器契约（4）

| 文件 | 验证行为 | 守护的不变量 |
|------|---------|-------------|
| `VfxSpriteSheetPlayerContractTests` | 帧推进纯数学：无限循环不完成、有限循环按圈数收尾、unscaled 时间基用 unscaled delta；`spritesheet_N` 数字序稳定（2<10）；State 播放器主段常驻、BeginExit 后结束；视图池 Release→Acquire 复位 sprite/color/enabled/scale；attached 脉冲宿主丢失即结束、independent 继续播完 | #197 sprite-sheet 播放器：帧序/循环/时间基/池复位契约 |
| `VfxGoldFlightPlayerContractTests` | `GoldFlightTiming` 首达/末达有限、与旧算法一致、大批量压进硬上限；`DistributeValues` 金额分账守恒；无宿主 StartPulse 失败且无 PresentationPlan；有宿主回传计划并自然完成（punch 次数 = 可视币数、settle 一次）；宿主中途死亡不取消已起飞币；经 VfxSystem 全链路回传计划并完成；`GoldHudDomainHost` punch 叠加、Snap/Settle 精确回基准 scale | #203 gold-flight：时间窗计划是 HUD 数字收敛的授时源；图标缩放无累积漂移 |
| `VfxParticlePlayerContractTests` | 预设库与 Content `VfxParticlePresetIds` 集合相等、Loop 标记符合 `particle.loop.*` 命名、note 非空；注册表能力位（Pulse+State、非素材型、参数覆盖白名单）；Pulse 装配出正确 ParticleSystem（unscaledTime/loop/sortingLayer/order 基准 5000+delta）、Cancel 拆净、粒子消亡后 Tick 收尾；fps 覆盖爆发数、tint 乘色、scale 缩尺寸；未知预设与 loop 预设挂 Pulse 拒绝；State 循环主段 + segment 排空退出、immediate 立拆；卫生校验器报未知键/错挂 | 粒子预设两侧表（Presentation 库 ↔ Content id 表）**由本测试强制一致**，新增预设漏一侧即挂 |
| `VfxProjectilePlayerContractTests` | 预设库与 `VfxProjectilePresetIds` 集合相等、FlightSpeed>0、Count 1~8、Impact/Muzzle 引用的粒子预设存在且非 loop；注册表能力位（仅 Pulse）；飞行全生命周期：源→靶推进、命中隐藏弹头+生成爆点、收尾拆净、回传首达/末达命中计划；多连发默认数与 fps 覆盖、错峰末达晚于首达；缺靶退化演示飞行不失败；未知/粒子预设键拒绝；Cancel 全拆；卫生校验报 projectile 挂 State 为 missing-player | 弹道预设两侧表一致 + 命中计划契约（表现授时用） |

### VFX 结构护栏、交付卫生与绑定解析（3）

| 文件 | 验证行为 | 守护的不变量 |
|------|---------|-------------|
| `VfxStructureGuardTests` | 扫描四源码根：禁新增第二个业务静态 Pulse Hub；禁业务直调 `IVfxSystem.RequestCue`（VfxSystem/VfxTriggerPulseSink 白名单）与 `SetSlot`；禁绕过 VfxSystem 直调 `StartPulse`（Systems/Vfx 目录白名单）；禁字符串拼接构造 `VfxCueRequest` 与动态 `vfx.*`/`fx.card.<uid>` id；禁把运行时 UID 写进 `VfxBindingKey.Compose`；播放器目录禁 `Camera.main`/`Find*`/`SortingGroup`/共享 Canvas（域宿主越界）；禁复活 `GoldGainFxManagerSingleton`（文件与类型双查） | #200/#204 结构护栏：**活的自动化约束**——《05》"业务禁直调 RequestCue/SetSlot/StartPulse"由本测试强制执行 |
| `VfxDeliveryHygieneTests` | 正式 `vfx_bindings.json` 过卫生校验无 critical（重复键/覆盖冲突/孤儿/断链素材/缺播放器/空说明等）；全程序集 `[VfxCue]`/state 声明 id 唯一、note 非空；playerId 全部已注册、素材型缺 materialKey/variants 报错、素材路径禁 `..`/`\`/绝对路径 | #193/#200 正式 VFX Catalog 交付卫生门禁 |
| `VfxBindingCatalogTests` | 解析优先级 卡+技 > 技 > 卡 > 基础；`TryFromJson` 拒非法/空集合不换 Catalog；BindingKey 不含运行时 UID；strict 解析对同特异度歧义报 Ambiguous 带双方 key；声明扫描报重复/未绑定；编辑会话 TrySaveCue 标 HumanConfirmed、拒覆盖冲突、非法 JSON 不换工作副本 | VFX 绑定选择器语义与 BindingKey 稳定性（#193） |

### 跨系统契约（1）

| 文件 | 验证行为 | 守护的不变量 |
|------|---------|-------------|
| `VfxCrossSystemContractTests` | `TriggerPulseOutputController` 源码含三通道生产装配（CardEffect/Debouncing/Audio/Vfx sink + Configure）；Hub 三通道独立脉冲、`ResetFxToNull` 只重置 FX 保 audio/VFX；正式 Catalog 的 `economy.gold_flight` strict 解析到已注册 gold-flight 播放器；正式飞币素材 `icons_full_32_9` 在 Resources 且切图有效；默认工厂能造已注册播放器；`PresentationSceneBindings` 与全程序集无 `GoldGainFxManager` 残留；`GoldGainPresentationBinder` 源码走 `TriggerPulseHub.PulseVfx` 稳定 cue | #200 Hub 三通道装配 + #204 金币迁移收口（旧壳不得复活，含源码级断言） |

### 金币与卡牌表现（3）

| 文件 | 验证行为 | 守护的不变量 |
|------|---------|-------------|
| `GoldGainPresentationMigrationTests` | `PresentationSceneBindings` 不再暴露 GoldGainFxManager；`GoldGainPresentationBinder.PresentGainVisual` 无宿主时不抛、返回无计划的非 Played 结果；有宿主时 Played + 计划与 `GoldFlightTiming` 一致 | #199 金币调用迁移：失败静默降级（无计划→数字直接收敛），成功回传授时计划 |
| `GoldHudNumberWindowTests` | `SampleDisplayed` 首达前保持旧值、末达时刻起精确等于新值、窗口内单调逼近、非法窗口直接跳新值 | #199 HUD 金币数字与飞币动画的时间窗对齐（首达不早跳、末达必收敛） |
| `CardDeckEntryDurationTests` | `EstimateEntryDuration` 与入场滑动+波纹公式一致（0/1/13 张三点校验）；`BeginDeckEntryAudio` 会话 PulseSlideBeat 发 DeckEntry cue | 发牌入场时长估算公式（排期器授时用）与入场声音 seam |

---

## 当下验证门槛

普通实施票：`unity command recompile` 后 Console 无本票导致的新增 Error/Exception/Assert（硬要求）+ 按需手动 Play / QuickTest `\0`–`\9`；两击放弃纪律见 `docs/code-map/tests.md`。

约束的两级保护要分清：

- **有自动化护栏的**（改了会挂 EditMode 测试）：禁直调 AudioKit/MMSoundManager、禁裸 `PulseAudio("...")`、禁动态 `sfx.effect.<uid>` / `vfx.*` 拼接、禁绕过 `TriggerPulseHub.PulseVfx` 直调 RequestCue/SetSlot/StartPulse、禁复活 GoldGainFxManagerSingleton、正式 audio/vfx Catalog 卫生、粒子/弹道预设两侧表一致——见上文护栏/卫生/契约各组。
- **只靠 ADR + code-map 文字约定的**：禁绕过 IntentIntake（ADR-0004）、禁新增业务静态 Sink、占格权威等输入/流程侧不变量——当前没有对应结构测试，本目录各篇的"不变量与坑"就是这些约束的落点清单。
