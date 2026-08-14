# Ninegrid Gambit · 权威代码事实文档库

> **定位**：权威代码事实文档库，初版生成日期 **2026-08-12**，对象是 `Assets/Scripts/` 全部代码（初版共 816 个 .cs）。目的：遇到恶性 bug 时，凭这套文档最快速地理解架构、掌控代码、分析推测和定位问题。
>
> **权威性声明**：本库以**当下代码实际实现**为准写成（文档/注释与代码冲突处均按代码事实记录并标注）。本库为**持续维护文档**：后续代码改动须同步更新对应区域文档（见根 `AGENTS.md`「Code Map 维护」）。日常长期权威口径由 `docs/code-map/` + `docs/adr/` + `CONTEXT.md` 与本库共同承担：前者管边界与不变量，本库管全量代码事实与事故导航。

## 目录结构

| 文档 | 内容 |
|------|------|
| [00-架构总览](00-架构总览.md) | 分层架构、核心运行循环（Run→Floor→Node→Battle）、十大架构范式鸟瞰 |
| [01-程序集与场景装配](01-程序集与场景装配.md) | 13 个 asmdef + 2 处 Assembly-CSharp 的清单/引用/文件数；MainScene 装配链与无场景自举点 |
| [02-ADR索引与代码对照](02-ADR索引与代码对照.md) | ADR 逐条（0001–0051）：决策一句话、已核实落地代码位置、关键行为不变量——「从行为规则找代码」的关键索引 |
| [03-领域词汇表](03-领域词汇表.md) | 领域词汇 → 含义 + 代码体现位置 |
| [04-预发布可疑问题总清单](04-预发布可疑问题总清单.md) | 六区读码发现的可疑问题汇总（31 条，其中 #31 已修）：疑似真 bug / 出包风险 / 废止残留与死代码 / 文档与口径矛盾 / 代码卫生，每条附出处链接 |
| [Core/](Core/) | `NineGrid.Core` 规则核（93 文件）区域文档 |
| [Content/](Content/) | `NineGrid.Content` + `Content.Editor` + `DevTest`（149 文件）区域文档 |
| [Presentation/Flow/](Presentation/Flow/) | 表现层 Flow 子树（216 文件）：导演/时间线/流程壳/房间/存档/教学/诊断/人读战斗日志 |
| [Presentation/Cards/](Presentation/Cards/) | 表现层 Cards 子树（166 文件）：卡视图/场地/手牌/牌库/卡面/特效 SO/静态 Hook |
| [Presentation/Systems与通信/](Presentation/Systems与通信/) | Systems/Commands/Queries/Controllers/Setup/Ui/Cheat/Platform/Editor/Tests（约 183 文件） |
| [Platform桥接/](Platform桥接/README.md) | `NineGrid.SteamBridge`（7）+ `NineGrid.SaveBridge`（1）：Steam 成就/云存档/回调泵 + ES3 落盘桥 |
| [工具与实验/](工具与实验/README.md) | `VisualFxLab`（8）+ `UI`/`VisualLook`（5）+ `Temporary Test`（2）：画面实验室/像素 Look 管线/临时脚本 |

## 按 bug 类型导航

| 症状类型 | 从哪入手 | 关键 ADR |
|----------|----------|----------|
| 战斗表演卡死 / 时间线不推进 / 输入没反应 | [00](00-架构总览.md) §4.1/4.3 → [Presentation/Flow/](Presentation/Flow/)（PresentationDirector/BattleTimeline/IntentIntake） | 0001, 0004 |
| 卡面数字错 / 血甲攻显示与结算不符 / 抢跳变 | [02](02-ADR索引与代码对照.md) 表演编排组 → CardFaceStatHandler/BattleBeatScheduler；Core 发射侧对账缝 CardFaceReconciliation（Cause=faceReconcile 可过滤诊断） | 0005, 0045, 0007, 0028 |
| 交战/齐射中盘面转动时机怪 / 转走还挨打 | BattleScopeSystem 位移锁定窗口 + PhaseSystem 收尾锚点排水 | 0044, 0012 |
| 文本语言不对 / 英文缺失回中文 / 词条断链 | LanguageSettingsSystem + LocalizationCatalog 三表；卡面覆盖缝 CardPresentationConfigCatalog.TryGet | 0046 |
| 点击点不中 / 悬停粘连 / 命中区怪异 | [02](02-ADR索引与代码对照.md) 输入命中组 → SlotClaimRegistry/GroundFieldHitSurface/PointerHitRouter | 0006, 0023, 0024 |
| 补牌/旋转时机错 / 空格不补 / 补牌触发效果误伤 | BoardStabilizationSystem + Scheduler | 0034, 0012 |
| 怪物不开火 / 开火节奏错 / 倒计时显示错 | CardRhythm + PhaseSystem 敌方行动 + ActionCount 提交链 | 0011, 0012, 0013, 0038 |
| 清关不触发 / 离开机关打不死 / 清场残留 | PhaseSystem 清关链 + CardCombatRules 双桶 | 0026, 0017 |
| 效果没触发 / 乱触发 / 装配报错 | Effects DSL（requires/conditions 分诊 + ProbeWhyNotTriggered）→ [Core/](Core/) | 0009, 0010, 0018 |
| 内容/卡面配置错 / 描述数字错 / 断链 | [Content/](Content/) + ContentHygieneValidator | 0008, 0035, 0037, 0029 |
| 战败不触发 / 0 血还能操作 / 软锁 | AvatarDefeatFollowUp（判死谓词唯一） | 0039 |
| 存档丢失 / 读档发牌不一致 / 云存档冲突 | [Platform桥接/](Platform桥接/README.md) + RunSaveService/RunSaveGame | 0041, 0043 |
| Steam 成就/云不工作 | [Platform桥接/](Platform桥接/README.md)（平台缺位零影响是预期行为） | 0043 |
| 音频不响 / BGM 重叠 / 音量设置异常 | AudioSystem/MusicSystem + audio_bindings.json + 抓音工作台 | 0036 |
| 特效不播 / 金币飞行异常 / 持续特效残留 | VfxSystem + vfx_bindings.json + VFX 工作台 | 0040 |
| 教学关异常 / 首次进游戏流程错 | Flow/Tutorial + TutorialProgressStore | 0042 |
| 房间/商店/图标交互异常 | Flow/RoomIcons + ShopBoard/TavernBoard/RewardBoard | 0020, 0021, 0022, 0025 |
| 画面异常（扫描线/像素/文字发糊） | [工具与实验/](工具与实验/README.md)（VisualLook 管线；FxLab 仅 Dev） | — |
| 卡在格子里飘/抖不停 / 抖屏太晃 / 卡带着偏移收敛歪位 | L1 装饰层单一写者 `BoardCardLifeFx`（[Cards/07](Presentation/Cards/07-卡面特效与装饰.md) §8）+ 抖屏 `ScreenImpact`（[Flow/05](Presentation/Flow/05-表演锚点排期与触发脉冲.md) §5）；三个全局旋钮可直接关 | 0051 |
| Windows 掉帧 / 高回报率鼠标 / 进程卡死 | Presentation/Platform（mitigation + WindowsHangWatchdog，命令行开关 `-ng-no-rawinput`/`-ng-no-watchdog`） | 0006 |
| 想从日志定位 | 诊断关联键 chainId/choreoSeqId；日志在 `Assets/Notes/Logs/`，分析用技能 `table-nine-battlelog-analysis` | 0003 |
| 想当场看伤害对不对 / 某效果到底触没触发 | 局内「战斗日志」按钮 → 人读日志面板（每笔数值 + 来源效果名，按房间分段）；实现见 [Flow/11](Presentation/Flow/11-人读战斗日志（BattleLog）.md) | — |

## 使用建议

1. **先分层再进目录**：用 [00-架构总览](00-架构总览.md) 判断 bug 属于哪一层（Core 规则 / Content 配置 / Presentation 表演 / 桥接平台），再进对应区域文档找关键类。
2. **规则性 bug 优先查 ADR 索引**：[02](02-ADR索引与代码对照.md) 每条给了落地类名——先确认「设计上应该怎样」，再对照代码找偏差；很多「疑似 bug」其实是 ADR 里写明的有意行为（如显示值滞后、忙时丢点击、平台静默降级）。
3. **看不懂术语查词汇表**：[03](03-领域词汇表.md) 直接给了词条 → 代码位置。
4. **复现优先走 QuickTest**：主菜单 `\0`（正式镜像）与 `\1–\9`（定向挂技能/机关）；战斗内 KeypadMinus 跳关（仅 QuickTest）；F12 作弊面板（Editor/Dev）可加卡/加遗物/记录 log 快照。
5. **验证纪律**：recompile 后查 Console；同一验证动作两次无果即停（两击放弃）；日志终验看 `Assets/Notes/Logs/`。
