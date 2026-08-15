---
status: accepted
---

# 地下城环境变体由 Run 进度权威

## 决策

- **环境显示名与卡面背景**由 Run 进度权威：`RunModel.Floor` + 层内房间序号（`MapNodeProgression.ToDisplayNode(nodeIndex)`，1–8）+ 选人难度档 `RunModel.DifficultyId`（困难 = `hard`）。
- **困难档**：整层使用对应层的「血色」环境名；普通/进阶按层内前四/后四房间切换前段/后段环境。
- **卡面背景资源**：`Assets/Resources/ContentArt/Png/Other/{显示名}.png`（Resources 加载约定与 ADR-0008 一致）。
- **怪物卡 JSON `sprites.faceBackground` 不是运行时权威**：内容 JSON 可留空或仅作编辑器预览；正式怪物卡面背景在表现层 `CoreCardPresentationMapper` 经 `GetCurrentDungeonEnvironmentQuery` 覆盖后写入 `CardPresentationSnapshot.FaceBackground` → `CardFacePresentationBinder`「背景」槽。其他卡种仍走 JSON。
- **困难数值**：`MonsterFloorStatScaling` 在困难档将层档与每满 4 全局节点档的攻/血加成翻倍（+1/+2 → +2/+4）；普通/进阶公式不变。`ContentSystem.CreateDraft` 读 `RunModel.DifficultyId` 传入缩放。
- **文案**：大楼层提示与战斗信息预览的 `{floor}`（或等价字段）显示环境显示名，不是「一层/二层」或「楼层·Ⅱ」。

权威解析纯函数：`NineGrid.Core.Content.DungeonEnvironmentCatalog.Resolve(floor, room, isHard)`。运行时读：`GetCurrentDungeonEnvironmentQuery`（QF `AbstractQuery`）。

## 为什么

主题怪物卡组 id（`deck.*`）是历史不透明主键（ADR-0014），不宜再用卡组静态填背景；环境应按玩家实际推进的层与房间切换。困难档此前仅展示记录，须写入 Run 并同时影响数值与视觉。

## 后果

- `scripts/set-monster-face-backgrounds.py` 按 deck 批量写 `faceBackground` **已废弃**；怪物 JSON 断链背景应清空而非误导。
- 楼层提示 / 战前信息预览须读 Query 或 Catalog，不得硬编码罗马数字楼层。
- 读档快照 `RunSaveSnapshot.difficultyId` 与跨关 `RunInventorySnapshot` 须保留难度，避免 Bootstrap 重置后环境/数值回退。
- GroundPanel / MainBG 场景贴图切换**不在本 ADR**；仅卡面背景与文案。
- **后果（2026-08-15 扩展）**：场地框 `GroundPanel ` 与 `MainBG` tint 由同一 `GetCurrentDungeonEnvironmentQuery` 驱动（`GroundPanelResourcePath` / `MainBackgroundColorHex`）；美术源在 `Assets/Arts/.../Panels/Panels/Env/`，运行时经 Resources `ContentArt/Png/Venue/` 加载（ADR-0008 同源）。

## 相关

- [ADR-0008](0008-single-source-content-and-resources-loading.md) — ContentArt Resources 路径
- [ADR-0002](0002-card-chassis-and-face-templates.md) — 卡面槽与 Commit
- [ADR-0020](0020-board-as-interaction-surface.md) — 楼层提示
- [ADR-0021](0021-run-progression-in-core.md) — 层内 8 节点与 `RunModel`
- [ADR-0014](0014-theme-ids-are-legacy-opaque.md) — deckId 非叙事主键
