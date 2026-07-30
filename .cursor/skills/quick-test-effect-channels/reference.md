# 近义借壳 · 处置决议与批次

对齐来源：Obsidian `技能缺口.md` 第三节 + 本仓库 Grill 共识（2026-07-30）。

## 原则

- 新版语义权威：`02-技能字典.md`
- 能力一模一样 → 改成新 id/名字；只留新技能 + 完全不一样的老技能（储备）
- 不关心「当前挂给谁」；怪物 JSON 清挂载后由 QuickTest 通道动态挂
- 神圣决斗本轮不做，进缺原子后续计划

## 第三节逐项

| 新版 | 字典 id | 处置 | 易误判旧壳（勿直接当新版挂） |
|------|---------|------|------------------------------|
| 吸收 | `skill.absorb` | 同义改名：`skill.absorb_random` → `skill.absorb`（模板已逐字等同） | `absorb_bone` / `absorb_stone` 留储备 |
| 献身 | `skill.sacrifice` | **新建**模板：`OnSelfRemoved` → 其他怪攻+1 | 旧 `devotion` 实际是献火，不是献身 |
| 神圣决斗 | `skill.holy_duel` | **延后**：缺跨战记忆原子 | `fight_me` 留储备 |
| 跳杀 | `skill.leap_kill` | **新建**：`OnFlip` + `IsFaceUp` + 正交邻玩家 → 攻击力伤害 | `relentless_chase` 留储备 |
| 盗取 | `skill.steal` | **新建**：`OnFlip` + `IsFaceUp` → 邻接帮助卡**随机移除一张** | `thief_claims` 实为链接准备，改名见下 |
| 呼唤 | `skill.call_melee6` | 同义改名：`skill.invoke` → `skill.call_melee6` | `call_followers` 留储备 |
| 献火 | `skill.offer_fire` | 同义改名：`skill.devotion` → `skill.offer_fire` | 勿挂 `love_fire` / 旧 `sacrifice` |

### 连带改名

| 现状 | → | 说明 |
|------|---|------|
| `skill.thief_claims` | `skill.link_prep` | 已是链接准备语义（OnInteract/5 移邻帮助卡） |
| 旧 `skill.sacrifice`（献祭=清龙信徒） | `skill.purge_followers` | 腾出 `skill.sacrifice` 给新版献身；displayName 仍「献祭」 |

## 翻面

- Core：`FaceUp` / `OnFlip` / `IsFaceUp` / `Flip` / `ActiveWhileFaceDown` 已落地（ADR-0016）
- 生产模板尚未接线；跳杀/盗取 = **内容接线**，不是再做 Core
- Dev：Alpha4=表现全体翻；**Alpha5=鼠标指向卡走 Core Flip**（测主动翻开规则 + OnFlip 技能）

## 批次切分（实施）

### 批次 A — 改名 + 非翻面

1. 清空全部 `monster_*.json` 的 `effectAssemblies` / `skillIds`（Arts+Streaming）
2. 改名包：`absorb_random`→`absorb`，`invoke`→`call_melee6`，`devotion`→`offer_fire`，`thief_claims`→`link_prep`，旧献祭→`purge_followers`
3. 新建 `skill.sacrifice`（献身）
4. 引用替换（模板 id、测试、夹具）；同步 Arts↔Streaming
5. 神圣决斗：只改文档进「缺原子」，不写模板
6. 搭 `\0`–`\9` 通道最小闭环 + 删局内 `\` 调速；本批结束后**推荐**通道划分，用户点头再写入预设

### 批次 B — 翻面技能 + 测试基建补全

1. 新建 `skill.leap_kill`、`skill.steal`
2. Alpha5 Core 指向翻面
3. 按 skill 流程推荐翻面通道（可与帮助卡联动发牌说明）
4. EditMode：OnFlip 模板解析 + 装配冒烟

## 缺原子（神圣决斗）

目标：与本卡战斗后，再与其他怪战斗 → 对玩家 2 伤。

现有 `OnBattle` / `EngagedEnemyUid` / `CardCounter`（只读）不够：**缺跨战持久记忆写入**（如 `SetCardCounter` 或等价决斗标记）。另票做小 Core 后再建 `skill.holy_duel`。
