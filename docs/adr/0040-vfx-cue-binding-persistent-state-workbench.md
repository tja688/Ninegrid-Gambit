---
status: accepted
---

# 视觉特效提示绑定、持续视觉状态与调试工作台

TableNine 的新视觉特效系统沿用音效系统已经验证的四层组织：稳定语义声明、持久化绑定、运行时播放、诊断与浏览器工作台；但只共享确实与载体无关的基础设施，不把 Audio 与 VFX 强行抽成万能 Effect 框架。业务代码分别发射 Audio Cue 与 VFX Cue；新 VFX 通过既有 `TriggerPulseHub` 增加类型化通道，不建立第二个业务静态入口。视觉特效不是规则权威，不占主线 ack；失败时不播放替代或回退表现，必须留下可归因错误，但不得中断规则或表演主线。

`vfx_bindings.json` 是“是否播放、调用哪个稳定播放器”的运行时单一真源。绑定按 Cue 或持续 State 声明解析，并复用声音绑定的固定稳定内容选择器与最高特异性规则；运行时 UID、坐标和宿主引用只进入本次视觉空间上下文，不进入绑定主键。现有 `visual_effects.json` 继续作为 Editor 素材浏览与导入索引，不参与运行时默认值合并。素材型播放器可以由绑定提供真实公共调参及显式覆盖白名单；程序化播放器自治其算法、内部参数、资源与完成条件，总控只校验稳定 `playerId`、Pulse/State 能力、空间所有权和视觉域能力，不伪造一套万能参数 schema。

一次性 VFX Cue 是发射后不持有业务所有权的 Pulse。独立型 Pulse 在请求接纳时取得确定视觉空间，开始后不因业务宿主消失或工作台停用而中断，按播放器完成契约自然播完；停用 Binding 只抑制后续请求。附着型特效依赖视觉域宿主，宿主丢失即按声明的正常生命周期结束。视觉域宿主只向实例提供受控父级、遮罩、跟随、坐标转换和排序边界；播放器不得越界修改共享相机、Canvas、卡级 SortingGroup 或卡牌底盘变换塔。

持续视觉状态不伪装成无限循环 Pulse，也不照搬 BGM 播放代数与幽灵源防护。系统以 `owner + slot + state` 表达期望视觉槽状态：同值重复提交无操作，新值原子替换旧值，空值清除，旧流程只能条件式清除仍属于自己的状态。首版完整交付 Persistent Slot API、State Binding、Attached 生命周期、立即或有限退出段、所有者/场景清理及工作台持续状态页；当前没有真实持续特效内容，因此只以契约测试和工作台预览验证，不虚构“蓄势待发”等业务状态。

VFX Runtime、绑定、历史与工作台覆盖应用会话；实例、视觉域宿主和持续槽随场景或战斗明确装配与清理。运行时不设置实例预算、自动丢弃、动态降质、软告警阈值或“多少算多”的猜测；Editor/Development 记录可归因的全生命周期事实、逐帧创建/完成/活跃数、会话累计与真实峰值，并写入现有 PerfTrace 关联层。Release 不启动工作台，只保留轻量错误与累计。未绑定、非法绑定、播放器/视觉域不可用及后端异常是显式错误；作者停用、工作台抑制、附着宿主正常消失和场景退出是非错误结果。

VFX 作者工具复用音效工作台的 localhost 鉴权传输、revision envelope、WebSocket/长轮询、主线程命令泵、工作副本、显式保存、程序集重载恢复和磁盘冲突门禁；Audio/VFX 的 DTO、命令、页面与诊断保持独立。VFX 工作台提供实时实例冒泡、静态 Binding 库、持续状态观察，以及实例→Binding→Cue/State→Player/素材的聚合钻取。Pulse 只允许观察；停用 Pulse Binding 只阻止后续播放。持续状态允许清除单个 `owner + slot`，或停用 State Binding 并结束其当前投影。临时停用只热应用到工作副本，显式保存后写入 `enabled=false` 永久停用。

现有旧 FX 路径不纳入新工作台、诊断或控制，也不要求整体迁移；新旧表现可以长期共存并由调用点显式叠加。金币飞入是明确例外：先修复 `GoldModified` 缺少来源 `CardUid`、金币在尸体卸载后的 `Settled` 才消费以及 Binder 订阅不稳的问题，再以程序化独立 Pulse 迁移飞币生成、散布、飞行、缩放和 HUD 图标吞噬，最后删除 `GoldGainFxManagerSingleton`、旧 DevTest 与场景序列化装配。金币数字与 Core 金币权威仍由 HUD 与规则核负责：金币播放器按批次一次性给出首达至末达的金币表现时间窗，HUD 在其中独立推进数字并在末端精确收敛到 `AmountAfter`，不建立逐金币回调。

## 相关

- [ADR-0036](0036-audio-cue-binding-and-music-ownership.md) — 声音提示绑定、工作台传输与结构护栏的参照系
- [ADR-0007](0007-unified-presentation-pipeline.md) — 装饰消费与表演锚点；金币需由 `Settled` 改至 `Impact`
- [ADR-0001](0001-battle-presentation-unified-timeline-batch-ack.md) — 特效/音效退化为脉冲 Trigger、装饰不进主线 ack
- [ADR-0018](0018-trigger-visible-causality.md) — 触发的可见时序
- [ADR-0008](0008-single-source-content-and-resources-loading.md) — 单一 Resources 根与内容真源
- `CONTEXT.md` — 视觉特效提示、视觉特效绑定、视觉空间上下文、视觉域宿主、附着型/独立型特效、持续视觉状态、期望视觉槽状态、金币表现时间窗
