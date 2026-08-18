---
status: accepted
---

# 地下城环境变体由 Run 进度权威

## 决策

- **环境显示名与卡面背景**由 Run 进度权威：`RunModel.Floor` + 层内房间序号（`MapNodeProgression.ToDisplayNode(nodeIndex)`，1–8）+ 选人难度档 `RunModel.DifficultyId`（困难 = `hard`）。
- **困难档**：整层使用对应层的「血色」环境名；普通/进阶按层内前四/后四房间切换前段/后段环境。
- **卡面背景资源**：表字段 `faceBackground`（默认 `Assets/Resources/ContentArt/Png/Other/{显示名}.png`，可在表现层配置器「地下城虚构」里换图）。
- **怪物卡 JSON `sprites.faceBackground` 不是运行时权威**：内容 JSON 可留空或仅作编辑器预览；正式怪物卡面背景在表现层 `CoreCardPresentationMapper` 经 `GetCurrentDungeonEnvironmentQuery` 覆盖后写入 `CardPresentationSnapshot.FaceBackground` → `CardFacePresentationBinder`「背景」槽。其他卡种仍走 JSON。
- **难度数值**：`MonsterFloorStatScaling` 完整落地三档成长：
  - **简单档（`normal`）**：频率减半，过层不加档，仅每层中段（节点 4 起）+1 档（+1 攻 / +2 血）；
  - **中等档（`advanced`）**：标准双轨，每层中段 +1 档且每打完一层 +2 档（+1 攻 / +2 血 每档）；
  - **困难档（`hard`）**：档位同中等档，但攻血加成翻倍为每档 +2 攻 / +4 血。
  `ContentSystem.CreateDraft` 直接读取 `RunModel.DifficultyId` 传入缩放。
- **文案**：大楼层提示与战斗信息预览的 `{floor}`（或等价字段）显示环境显示名，不是「一层/二层」或「楼层·Ⅱ」。

权威解析：`DungeonEnvironmentCatalog.Resolve`。作者真源为 `Arts/ContentVisual/tables/dungeon_environments.json`（StreamingAssets 双写）；缺表时回退烘焙默认。运行时读：`GetCurrentDungeonEnvironmentQuery`。

## 为什么

主题怪物卡组 id（`deck.*`）是历史不透明主键（ADR-0014），不宜再用卡组静态填背景；环境应按玩家实际推进的层与房间切换。困难档此前仅展示记录，须写入 Run 并同时影响数值与视觉。

## 后果

- `scripts/set-monster-face-backgrounds.py` 按 deck 批量写 `faceBackground` **已废弃**；怪物 JSON 断链背景应清空而非误导。
- 楼层提示 / 战前信息预览须读 Query 或 Catalog，不得硬编码罗马数字楼层。
- 读档快照 `RunSaveSnapshot.difficultyId` 与跨关 `RunInventorySnapshot` 须保留难度，避免 Bootstrap 重置后环境/数值回退。
- GroundPanel / GroundAnchors 格面由同一 Query 驱动（表字段 `groundPanel` / `slotHex`）；作者在 `NineGrid/表现层配置` 侧栏 **地下城虚构** 换图换色，保存双写 JSON。**暂禁**：场地框与格面运行时接线已断开，场景静态配色为准，见 [ADR-0055](0055-venue-board-visuals-static-fallback.md)。
- **MainBG 滑动底** 同 Query 驱动（表字段 `mainBackground` / `mainBackgroundHex`），局内按层切换，见 [ADR-0058](0058-mainbg-scroll-by-dungeon-layer.md)。
- 缺表或未命中行时 `DungeonEnvironmentCatalog` 回退烘焙默认，不崩。

## 相关

- [ADR-0008](0008-single-source-content-and-resources-loading.md) — ContentArt Resources 路径
- [ADR-0002](0002-card-chassis-and-face-templates.md) — 卡面槽与 Commit
- [ADR-0020](0020-board-as-interaction-surface.md) — 楼层提示
- [ADR-0021](0021-run-progression-in-core.md) — 层内 8 节点与 `RunModel`
- [ADR-0014](0014-theme-ids-are-legacy-opaque.md) — deckId 非叙事主键
- [ADR-0055](0055-venue-board-visuals-static-fallback.md) — 场地框/格面静态回退
- [ADR-0058](0058-mainbg-scroll-by-dungeon-layer.md) — 局内 MainBG 滑动底按层切换
