# agents.md

## 项目介绍

**TableNine（九宫牌局）**：Unity 6.3 LTS · URP · 2D 卡牌像素风 Roguelike。核心玩法为九宫格棋盘驱动的卡牌战斗。

---

## 文档地图（按用途选读）

### 架构与设计

| 文档 | 何时读 |
|------|--------|
| `Assets/Docs/九宫牌局-代码文档/` | **代码现状权威库**（只描述源码事实；摸结构、改模块、重构前先读） |
| `Assets/Docs/`（其余） | 策划设计案、效果语义 |
| `Assets/Notes/` | 进行时过程性开发笔记；归档里有历史记录 |

---

## 工具与工作流

- **Unity MCP**：改场景/组件用 MCP，**禁止**手改 `.unity`；改脚本后 `refresh_unity` 并读 Console。
- **DevTest 小键盘测试**：见项目 skill `.cursor/skills/table-nine-devtest-keys/`（接入、置顶、优先级以 `TestKeyStack` SO 为准）。
- **战斗 / 流程 / 表现日志分析**：Play 结束导出至 `Assets/Notes/Logs/`；分析见 `.cursor/skills/table-nine-battlelog-analysis/`。
- **代码现状**：先读 `Assets/Docs/九宫牌局-代码文档/`；改完脚本后按上文「代码文档库维护」同步文档。

---

## Agent skills

### Issue tracker

Issues live in this repo's GitHub Issues (via `gh`). See `docs/agents/issue-tracker.md`.

### Triage labels

Default triage vocabulary: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: root `CONTEXT.md` + `docs/adr/`. See `docs/agents/domain.md`.
