---
status: accepted
---

# 场地框/背景/格面暂用场景静态配色

## 决策

- **暂时断开** `VenueEnvironmentPresenter` 对 `GroundPanel `、`MainBG`、`GroundAnchors/slotN` 的运行时动态换图/换色接线（`ApplyVenueBoardVisualTheming = false`）。
- **保留** ADR-0053 全部读侧逻辑：`GetCurrentDungeonEnvironmentQuery`、`DungeonEnvironmentCatalog.Resolve`、`dungeon_environments.json` 作者表、表现层配置器「地下城虚构」页、怪物卡面 `faceBackground` 运行时覆盖、大楼层提示与战前 `{floor}` 环境名等**不受影响**。
- 上述三样视觉以 **MainScene 场景作者值** 为准（原版基础配色）：
  - `GroundPanel `：`F_UI_Panel_H` 9-slice，tint 白；
  - `MainBG`：暖褐氛围底（场景序列化色）；
  - `GroundAnchors` 九格：暖褐格面 tint（场景序列化色）。

## 为什么

外部美术审计反馈：按地下城环境动态切换场地框、氛围底与格面 tint 的观感不如原版静态配色；在重新调色/分环境面板方案定稿前，先回退视觉、避免错误方向固化。

## 后果

- 换层/换房间时场地三样**不再**随环境变体变色；卡面背景仍随环境切换。
- `dungeon_environments.json` 中 `groundPanel` / `mainBackgroundHex` / `slotHex` 字段与编辑器 UI **继续维护**，便于日后一键恢复接线（将 `ApplyVenueBoardVisualTheming` 改回 `true`）。
- 恢复动态接线前须重新过美术验收（明度分层、Apollo 色板、困难血色面板等 ADR-0053 约束仍适用）。

## 相关

- [ADR-0053](0053-dungeon-environment-from-run-progress.md) — 地下城环境变体权威（卡面背景等仍生效）
- `P/Flow/BoardBriefTip/VenueEnvironmentPresenter.cs` — 接线开关
