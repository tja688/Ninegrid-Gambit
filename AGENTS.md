# agents.md

## 项目介绍

**TableNine（九宫牌局）**：Unity 6.3 LTS · URP · 2D 卡牌像素风 Roguelike。核心玩法为九宫格棋盘驱动的卡牌战斗。

---

## 文档地图（按用途选读）

### 架构与设计

| 文档 | 何时读 |
|------|--------|
| [`docs/code-map/`](docs/code-map/) | **代码现状入口**（程序集、Presentation 目录、测试地图） |
| [`docs/adr/`](docs/adr/) | 长期架构决策（时间线 Batch-ack、卡牌底盘） |
| `Assets/Notes/` | 进行时过程性开发笔记；归档里有历史记录 |

---

## 工具与工作流

- **严禁 worktree**：禁止 `git worktree add`、旁路克隆、best-of-n 第二目录；所有改动只在本仓库根目录进行。
- **Unity CLI（首选）**：与 Editor 协作一律用 `unity` CLI + `com.unity.pipeline`（`unity command …`），改场景/组件优先走 Pipeline 命令；改脚本后 `unity command recompile`（或等自动编译）并用 `unity command console` 读 Console。详见 `docs/agents/unity-cli.md`；硬规则 `.cursor/rules/unity-cli.mdc`。
- **禁止 Safe Mode**：不准以 Safe Mode 启动或在 Safe Mode 下操作；遇编译错误选 Ignore/Quit 并修脚本，再用 `-automated` 正常重开。
- **Unity `-automated` 启动**：Agent 开/重开 Editor 用 `.cursor/skills/unity-automated-launch/`（直启 + `-automated`，避免外部改 `.unity` 弹窗卡死 Pipeline；可整夹移植到其他工程）。若编辑器已打开，重启编辑器时必须保存当前工作再重启。
- **多 AI 工作区协调**：同仓多 Agent 先 `claim`，EditMode 测走 `ai-workspace test`，关/重开 Editor 前先 `gate-restart`。见 `.cursor/skills/ai-workspace/`。
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

### Sprite-owned pixel snap

Pixel-art snap without blurring SDF/TMP text: geometry snap on sprite materials only, fullscreen sample snap off. Portable: `.cursor/skills/sprite-owned-pixel-snap/` (copy the folder to other Unity URP 2D repos).

### AI workspace coordinator

Multi-agent claims + EditMode test mutex: `.cursor/skills/ai-workspace/` (PowerShell CLI `scripts/ai-workspace.ps1`). Claim on start; run EditMode only via `test`; `gate-restart` before relaunching Editor.

