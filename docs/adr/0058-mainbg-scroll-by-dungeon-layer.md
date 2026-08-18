---
status: accepted
---

# 局内 MainBG 滑动底按层切换

## 决策

- 局内 `Panels/MainBG` 的 **Tiled 滑动贴图 + 色罩** 随 Run 层与困难档切换，读侧仍是 `GetCurrentDungeonEnvironmentQuery` / `DungeonEnvironmentCatalog.Resolve`。
- 映射按 **层**（不是层内前/后四房间变体）：
  - 密林（1 层）：`texture185.png`，色罩 `#b09173`
  - 岩层（2 层）：`texture29.png`，色罩 `#b09173`
  - 溶洞（3 层）：`texture90.png`，色罩 `#b09173`
  - 困难血色（整层）：`texture129.png`，色罩 `#7a0305`
- 作者表字段 `mainBackground`（贴图路径）+ 既有 `mainBackgroundHex`（色罩）。主菜单 `MainBG` 与 `菜单背景` 保持场景作者贴图，不跟层走。
- 贴图必须 **Wrap = Repeat**、无 sprite extrude，走现有 `_ScrollUv` 无缝滚动；Clamp 会退化成 Transform 漂移并滑出画面。
- [ADR-0055](0055-venue-board-visuals-static-fallback.md) 对 **GroundPanel / GroundAnchors 格面** 的静态回退仍有效；本决策只恢复 MainBG 滑动底的动态接线。

## 为什么

单一暖褐滑动底无法区分三层与血色；新贴图导入默认 Clamp，手动换图后会慢慢滑出屏幕，必须与现有 Repeat UV 滚动对齐。

## 后果

- `VenueEnvironmentPresenter.ApplyVenueBoardVisualTheming` 仍为 `false`（场地框/格面不换）。
- `MainMenuBackdropPresenter` 在非主菜单相位给 `MainBG` 换贴图换色罩；回主菜单还原场景作者值。
- 像素图批处理不得把这几张 tiling 底改回 Clamp。

## 相关

- [ADR-0053](0053-dungeon-environment-from-run-progress.md) — 环境变体权威
- [ADR-0055](0055-venue-board-visuals-static-fallback.md) — 场地框/格面仍静态
- `P/Flow/MainMenu/MainMenuBackdropPresenter.cs` — 滑动与按层换图
