# 策划试玩反馈三方比对审计报告

- 日期：2026-08-08
- 范围：仅探查，未改动任何文件
- 方法：设计案（`Assets/Docs/九宫格登神`）↔ 权威文档（`CONTEXT.md` / `docs/adr/`）↔ 代码实施与内容数据（`NineGrid.Core` / `NineGrid.Content` / `Assets/Arts/ContentVisual/cards/*.json`）三方比对
- 数据快照：工作区当前 HEAD（`e5428ff06`）下的 `Assets/Arts/ContentVisual/cards/`（与 `Assets/StreamingAssets/ContentVisual/cards/` 逐字节一致，抽查 7 张序列 1 卡 MD5 全等）

---

## 结论摘要

| 条目 | 判定类型 | 一句话结论 |
|------|----------|------------|
| 1. 近战攻击倒计时统一改 5 | **内容变更请求，落地点是 Core 机制代码（非内容 JSON）** | 当前所有近战模式阈值硬编码为 3（`AttackPatternRules.Frequency`），JSON `stats.action` 被代码覆盖、非权威；改 5 属机制单点变更，且与 ADR-0011/0013 现行决策（阈值 3、梯度推算）冲突，需先修订 ADR 与钉死频率的测试 |
| 2. 序列 1 怪物卡全部 1 攻 4 血无护甲 | **内容配置问题（反馈事实部分不符）** | 「全是 1 攻 4 血」属实；「都没有护甲」**不属实**——七套序列 1 中 5 套有甲（2/2/2/2/4），仅 2 套 0 甲；当前数据与设计案 `怪物卡.md` 逐值一致，非生成/校验脚本造成 |

---

## 条目 1：「近战攻击的倒计时全部修改为 5」

### 判定

内容变更请求（策划新要求）→ **执行点是机制代码而非内容 JSON**。当前配置值与目标（5）的差距：**所有近战模式卡当前阈值为 3**，改 5 即全部 +2。该改动会推翻 ADR-0011 决策表中的频率值并破坏 ADR-0013 的梯度推算，属需要先走 ADR 修订的机制变更。

### 设计案原文

`Assets/Docs/九宫格登神` 正文未出现具体倒计时阈值数字（02-机制 各文档无「倒计时=N」表述）。阈值的唯一权威来自 ADR-0011 决策表（其来源是初版设计文本的「近战 3 对 远程 5」讨论）：

| 模式 | 位置条件 | 阈值（ADR-0011） |
|------|----------|------------------|
| 普通近战 | 正交相邻 | **3** |
| 斜角近战 | 对角相邻 | **3** |
| 全向近战 | 八向相邻 | **3** |
| 普通远程 | 无条件 | 5 |
| 无 | —— | 不开火 |

（`docs/adr/0011-monster-attack-pattern-intrinsic.md:15-21`）

「倒计时」措辞辨析：卡面「行动计数」槽显示的是**剩余倒计时**（进场置为阈值、每次互动 −1），不是阈值本身（CONTEXT「行动倒计时」「开火窗口」；`docs/adr/0013-action-countdown-unified.md`）。「全部修改为 5」的等效语义是「阈值改 5、进场初始显示 5」，本文按此理解。

### 权威决策

- ADR-0011：频率表（上表）为决策的一部分；「可被效果影响的只有行动倒计时」（`0011:24`），攻击模式本身不可被运行时改写。
- ADR-0013 窗口模型梯度推算：阈值 3 下期望出手间隔「全向 3 拍 > 远程 5 拍 > 单向近战 6 拍」，梯度干净（`0013:24-33`）。**若近战阈值改 5**：全向近战期望 = 远程期望 = 5 拍，而全向还有八向位置条件（不满足即重置倒计时），等于被普通远程**严格支配**，「位置限制越松、频率越慢」的设计意图直接破坏；单向近战（正交/对角）期望 10 拍，与远程 5 拍差距拉大到一倍。
- CONTEXT「攻击模式」「行动倒计时」「开火窗口」词汇（行为不变量）。

### 代码实施（关键证据：阈值不在内容里，在 Core）

1. **频率表硬编码**：`Assets/Scripts/NineGrid.Foundation/NineGrid.Core/Domain/AttackPattern.cs:31-44`
   `AttackPatternRules.Frequency()`：三个近战模式 `return 3;`，`Ranged return 5;`，默认 `0`。这是运行时倒计时初始值的唯一真相源。
2. **JSON `stats.action` 非权威，被投影覆盖**：
   - `NineGrid.Content/Catalog/ContentJsonCatalogProjector.cs:186-197` 解析 `attackPattern` 后调 `WithAttackPattern`；
   - `NineGrid.Core/Content/ContentDefinitions.cs:255-264`：`Stats.Action = AttackPatternRules.Frequency(attackPattern)` —— **作者在 JSON 里填的 `stats.action` 被代码频率表覆盖**；
   - `NineGrid.Core/Systems/ContentSystem.cs:139-151` `CreateDraft` 把 `definition.Stats.Action` 灌进 `ActionFrequency`；
   - `NineGrid.Core/Domain/CardDraft.cs:92-99`：`ActionFrequency > 0 ? ActionFrequency : Frequency(...)`，初始 `AttackPatternCountdown = frequency`。
   即：即使某张卡 JSON 写 `stats.action: 5`，运行时仍按 3 走。**只改 JSON 无法实现「统一改 5」**。
3. **校验不查一致性**：`ContentSystem.cs:379-394` `ValidateMonsterAttackPatterns` 只报模式缺失/非法（Unspecified），不校验 `stats.action` 与频率表是否一致。
4. **钉死当前频率的测试**（改机制必被波及）：
   - `NineGrid.Core.Tests/ContentCatalogValidationTests.cs:79`：`Assert.AreEqual(3, beggar.Stats.Action)`
   - `NineGrid.Core.Tests/AttackPatternDataPlaneTests.cs:114/119`：melee→3、ranged→5
5. 倒计时消费端（行为不变量，不受阈值影响）：`NineGrid.Core/Systems/PhaseSystem.cs:1639-1660, 1720-1774`（报名扣减、资格复核、重置为 `Frequency`）；`EffectAtomLibrary.cs:3322`（加减速效果）。

### 内容数据现状（`Assets/Arts/ContentVisual/cards/*.json`，64 张怪全量扫描）

- JSON `stats.action` 分布：`3`×49、`5`×9、`0`×6 —— 恰好镜像代码频率表（近战 3 / 远程 5 / 无 0）。
- 近战模式卡：全怪 **49/64** 张（普通近战 38、斜角近战 3、全向近战 8）；七套正式卡组（ADR-0029 `ThemeDeckStableMapping` 35 张）中 **23/35** 张为近战模式（普通近战 21、全向 1、斜角 1），当前阈值全部为 3。
- 远程 9 张（含正式 6 张）、无 6 张（含正式 6 张）不受「近战改 5」影响。

### 影响面与归类

- **归类**：内容变更请求；落地为**机制变更**（Core `AttackPatternRules.Frequency` 三处 3→5），非内容配置变更。
- **与现行决策冲突**：ADR-0011 频率表、ADR-0013 梯度推算均会被推翻；需修订两篇 ADR（或新 ADR）并同步更新上述两个钉死测试；`docs/code-map/tests.md` 相关条目同步。
- 若策划本意是「每张怪可单独配阈值」，则需把 `stats.action` 变为权威（去掉 `WithAttackPattern` 覆盖），改造面更大且需新设计（阈值是否仍受 ADR-0011「必填无默认」约束）。
- 平衡提示：改 5 后全向近战与普通远程同为 5 拍，全向被远程严格支配；单向近战（10 拍期望）与远程（5 拍）差距拉大一倍，需实测校准（ADR-0013 后果本身即预留「以实测为准」）。

---

## 条目 2：「所有序列为 1 的怪物卡都没有护甲了，全是 1 攻击 4 血量」

### 判定

**内容配置问题（反馈事实部分不符）**：「全是 1 攻击 4 血量」与现状、设计案**一致**；「都没有护甲了」与现状、设计案**均不符**——七张序列 1 卡中 5 张有护甲（2/2/2/2/4），仅 2 张 0 甲。当前数据与设计案 `怪物卡.md` 逐值一致，**没有**任何批量生成/校验逻辑把序列 1 写成固定 1/4/0。

### 设计案原文

`Assets/Docs/九宫格登神/04-敌人侧卡牌信息/怪物卡.md:9-75` 七套机制表，序列 1 各行：

| 设计案名称 | 血量 | 攻击 | 护甲 | 技能 | 序列 |
| :-- | :-- | :-- | :-- | :-- | --- |
| 近战1（基础） | 4 | 1 | **2** | 普通近战 | 1 |
| 近战2（旋转） | 4 | 1 | **2** | 斜角近战 | 1 |
| 近战3（召唤） | 4 | 1 | **0** | 普通近战+死亡召唤 | 1 |
| 近战4（翻面） | 4 | 1 | **2** | 潜伏近战+跳杀 | 1 |
| 近战5（链接） | 4 | 1 | **2** | 链接近战 | 1 |
| 近战6（烈焰） | 4 | 1 | **0** | 普通近战+献火 | 1 |
| 近战7（决斗） | 4 | 1 | **4** | 神圣决斗 | 1 |

即设计案序列 1 为「统一 4 血 1 攻，护甲 2/2/0/2/2/0/4」，**并非统一无甲**。

### 内容数据实测（七套序列 1 槽位，`ThemeDeckStableMapping` + 各卡 JSON）

deck 与设计案表名的对应由各 deck JSON 的 `displayName` 证实（基础/旋转/召唤/翻面/链接/烈焰/决斗）：

| 卡组（deckId） | 设计案表 | 序列 1 卡 | JSON 实测（hp/atk/armor/action） | 攻击模式 |
|------|------|------|------|------|
| deck.dragon | 基础 | monster.melee_3（小小莱姆） | 4/1/**2**/3 | 普通近战 |
| deck.orc_legion | 旋转 | monster.big_stone（小蜜蜂） | 4/1/**2**/3 | 斜角近战 |
| deck.insect | 召唤 | monster.wandering_child（巨斧骷髅） | 4/1/**0**/3 | 普通近战 |
| deck.skeleton_legion | 翻面 | monster.smart_orc（紫蝎） | 4/1/**2**/3 | 普通近战 |
| deck.smallanimal | 链接 | monster.ringleader（巨棒哥布） | 4/1/**2**/3 | 普通近战 |
| deck.stone_legion | 烈焰 | monster.dragon_cult_leader（蓝焰） | 4/1/**0**/3 | 普通近战 |
| deck.void | 决斗 | monster.bone_club_skeleton（拳击决斗者） | 4/1/**4**/0 | 无 |

- 护甲序列 `2,2,0,2,2,0,4` 与设计案 `2,2,0,2,2,0,4` **完全一致**。
- 攻击模式差异说明：设计案「潜伏近战 / 链接近战 / 神圣决斗」不是 ADR-0011 五种模式之内的枚举值，代码按 ADR-0011 归约为 普通近战 / 无，属既定迁移（`0011` 后果：默认普通近战，主题覆盖），不是本次条目问题。
- `Assets/StreamingAssets/ContentVisual/cards/` 同 7 张 MD5 与 Arts 侧一致（ADR-0029 窄契约「双侧字节一致」满足）。

### 是否存在「把序列 1 写成固定 1/4/0」的生成/校验逻辑

- **无**。`NineGrid.Content.Editor` 内只有表现层编辑/校验工具（`CardPresentationEditorWindow` / `CardPresentationEditorSession` / `MonsterLoadoutPresentationValidator` 等），无批量写数值逻辑。
- 内容校验（`ContentSystem.ValidateMonsterAttackPatterns`）只查攻击模式缺失；`ThemeDeckMappingVerifier` / `ThemeDeckFormalReadiness`（ADR-0029）只查槽位唯一、无缺槽、boss 标志、Reserve 状态，**不校验数值/护甲**。
- git 历史佐证数值为人工维护：2026-08-03 `297e712ca`（合并）把多张序列 1 护甲归一化到设计案值——`melee_3` 0→2、`big_stone` 5→2、`smart_orc` 0→2、`ringleader` 4→2；`wandering_child` / `dragon_cult_leader` 保持 0，`bone_club_skeleton` 保持 4（即设计案值）。此后（`a4a7b9f92` 改动卡牌配置）仅动过动画路径，未动 stats。

### 表现层是否有「有甲不显示」的断层

- 怪物卡面模板 `Assets/Prefabs/怪物卡标准模板.prefab` 含「护甲」「护甲数值」节点（`\u62A4\u7532` 行 2279 / `\u62A4\u7532\u6570\u503C` 行 2555）。
- `CardFacePresentationBinder.cs:451-457` 怪物分支绑定 Attack/Armor/Hp；映射取当前护甲 `StatArmorUtility.GetCurrentArmor`（`CoreCardPresentationMapper.cs:90`；`CardDraft.cs:60-64` 有甲则基础甲=当前甲）。**未见有甲不显示的断层**。

### 反馈与现状不符的可能解释（供后续，不属本次判定）

1. 序列 1 在前 1-2 个地图节点占比极高（`07-流程/卡组生成流程.md:11-18`：节点 1 全为序列 1、节点 2 为 6 序列 1 + 4 序列 2）。若试玩层绑定到**召唤（0 甲）或烈焰（0 甲）**两套之一，玩家看到的序列 1 全是「1 攻 4 血无甲」，易概括成「全部」。
2. 反馈措辞宽松（「没有护甲了」可能意指「护甲普遍很低/没存在感」——最高才 4 且每节点重置，CONTEXT「当前护甲」语义）。
3. 若本条是**新平衡要求**（「序列 1 统一无甲」），则与设计案（5 套有甲）冲突，需策划确认后再改内容；改的话只涉及 5 张 JSON 的 `stats.armor`，无校验/测试钉子阻碍（无测试断言序列 1 护甲）。

---

## 附：影响面速查

- 全怪 64 张：近战 49（action=3）、远程 9（action=5）、无 6（action=0）。
- 七套正式卡组 35 张：普通近战 21、普通远程 6、全向近战 1、斜角近战 1、无 6 —— 「近战改 5」波及 23 张。
- 序列 1 七张全部 4 血 1 攻；护甲 2/2/0/2/2/0/4，与设计案一致。
- 关键文件清单：
  - `docs/adr/0011-monster-attack-pattern-intrinsic.md`、`docs/adr/0013-action-countdown-unified.md`、`docs/adr/0029-content-guardrail-stable-theme-deck-mapping.md`
  - `Assets/Scripts/NineGrid.Foundation/NineGrid.Core/Domain/AttackPattern.cs`（频率表）
  - `Assets/Scripts/NineGrid.Foundation/NineGrid.Core/Content/ContentDefinitions.cs:255-264`（stats.action 覆盖点）
  - `Assets/Scripts/NineGrid.Foundation/NineGrid.Content/Catalog/ContentJsonCatalogProjector.cs:186-197`
  - `Assets/Scripts/NineGrid.Foundation/NineGrid.Content/Catalog/ThemeDeckStableMapping.cs:20-69`
  - `Assets/Arts/ContentVisual/cards/monster_*.json`（64 张，含七套序列 1）
  - `Assets/Arts/ContentVisual/tables/monster_decks.json`（七套 deck_kind=Unknown 正式可选）
  - `Assets/Docs/九宫格登神/04-敌人侧卡牌信息/怪物卡.md`、`07-流程/卡组生成流程.md`
