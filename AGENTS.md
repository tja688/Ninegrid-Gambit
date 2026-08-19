# AGENTS.md

## 项目

**Ninegrid Gambit（九宫牌局）**：Unity 6.3 LTS · URP · 2D 像素 Roguelike，九宫格驱动卡牌战斗。

**内容代号**（[ADR-0014](docs/adr/0014-theme-ids-are-legacy-opaque.md)）：`monster.*`、`deck.*`、`SkeletonFusion*` 等是实现侧不透明主键，勿从命名推断玩法。

## 文档地图

| 文档 | 何时读 |
|------|--------|
| [`docs/code-map/`](docs/code-map/) | 程序集、Presentation、验证门槛 |
| [`docs/adr/`](docs/adr/) | 行为不变量 |
| [`CONTEXT.md`](CONTEXT.md) | 仓库当下事实 |
| `Assets/Notes/` | 过程笔记（非权威） |
| 策划 MyNote（本地） | 半权威；与实现冲突时问用户 |

## 规则索引

[`.cursor/rules/`](.cursor/rules/) 均 `alwaysApply`。改代码 / Unity / **交付**前先对表：

| 主题 | 文件 | 触发 |
|------|------|------|
| Unity | [`unity-cli.mdc`](.cursor/rules/unity-cli.mdc) | 开 Editor、MCP/CLI、场景检索 |
| 文档 | [`code-map-maintenance.mdc`](.cursor/rules/code-map-maintenance.mdc) | 改结构、ADR、验证 |
| 提交 | [`agent-git-commit.mdc`](.cursor/rules/agent-git-commit.mdc) | 任务完成、准备交付 |

细则：[`docs/agents/`](docs/agents/)（Issue、CLI 回退命令等）。

## 技能索引

| 技能 | 触发 | 路径 |
|------|------|------|
| Editor 启动 | 开/重开、`-automated` | `.cursor/skills/unity-automated-launch/` |
| 日志分析 | 战斗/流程日志 | `.cursor/skills/table-nine-battlelog-analysis/` |
| QuickTest | `\` + `\0`–`\9` | `.cursor/skills/quick-test-effect-channels/` |
| Live Lab | Play 中探针/补丁 | `.cursor/skills/live-lab/` |
| 表演时序 | 错拍/重复/缺反馈 | `.cursor/skills/table-nine-presentation-timing/` |
| 像素 snap | sprite snap 且不糊 TMP | `.cursor/skills/sprite-owned-pixel-snap/` |
