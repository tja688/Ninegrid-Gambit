---
name: quick-test-effect-channels
description: >-
  Strengthen the main-menu backslash (\) QuickTest picker into \0–\9 effect
  experience channels: blank monsters in content JSON, dynamic skill assembly
  only on QuickTest, card-face skill descriptions, no in-battle \ logic.
  Also covers near-synonym skill-gap landing (rename-identical / new-id /
  reserve-different). Use when landing monster skills or relics for playtest,
  wiring QuickTest channels, clearing monster effectAssemblies, Alpha5 Core
  flip, or when the user mentions \0–\9 / 快速测试通道 / 白板挂技能 / 近义借壳.
---

# QuickTest effect channels（`\` 效果体验通道）

革新时代约定：**正式开局与 QuickTest 从根上分离**。正式开局点主菜单「开始」走正常卡组，怪无技能（白板）。`\` 通道专供效果体验，不得污染正式接线。

实现里按键是 **`KeyCode.Backslash`（`\`）**，口语里的「/」即此键。

## When to use

- 落地一批怪物技能 / 帮助卡 / 遗物效果后，需要给人可玩验证
- 改 `QuickTestDeckCatalog` / `QuickTestRunOptions` / 动态挂技能
- 清空 `monster_*.json` 挂载、只在 `skill_*.json` 留技能权威
- 执行「近义借壳」改名 / 新建 / 储备分流（见 [reference.md](reference.md)）

## Hard rules

1. **正式开局**：点开始按钮；`monster_*.json` 的 `effectAssemblies` / `skillIds` **保持空**；正常卡组逻辑发牌；道具正常发。
2. **`\` 仅主菜单**：长按 `\` → 选 `0–9` → 开局。局内**无** `\` 逻辑（含调速）；要换通道就退回主菜单重开。
3. **`\0`–`\9` 全是可挂技能通道**（含 `\0`）。通道预设表决定挂哪些 `skillId`；与旧「钉死军团牌组」解耦。
4. **动态装配只发生在 QuickTest**：开局后把本通道 `skillIds` 挂到场上已发怪物（不够则再发宿主）；**卡面描述必须写成技能效果描述**（玩家能读到在测什么）。
5. **独立效果可混挂同一通道**；自成体系、依赖配合的效果**单开通道**。
6. **同义改名原则**（ verbatim ）：能力一模一样就改为新的，无论是实际代码还是表述/名字，最终只留新的技能和完全不一样的老技能（作为后续设计储备，可能被归档）。
7. 内容日常改 **`Assets/Arts/ContentVisual/`**，保存/同步到 StreamingAssets；勿只改 Streaming。

## Agent workflow（落地一批效果后）

```
Task Progress:
- [ ] 1. 按内容权威落地 skill_*.json + effect_templates（Arts 双写）
- [ ] 2. 向用户推荐本批要测的通道划分（优先新机制/新能力；老且稳的可略）
- [ ] 3. 用户点头后写入 \0–\9 预设表（缺通道逻辑就补）
- [ ] 4. 卡面描述 = 技能效果全文；道具仍正常发
- [ ] 5. 说明如何复现：主菜单 \ → 码 → 观察点（含 Alpha5 Core 翻面若相关）
```

### 推荐通道时怎么说

- 先列本批**新机制**技能，说明为何要验
- 提案：`\N` = 技能列表（混挂或单开）+ 一句话观察法
- 等用户点头再改预设表；不要擅自占满 0–9

### 扩展点（实现时改这些）

| 层级 | 路径 | 做什么 |
|------|------|--------|
| 码表 | `Assets/Scripts/NineGrid.Presentation/Flow/QuickTestDeckCatalog.cs` | `MaxPickerCode=9`；码→预设（技能列表，可选 deck） |
| 选项 | `.../Flow/QuickTestRunOptions.cs` | 增加本局要挂的 `skillIds`（或预设 id） |
| 编排 | `.../Systems/GameFlowShellSystem.cs` | `TryBeginQuickTestFromPickerCode` 写入 options |
| 挂载钩子 | `.../Flow/GameFlow/GameFlowOrchestrator.cs` | 仿 `ApplyQuickTestAvatarCheatsIfNeeded`，QuickTest 专用挂技能 + 刷卡面描述 |
| 作弊 API | `.../Flow/BattleSession/BattleSessionCheat.cs` | Dev 向场上怪动态挂 `skillId` |
| 输入 | `NineGrid.DevTest/Flow/QuickTestEntryInputHandler.cs` | 保持只透传 code；**删除**局内 `\` 调速（`InBattleDebugQuickModeInputHandler`） |
| 翻面测 | `NineGrid.DevTest/Cards/GroundFieldManagerDevKeys.cs` | Alpha4=表现 POC；**Alpha5=鼠标指向卡 Core `Flip`**（触发 `OnFlip`） |

HP99 / ATK5 等 QuickTest 作弊可保留；与技能通道正交。

## 近义借壳（本节技能缺口）

细则与两批次清单见 [reference.md](reference.md) 与  
`Assets/Notes/近义借壳技能落地-批次计划-2026-07-30.md`。

摘要：

| 处置 | 例子 |
|------|------|
| 同义 → 改成字典新 id | `absorb_random`→`absorb`，`invoke`→`call_melee6`，`devotion`→`offer_fire`，`thief_claims`→`link_prep` |
| 异义旧技 → 改名腾位或留储备 | 旧献祭→`purge_followers`；`absorb_bone`/`fight_me` 等保留 |
| 新语义 → 新模板 | `sacrifice` 献身、`leap_kill`、`steal`（邻接帮助卡**随机移除一张**） |
| 缺原子 → 不进本批 | `holy_duel` 神圣决斗（交战记忆） |

新版效果文字权威：Obsidian `九宫格登神-怪物配置/02-技能字典.md`。

## Anti-patterns

- 把 QuickTest 挂载写回 `monster_*.json` 的 `effectAssemblies`
- 局内再做 `\` 换通道 / 调速
- 正式开局路径调用动态装配
- 卡面空白或仍显示旧主题名，却在测新技能
- 同义旧 id 继续挂在新怪配表上（勿直接挂陷阱表）
