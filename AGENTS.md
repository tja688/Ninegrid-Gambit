# agents.md

## 项目介绍

**TableNine（九宫牌局）**：Unity 6.3 LTS · URP · 2D 卡牌像素风 Roguelike。核心玩法为九宫格棋盘驱动的卡牌战斗。

---

## 文档地图（按用途选读）

### 架构与设计

| 文档 | 何时读 |
|------|--------|
| `Assets/Docs/九宫牌局-代码文档/` | **代码现状权威库**（尽可能描述源码事实；摸结构、改模块、重构前先读，保质期短，重构完成后弃掉） |
| `Assets/Notes/` | 进行时过程性开发笔记；归档里有历史记录 |

---

## 工具与工作流

- **严禁 worktree**：禁止 `git worktree add`、旁路克隆、best-of-n 第二目录；所有改动只在本仓库根目录进行。详见 `.cursor/rules/no-worktrees.mdc`。
- **Unity CLI（首选）**：与 Editor 协作一律用 `unity` CLI + `com.unity.pipeline`（`unity command …`），**不要依赖 Unity MCP**。改场景/组件优先走 Pipeline 命令；改脚本后 `unity command recompile`（或等自动编译）并用 `unity command console` 读 Console。详见 `docs/agents/unity-cli.md`。
- **Unity `-automated` 启动**：Agent 开/重开 Editor 用 `.cursor/skills/unity-automated-launch/`（直启 + `-automated`，避免外部改 `.unity` 弹窗卡死 Pipeline；可整夹移植到其他工程）。
- **DevTest 小键盘测试**：见项目 skill `.cursor/skills/table-nine-devtest-keys/`（接入、置顶、优先级以 `TestKeyStack` SO 为准）。
- **战斗 / 流程 / 表现日志分析**：Play 结束导出至 `Assets/Notes/Logs/`；分析见 `.cursor/skills/table-nine-battlelog-analysis/`。

---

## Agent skills

### Issue tracker

Issues live in this repo's GitHub Issues (via `gh`). See `docs/agents/issue-tracker.md`.

### Triage labels

Default triage vocabulary: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: root `CONTEXT.md` + `docs/adr/`. See `docs/agents/domain.md`.

### Unity CLI

Prefer `unity command` over MCP for Editor automation. See `docs/agents/unity-cli.md`.

### Unity `-automated` launch

Start/restart Editor with `-automated` via `.cursor/skills/unity-automated-launch/` (portable; copy the folder to other repos).

