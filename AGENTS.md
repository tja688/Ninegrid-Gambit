# agents.md

## 项目介绍

**TableNine（九宫牌局）**：Unity 6.3 LTS · URP · 2D 卡牌像素风 Roguelike。核心玩法为九宫格棋盘驱动的卡牌战斗。

---

## 文档地图（按用途选读）

### 架构与设计

| 文档 | 何时读 |
|------|--------|
| [`docs/code-map/`](docs/code-map/) | **代码现状入口**（程序集、Presentation 目录、测试地图） |
| [`docs/adr/`](docs/adr/) | 长期架构决策 |
| `Assets/Notes/` | 进行时过程性开发笔记；归档里有历史记录 |

### 内容代号与卡组

- **内容代号**：`monster.skull_head`、`deck.dragon`、`SkeletonFusion*` 等主题词是历史残留的虚构命名（不透明主键 / 类名）；以实现效果为准，不要被字面世界观带偏。（见 [ADR-0014](docs/adr/0014-theme-ids-are-legacy-opaque.md)）
- **卡组**：`deckId` 只作内部渠道（卡背 / 遭遇分组）；玩家可见称呼若需要，用另行映射的显示名即可。

### Code Map 维护（开发后必做）

权威层级：`docs/code-map/` + `docs/adr/` + 根目录 [`CONTEXT.md`](CONTEXT.md) 描述**仓库当下事实**；`Assets/Notes/` 仅过程笔记，**非权威**。

**何时同步 Code Map（先文档、后或同 PR 改代码）**

| 改动类型 | 更新目标 |
|----------|----------|
| 表现层目录 / 程序集边界 / 装配入口（`Setup/`、`CompositionRoot`、场景绑定） | [`docs/code-map/README.md`](docs/code-map/README.md) |
| Controller / System / Command·Query 边界、Hook 矩阵、读写与扩展点 | [`docs/code-map/presentation.md`](docs/code-map/presentation.md) |
| 测试分层、结构护栏、关闭门槛 | [`docs/code-map/tests.md`](docs/code-map/tests.md) |
| 长期行为不变量（Batch-ack、占格权威、IntentIntake、卡面 Commit 等） | 新增或修订 [`docs/adr/`](docs/adr/)，并在 code-map 中引用 |

**普通实施票关闭门槛**（见 [`docs/code-map/tests.md`](docs/code-map/tests.md)）：受影响 EditMode 绿；`unity command recompile` 后 Console 无新增 Error / Exception / Assert。全量 PlayMode 终验属 Spec 级，非每张票的默认门槛。

**禁止**：在 `Assets/Notes/` 或新镜像库里维护与 code-map 重复的「代码现状」；未落地 Spec 目标树（如平铺 `Architecture/`、`Views/`）写入 code-map 当作既成事实。

项目级硬规则见 [`.cursor/rules/code-map-maintenance.mdc`](.cursor/rules/code-map-maintenance.mdc)。

### 游戏设计文档

[`Assets/Docs/九宫格登神`] 为策划维护的游戏设计文档，在有了解权威设计需求的情况下可以参考，但注意，有时候因为沟通问题会出现项目实现细节、用户开发要求与策划设计冲突，此时需要提出问题让用户确认。

---

## 工具与工作流

- **严禁 worktree**：禁止 `git worktree add`、旁路克隆、best-of-n 第二目录；所有改动只在本仓库根目录进行。
- **Unity CLI（首选）**：与 Editor 协作一律用 `unity` CLI + `com.unity.pipeline`（`unity command …`），改场景/组件优先走 Pipeline 命令；改脚本后 `unity command recompile`（或等自动编译）并用 `unity command console` 读 Console。详见 `docs/agents/unity-cli.md`；硬规则 `.cursor/rules/unity-cli.mdc`。
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

### Unity `-automated` launch

Start/restart Editor with `-automated` via `.cursor/skills/unity-automated-launch/` (portable; copy the folder to other repos).

### Sprite-owned pixel snap

Pixel-art snap without blurring SDF/TMP text: geometry snap on sprite materials only, fullscreen sample snap off. Portable: `.cursor/skills/sprite-owned-pixel-snap/` (copy the folder to other Unity URP 2D repos).

### AI workspace coordinator

Multi-agent claims + EditMode test mutex: `.cursor/skills/ai-workspace/` (PowerShell CLI `scripts/ai-workspace.ps1`). Claim on start; run EditMode only via `test`; `gate-restart` before relaunching Editor.

### QuickTest effect channels

Main-menu `\` (`Backslash`) `\0`–`\9` effect experience channels: blank monsters in JSON, dynamic skill assembly only on QuickTest, card-face skill descriptions. Near-synonym skill-gap rename/new/reserve rules. See `.cursor/skills/quick-test-effect-channels/`.

