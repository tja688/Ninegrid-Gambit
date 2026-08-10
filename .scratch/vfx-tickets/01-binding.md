Part of #192.

## 目标

建立新 VFX 系统的内容与声明基础：稳定 `VfxCue`、`PersistentVfxState`、单源 `vfx_bindings.json`、五维稳定选择器、严格解析与作者保存门禁。

## 范围

- 定义 Cue/State Attribute、中文说明、模块、权威发射者及允许上下文。
- Binding 文件内分 `cueBindings[]` / `stateBindings[]`，支持 `enabled`、稳定 `playerId`、Attached/Independent、素材型真实参数及覆盖白名单。
- 复用 `cardDefId/skillId/roomId/itemDefId/contentId` 与最高特异性解析。
- 同特异性歧义阻止保存；运行时严格解析可返回结构化错误。
- `visual_effects.json` 只作为 Editor 素材候选索引，不参与运行时默认值合并。
- 增加声明扫描器、重复/空说明/孤儿/歧义/player 缺失/非法覆盖等卫生校验契约。

## 验收

- 基础、单选择器、联合选择器解析顺序可测。
- 合法空 Catalog 可装载；非法 JSON/null rows/重复键不替换旧 Catalog。
- 运行时 UID、Transform 和坐标无法进入 Binding Key。
- 正式 JSON 可由 Editor session 原子读写并保留 `humanConfirmed`。

## 通用约束

- Part of #192。
- 遵守 ADR-0040、ADR-0001、ADR-0007、ADR-0018 与 `CONTEXT.md` 的 VFX 领域词汇。
- VFX 不成为规则权威，不占主线 ack；失败可见但不得阻断游戏流程。
- 不接管普通旧 FX 路径；仅金币是本 Spec 的明确迁移例外。
- 完成后同步实际受影响的 `docs/code-map/`，并满足 Unity recompile 后 Console 无本票新增 Error/Exception/Assert。
