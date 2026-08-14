# AGENTS.md

## 项目

**Ninegrid Gambit（九宫牌局）**：Unity 6.3 LTS · URP · 2D 卡牌像素风 Roguelike，九宫格棋盘驱动的卡牌战斗。

**内容代号**：`monster.skull_head`、`deck.dragon`、`SkeletonFusion*` 等主题词是历史虚构命名（不透明主键 / 类名），以实现效果为准；`deckId` 只作内部渠道（卡背 / 遭遇分组），玩家可见称呼另行映射。（[ADR-0014](docs/adr/0014-theme-ids-are-legacy-opaque.md)）

## 文档地图

| 文档 | 何时读 |
|------|--------|
| [`docs/code-map/`](docs/code-map/) | 代码现状入口（程序集、Presentation 目录、验证约定） |
| [`Assets/Docs/`](Assets/Docs/) | 权威代码事实文档库（分层区域文档 + ADR 对照 + 领域词汇表 + 按 bug 症状导航） |
| [`docs/adr/`](docs/adr/) | 长期架构决策与行为不变量 |
| [`CONTEXT.md`](CONTEXT.md) | 仓库当下事实 |
| `Assets/Notes/` | 进行时过程笔记（非权威） |
| 策划设计文档（本地 MyNote） | 可参考；与实现 / 开发要求冲突时，向用户确认 |

## Agent 规则索引

硬规则均在 [`.cursor/rules/`](.cursor/rules/)（`alwaysApply: true`）。改代码、开子代理、调 Unity 前先对表：

| 主题 | 规则文件 | 要点 |
|------|----------|------|
| Unity 协作 | [`unity-cli.mdc`](.cursor/rules/unity-cli.mdc) | **MCP 首选**；不可用 / 缺能力 / 卡死时回退 CLI + Pipeline；禁 Safe Mode |
| 文档维护 | [`code-map-maintenance.mdc`](.cursor/rules/code-map-maintenance.mdc) | 同步 `docs/code-map/`、`docs/adr/`、`Assets/Docs/`；验证门槛与两击放弃 |
| 工作区 | [`no-worktrees.mdc`](.cursor/rules/no-worktrees.mdc) | 禁止 worktree、旁路克隆、best-of-n 第二目录 |
| 子代理模型 | [`subagent-models.mdc`](.cursor/rules/subagent-models.mdc) | 全局 hooks 白名单 + Task 选型；见 `%USERPROFILE%\.cursor\hooks.json` |

CLI 回退细则：[`docs/agents/unity-cli.md`](docs/agents/unity-cli.md)。Issue / 领域：[`docs/agents/issue-tracker.md`](docs/agents/issue-tracker.md)、[`docs/agents/domain.md`](docs/agents/domain.md)。

## 技能索引

| 技能 | 何时用 | 位置 |
|------|--------|------|
| Editor 启动 | 开 / 重开 Editor（`-automated`） | `.cursor/skills/unity-automated-launch/` |
| 日志分析 | 战斗 / 流程 / 表现日志 | `.cursor/skills/table-nine-battlelog-analysis/` |
| 像素描边 | 像素 snap 且不糊 SDF / TMP | `.cursor/skills/sprite-owned-pixel-snap/` |
| QuickTest | 主菜单 `\` + `\0`–`\9` 效果通道 | `.cursor/skills/quick-test-effect-channels/` |
| Live Lab | Play 中注入探针 / 补丁边玩边调 | `.cursor/skills/live-lab/` |
| 表演时序 | 效果错拍、重复、缺反馈 | `.cursor/skills/table-nine-presentation-timing/` |
