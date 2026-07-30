---
status: accepted
---

# 卡面槽位定位不得依赖世界变换

## 决策

1. **卡面主视图（及同类槽位）的 Mask 锚定必须在祖先变换无关的局部空间完成。** 用共同祖先两侧各自累乘 `localPosition` / `localRotation` / `localScale` 得到局部到局部矩阵，再在父节点空间内算包围盒与 delta。**禁止**把 `localToWorldMatrix` / `InverseTransformPoint` / Renderer 世界 `bounds` 作为入场、悬停、退场、死亡缩放等期间的主定位路径。
2. **BounceFan（及其他会临时把祖先 `scale` 置 0 的流程）不得在祖先 `scale=0` 期间提交卡面主视图定位。** `BuildEntries` 保持 `scale=1` 套视觉；入场归零只允许发生在入场动画入口。二次 Commit（如 `RewardOffered` 冲刷）若仍可能落在退化变换上，局部空间算法须保证结果与 `scale=1` 时一致。
3. **算出的 `localPosition` 非有限值（NaN / Inf）时拒写。** 退化路径不得把节点甩飞。

`ComputeWorldPositionForAnchor` 可保留为纯数学辅助 / 单测入口，但不得再作为实例定位主算法。

## 为什么

世界空间锚定依赖祖先的有效变换。BounceFan 入场把 wrapper `localScale` 置 0 后，世界包围盒塌成一点，`InverseTransformPoint` 退化，主图标 `localPosition` 被写成近似偏移原点。遗物卡面内容整体有 x 偏移且主图标吃 `VisibleInsideMask`，损坏落点跑出 Mask → 登场无主图案；点选后 `scale≈1` 再 Sync 才「出现」。帮助卡节点 x=0，同一损坏落在 Mask 内，肉眼不易察觉。入场 / 悬停 / 退场 / 死亡缩放都会制造同类窗口；把定位绑在世界变换上，等于把正确性押在「此刻祖先变换刚好正常」。

## 考虑过的替代

- **只删 BounceFan `BuildEntries` 末尾的 `scale=0`，不改锚定算法**：否决为唯一手段——能关当前窗口，但悬停抬升、退场缩放、死亡缩小等仍会踩世界空间坑；局部空间才是不变量。
- **二次 Commit 时跳过 Placement**：否决——掩盖症状，且其它入口仍会写坏。
- **检测 `lossyScale≈0` 则跳过写入**：否决——时序竞争、阈值难定，且旋转/非均匀缩放仍可能扭曲世界包围盒。

## 后果

- `CardMainVisualMaskAnchor.GetAnchoredLocalPosition` / `GetSuggestedLocalPosition` 走局部空间；拿不到共同祖先等退化情况才回退世界路径。
- `CardMainVisualPlacement` 拒写非有限 `localPosition`。
- BounceFan `BuildEntries` 不再置 `wrapper.localScale = Vector3.zero`；`PlayEntryAnimation` 仍负责入场归零。
- 回归测试须覆盖「零缩放祖先下锚定不变」与「图标 AABB 留在 Mask 内」，不能只做源码文本顺序断言。

## 相关

- [ADR-0002](0002-card-chassis-and-face-templates.md) — 单底盘四卡面
- [ADR-0005](0005-card-face-beat-commit.md) — 卡面提交时机；Bounce 候选项经 `RewardOffered` 二次提交
- [ADR-0008](0008-single-source-content-and-resources-loading.md) — 出包只认 `/Resources/` 图标路径
- [`docs/code-map/presentation.md`](../code-map/presentation.md) — BounceFan / 主视图锚定现状
