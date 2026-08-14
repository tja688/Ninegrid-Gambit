---
status: accepted
---

# 装饰运动层：L1 场地卡生命层 + 整像素抖屏

> **修订（2026-08-14）**：悬浮飘动改为**常驻非阻塞**——不再受 `MainlineBusy` / `IsFieldBusy` 门禁暂停。手牌（`HandCardLifeFx`）与场地卡（`BoardCardLifeFx`）在主线开演、场地忙碌期间照常飘动，不占主线、不进编排、纯视觉；唯一特例是手牌中正被鼠标指向（hover 弹出）的卡，仍随 hover 让位归零。逐卡「权威运动让位」（第 2 条）不变量不变。

## 决策

**"让画面活起来"的动效一律落在与权威运动正交的装饰层上，确立四条不变量：**

1. **L1 `_L1_BoardFrame` 是装饰运动层，单一写者。** 场地卡的悬浮飘动 / 冲击波 / 落位沉降只写 L1 局部位移与微旋，权威写者为 `Cards/Vfx/BoardCardLifeFx`（+ 手牌拖拽期的惯性倾斜，见第 3 条）。L0（占格锚点）、L2（格位收敛）、L3（交战位移）、L4（视觉缩放）语义不变，装饰层**不碰**它们。L1 的权威闲置位姿是 `localPosition=(0,0,0)` + `identity` + `localScale=one`。
2. **装饰位移在权威运动接手时立刻让位，不缓动。** 每卡逐帧自检"是否正被别的系统搬动"——`L2`/`L3` 局部位移非零、L2/L3 收敛 driver 激活、L0 偏离格锚点、`DisplayMode != GroundCardMode`——任一成立即**硬归零** L1（不淡出）。这既是用户要的「战斗与受击回位」，也保证 `SlotFrameConvergence.WorldToSlotLocal`（经 L1 做世界→L2 换算）不会把飘动量算进收敛目标。飘动整体开合**不受 `MainlineBusy` / `IsFieldBusy` 门禁暂停**（修订 2026-08-14）——常驻非阻塞：即使主线开演、场地忙碌，未被搬动的卡照常飘动；只有本卡正被权威运动接手（含手牌 hover 弹出）才让位。
3. **抖屏独占主相机 `localPosition`，且只走整像素。** `Flow/Presentation/ScreenImpact` 以 trauma 模型累积/衰减，偏移量按参考分辨率（`540 / (2 × orthographicSize)` = 32 PPU）量化到整像素，**不旋转相机**——像素栅格恒轴对齐，PixelPerfectCamera 下不糊边、不抖亚像素。起震瞬间重采基准位、trauma 归零即精确复位，不与任何镜头构图系统争位。
4. **签名冲击是登记制，默认静默。** `Flow/Presentation/BoardImpactSignatures` 按**效果容器 defId**（事件 `SourceDefId`）登记「触发瞬间就是可见爆点」的效果（爆弹 `help.bomb`、手雷、闪光弹等）才给抖屏 + 冲击波档位；表内没有的来源不抖不推。走打击表演（ADR-0050）的伤害类效果**不得登记**——它们的反馈由命中帧冲刷出的 `ShowDamage` 驱动抖屏，登记在此会比可见命中提前一拍。

## 为什么

玩家反馈「战斗和游玩过程画面死板」。缺的不是特效素材（VFX 库已相当完整），而是**恒定的低频生命感**与**冲击的物理后果**：棋盘上九张卡在等待输入时完全静止，命中与爆炸除了飘字与素材外没有任何重量反馈。

**为什么用 L1 而不是新起一层或复用 L4**：`CardTransformTower` 已把 L1 定义为「整盘运动层」且此前无人使用，正是整盘装饰运动的语义位置；新起层会破坏 ADR-0002 的固定塔形，复用 L4 会与卡面缩放脉冲（hop/hover/punch）互踩。

**为什么装饰位移必须硬归零而不是淡出**：L2 的父级就是 L1，收敛目标经 `L1.InverseTransformPoint` 换算。若在收敛期间继续飘动，卡的落点会跟着飘动量漂移；硬归零把误差窗压到「权威运动开始的那一帧」，且终点由 `SnapHome` / 终态守卫精确校正。淡出反而把误差窗拉长到淡出时长。

**为什么抖屏要整像素、且不旋转**：本项目是 32 PPU 像素画（960×540 参考、pixelRatio 2、sprite 侧自有几何 snap）。相机做亚像素平移或任何旋转都会让全屏栅格与世界网格错位，代价是整屏边缘蠕动——比"不抖"更糟。整像素平移则完全无损。

**为什么签名表默认静默**：TriggerEffect 在一局里极密（机关每步都触发）。若按前缀给通用抖屏，反馈会退化成背景噪声，反而更"廉价"。宁缺毋滥，只给真爆点。

## 考虑过的替代

- **用 Feel/MMFeedbacks 的 `MMCameraShakeEvent` 做抖屏**：否决——其抖动是连续浮点位移，对整像素栅格不友好；且需要在相机上装额外 shaker 组件与场景序列化，比 30 行 trauma 模型的黑箱面积更大。
- **飘动写 L4（视觉叶子）以彻底避开收敛换算**：否决——L4 承载 hop/hover/punch 缩放与 FacePivot 翻牌，叠位移会与既有卡面动效互踩；且 L1 的语义本就是这件事。
- **飘动做成 DOTween 循环 tween**：否决——每卡常驻 tween 需要在每次交战/移动时 kill/rebuild，与 `CardDeckTween.KillMotion` 家族的清理纪律纠缠；纯函数正弦叠加无状态可随时让位，且能被权重连续开合。
- **抖屏挂在 `FieldBattlePresentationExecutor.ApplyHitFrameVisuals`**：否决（作为唯一入口）——那里只覆盖攻击/反击命中帧，效果伤害、齐射、道具伤害都漏；`DamageFloaterBeatHandler` 才是所有可见伤害的同一条缝（且已是飘字/受击 VFX/音频的权威发射点，天然对齐 ADR-0048 两相 Impact 的命中帧冲刷）。
- **冲击波推真实占格（把卡挪到别的格）**：否决——占格权威在 Core `BoardModel`，表现层不得越权；且用户要的是"炸开一圈"的观感，不是规则位移。

## 后果

- **行为变化**：①九宫格上的卡在主线空闲时各自低频飘动（0.30–0.52 Hz 错频、幅度约 2–3 像素、微旋 ≤0.4°），悬停时幅度 ×1.45；②**飘动常驻非阻塞（修订 2026-08-14）**——主线开演 / 场地忙不再停飘，交战与受击卡仍由逐卡让位即刻回位；③任何可见伤害按「扣血+破甲」量抖屏（1 点约 1 像素、16 点以上约 4 像素，0.42s 线性衰减），受击卡另加一次颤动（正被打击表演搬动的卡自动让位，不与击退叠加）；④爆弹一类登记效果在触发帧从棋盘中心向四周推开全场卡（按距离衰减 + 波前延迟）并抖屏；⑤被别的系统搬完重新交回 L1 的卡（发牌、跳跃、旋转、换位通吃）落地时一次向下沉降；⑥手牌拖拽按横向速度反向倾斜（≤9°）、拾起有一次挤压。
- **可控性**：`ScreenImpact.Enabled` / `IntensityScale`、`BoardCardLifeFx.Enabled` / `DriftScale` / `ImpulseScale` 为静态可写门面，支持 live-lab 运行时调参与整体关闭；关闭即精确复位，不留残余偏移。两者的 Runner 与旋钮在 `SubsystemRegistration` 复位（Editor 免域重载进 Play 也干净）；飘动 Runner 另在 `AfterSceneLoad` 主动起（常驻表现不能等第一次冲击才建），抖屏 Runner 保持按需建。
- **不进主线**：两者都不参与 Batch-ack、不认领指令、不改规则状态、不进 `VfxBindingCatalog`（不是素材型 VFX，与 `CardEdgeDustFx` / `BoardRangeGlowFx` 同类程序化装饰）。
- **命中框与坐标不受影响**：L0 不动，`GroundCardHitProxy` 的锚点对齐门禁、`ResolveCardWorldPosition`（飘字/VFX 坐标）与拖拽落格解析全部读 L0，飘动只改可见位置（≤3 像素）。
- **新增内容契约**：给效果加"触发即爆点"的物理反馈 = 往 `BoardImpactSignatures` 加一行容器 defId + 两个档位；不要在业务代码里散调 `ScreenImpact` / `BoardCardLifeFx`。
- **回归**：无自动化测试（见 [`tests.md`](../code-map/tests.md) 冲刺期约定）；`recompile` 后 Console 无新增 Error/Exception/Assert，其余为手动 Play 观感验收。

## 相关

- [ADR-0002](0002-card-chassis-and-face-templates.md) — 卡牌底盘与固定变换塔（L0–L4 语义来源）
- [ADR-0024](0024-board-placement-and-prefab-authored-size.md) — 尺寸权威在预制体、动效为相对基准且可还原
- [ADR-0048](0048-two-phase-impact-and-trigger-chain-dedup.md) — 两相 Impact 与触发链级去重（签名冲击的发射时机与去重）
- [ADR-0050](0050-effect-strike-unified-damage-presentation.md) — 效果打击统一表演（为何伤害类效果不进签名表）
- [ADR-0023](0023-slot-hit-frame-and-claim.md) — 格位命中权威（L1 装饰不影响命中与认领）
