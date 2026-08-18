---
status: accepted
---

# 场地框/背景/格面暂用场景静态配色

## 决策

- **暂时断开** `VenueEnvironmentPresenter` 对 `GroundPanel `、`GroundAnchors/slotN` 的运行时动态换图/换色接线（`ApplyVenueBoardVisualTheming = false`）。
- **MainBG 滑动底** 已按层恢复动态接线，见 [ADR-0058](0058-mainbg-scroll-by-dungeon-layer.md)；本决策不再约束 MainBG 贴图/色罩。
- **保留** ADR-0053 全部读侧逻辑：`GetCurrentDungeonEnvironmentQuery`、`DungeonEnvironmentCatalog.Resolve`、`dungeon_environments.json` 作者表、表现层配置器「地下城虚构」页、怪物卡面 `faceBackground` 运行时覆盖、大楼层提示与战前 `{floor}` 环境名等**不受影响**。
- 上述三样视觉以 **MainScene 场景作者值** 为准（原版基础配色）：
  - `GroundPanel `：`F_UI_Panel_H` 9-slice，tint 白；
  - `GroundAnchors` 九格：暖褐格面 tint（场景序列化色）。
  - `MainBG` 滑动底：按层换贴图与色罩（ADR-0058），不再用本决策的静态暖褐。

## 为什么

外部美术审计反馈：按地下城环境动态切换场地框、氛围底与格面 tint 的观感不如原版静态配色；在重新调色/分环境面板方案定稿前，先回退视觉、避免错误方向固化。

## 后果

- 换层/换房间时场地框与格面**不再**随环境变体变色；卡面背景仍随环境切换。局内 MainBG 滑动底按层切换（ADR-0058）。
- `dungeon_environments.json` 中 `groundPanel` / `slotHex` 字段与编辑器 UI **继续维护**，便于日后一键恢复场地框/格面接线（将 `ApplyVenueBoardVisualTheming` 改回 `true`）。`mainBackground` / `mainBackgroundHex` 已由 MainBG 滑动接线消费。
- 恢复动态接线前须重新过美术验收（明度分层、Apollo 色板、困难血色面板等 ADR-0053 约束仍适用）。

## 相关

- [ADR-0053](0053-dungeon-environment-from-run-progress.md) — 地下城环境变体权威（卡面背景等仍生效）
- [ADR-0058](0058-mainbg-scroll-by-dungeon-layer.md) — 局内 MainBG 滑动底按层切换
- `P/Flow/BoardBriefTip/VenueEnvironmentPresenter.cs` — 场地框/格面接线开关
