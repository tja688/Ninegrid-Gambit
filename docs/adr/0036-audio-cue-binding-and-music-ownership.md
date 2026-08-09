---
status: accepted
---

# 声音提示绑定、调音真源与唯一音乐状态

TableNine 的业务与表现代码只发出稳定的声音提示，或提交唯一的期望音乐状态；不直接选择素材，也不直接调用 AudioKit、MMSoundManager。项目自有的 QFramework 音频 System 负责提示解析、内容专属覆盖、延迟、音频起播点、最短间隔、随机音频池、播放历史与总线设置；MMSoundManager 仅作为实际播放 Adapter，SFX 与 BGM 共用其 Master、Music、Sfx Mixer 轨。随机音频池使用独立随机源，不消费 Core RNG，也不要求跨跑图复现。

音频 Runtime 在 `PresentationSceneRoot.OnBind` 阶段安装，覆盖主菜单到跑图结束的应用会话，不跟随只服务局内战斗的 `PresentationCompositionRoot` 启停。QFramework `IAudioSystem` 是调用方可见的深模块；Unity 生命周期 Controller 只负责安装、Tick 与重置，素材播放集中在 MMSoundManager Adapter。

声音提示采用带 Attribute 的 C# 声明和单一发射入口，Attribute 必须提供稳定 cueId、中文音效说明、所属模块、发射所有者及允许的上下文维度。编辑器机械扫描声明，形成可检索埋点表；运行时 UID 不得进入持久化绑定主键。全量埋点只覆盖玩家可感知且可能需要声音设计的动作，未绑定提示静默并进入诊断。

声音提示深化既有 `TriggerPulseHub`，不新增第二个业务静态 Sink：简单提示保留无上下文入口，内容相关提示使用只读 struct 请求携带固定字段与诊断来源。普通绑定延迟由音频模块集中排期且默认必播；可取消排期只服务尚未成立的蓄力/预备动作，返回轻量排期键并由动作的明确取消出口撤销，不建立对象订阅。

正式音频素材唯一位于 `Assets/Resources/audio/`：保留旧 `Assets/Arts/audios/` 的 GUID 搬入，重复的 `Assets/Arts/音频/` 经哈希与引用核对后删除，隔离素材留在 Resources 之外。模块化 JSON 是绑定与作者总线默认值的唯一真源；运行时只从固定路径加载 Catalog，Catalog 中的素材键指向 Resources 相对路径。编辑器以磁盘快照和工作副本提供单条/全部保存、单条回撤和一键回撤全部脏改动；点击 Save 后直接写入随包体发布的正式 JSON，不保留第三层本机覆盖。

每条声音绑定可配置素材或封闭随机池、绑定音量、音频起播点、绑定延迟、最短播放间隔和启用状态。内容专属覆盖按“联合内容标识 → 单一内容标识 → 基础提示”解析；调用方只提交基础提示和稳定上下文。音频起播点表示从素材内部哪个位置开始读取；绑定延迟用于声音与既有表现对齐，提示一旦被接纳便默认必播，不因发射宿主随后死亡或销毁而自动取消。只有蓄力、预备、持续施法等尚未成立的动作使用可取消排期提示，由动作已有的明确取消出口按排期键撤销；禁止为普通延迟建立对象订阅或普遍传播 CancellationToken。

BGM 不走声音提示脉冲。游戏流程层提交期望音乐状态，音频模块解析最终绑定；解析到同一 Clip 时默认不重启。每次实际切歌分配递增的音乐播放代数，维护一个当前来源与至多一个淡出来源；快速连续切歌时旧淡出必须收口，旧代数回调不得修改当前状态。现有 PerfTrace 承接状态请求、解析、播放、淡化、停止与异常重叠记录；Editor/Development Build 低频比较 MMSoundManager Music 轨实际播放集合与模块认领集合，报告可追溯的未认领 BGM，但默认不做侵入式自动停止。

音乐状态只由流程层提交，来源标识必填。切歌保持一个当前来源与至多一个淡出来源；新切歌到来时立即回收更老的淡出来源。Editor/Development Build 在状态请求、播放、淡出完成、场景切换及低频巡检时审计 Music 轨；未被当前来源、淡出来源或编辑器试听认领的播放记为异常，默认只报告并允许工具一键停止。编辑器试听 BGM 默认暂停游戏音乐并记录播放位置，结束后原位恢复，不改变期望音乐状态。

项目自有源码须有结构护栏：只有音频播放 Adapter 可以调用 MMSoundManager 播放，只有音乐模块可以指定 Music 轨，业务代码不得直接调用 AudioKit 或 MMSoundManager。#179 起由 EditMode `AudioStructureGuardTests` 扫描项目自有源码强制该约束，并禁止裸 `PulseAudio("...")` 与动态 `sfx.effect.<uid>` 绑定键拼接。完整调音工作台仅为 Editor 开发工具（系统浏览器 localhost HTML，非玩家可见 UI；#188 起为权威三模式交互面）；玩家 Master/BGM/SFX 音量与静音属于独立本地设置，不写回作者配置。

作者 Master/BGM/SFX 默认值属于正式 JSON；玩家三路音量与静音属于独立本地设置，两者不得共用同一权威。#187/#188 起作者态由单一 `AudioWorkbenchEditorState` 拥有双 Session：浏览器只发约定命令补丁，不改 JSON 文本；Play Mode 下 SFX 工作副本每次变更热应用到后续触发，唯显式保存写盘并标 `humanConfirmed`；未保存禁用为临时静音（`Suppressed`，UI 标「临时静音（未保存）」），保存后为永久禁用（「已永久禁用」）。程序集重载经 SessionState 恢复工作副本；若磁盘相对基线已变，阻塞 save/revert/apply，仅提供显式「恢复临时版」或「丢弃临时版」，无静默合并。完整作者 UI 为系统浏览器鉴权 localhost HTML 工作台（三模式：实时抓音 / 静态绑定库 / BGM）；Unity Editor 仅保留菜单启动器、loopback 服务与作者态编排，**不再**以 UI Toolkit 为主界面，无兼容双窗。

Editor/Development Build 下 `IAudioSystem` 暴露运行时热调音缝：`ApplyWorkbenchCatalog` 以严格 `AudioBindingCatalog.TryFromJson` 原子替换**后续**请求所用 catalog（非法 JSON、空 DTO、或 `bindings == null` 失败且不替换旧 catalog；合法空数组 `bindings:[]` 可成功装入空 catalog；Revision 递增仅在成功时）；`enabled=false` 的命中记为可观察的 `Suppressed`（trace `AudioCueSuppressed`，reason `workbench binding disabled`），不选变体、不播放，但仍进历史与聚合——未保存的禁用是临时静音，显式保存后写入正式 JSON 才成永久禁用。热应用保留 cue 级频率窗，清除绑定实例级冷却/变体记忆；已在播源不重启不停；已排期回调解析最新 catalog。每条 `AudioHistoryRecord` 有单调 `Sequence` 与完整请求上下文及 Adapter `SourceId`；快照含 History、PlayingSources（含 Adapter 认领标记 `Claimed`，区分逃逸的 MMSoundManager Sfx 源）、SceneOrphans（场景旁路 `AudioSource`，只报不停）、PersistAnomalies（持续播放异常进工作台，不只埋 PerfTrace）与按 BindingKey（无则 CueId）聚合。抓音工作台观测面含 **正在播放** 审计，不限于 cue History。`StopSfxSource` / `StopAllSfxSources` / `PreviewWorkbenchBinding` 经 diagnostics Adapter 停指定或全部 Sfx 源，或按 BindingKey 试听工作副本（忽略 enabled，不改冷却/burst/普通历史）；`IMusicSystem.StopMusicSource` / `StopUnknownMusic` 停 Music 轨指定或未知源。MMSoundManager 仍只在 Adapter 内。BGM 保持预览+保存，本 ADR 不要求 live music catalog 热换。

关键音频事件并入现有 PerfTrace 会话，与 Battle/Core/Perf 共用 sessionId、seed、runTag、chainId 和 batchId；SFX 另记录 `AudioCueScheduled` / `AudioCueCancelled`（含 `scheduleKey`）与播放/冷却/失败；音乐侧保留切歌代数因果。完整实时播放历史只保留在 Editor 固定容量环形缓冲。项目自有源码的结构护栏禁止绕过声音提示或音乐状态入口。

## 相关

- [ADR-0007](0007-unified-presentation-pipeline.md) — 表现排期与装饰消费
- [ADR-0008](0008-single-source-content-and-resources-loading.md) — 单一 Resources 根与可审查真源
- [ADR-0018](0018-trigger-visible-causality.md) — 触发的可见时序
- `CONTEXT.md` — 声音提示、声音绑定、音频起播点、绑定延迟、可取消排期提示、期望音乐状态
