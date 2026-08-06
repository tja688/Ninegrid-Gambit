# AGENTS.md

## 项目

**TableNine（九宫牌局）**：Unity 6.3 LTS · URP · 2D 卡牌像素风 Roguelike，九宫格棋盘驱动的卡牌战斗。

**内容代号**：`monster.skull_head`、`deck.dragon`、`SkeletonFusion*` 等主题词是历史虚构命名（不透明主键 / 类名），以实现效果为准；`deckId` 只作内部渠道（卡背 / 遭遇分组），玩家可见称呼另行映射。（[ADR-0014](docs/adr/0014-theme-ids-are-legacy-opaque.md)）

## 文档地图

| 文档 | 何时读 |
|------|--------|
| [`docs/code-map/`](docs/code-map/) | 代码现状入口（程序集、Presentation 目录、测试地图） |
| [`docs/adr/`](docs/adr/) | 长期架构决策与行为不变量 |
| [`CONTEXT.md`](CONTEXT.md) | 仓库当下事实 |
| `Assets/Notes/` | 进行时过程笔记，**非权威** |
| `Assets/Docs/九宫格登神` | 策划设计文档，可参考；与实现细节 / 开发要求冲突时，提出问题让用户确认 |

## Code Map 维护（开发后必做）

先文档、后或同 PR 改代码。代码现状只写进 `docs/code-map/` + `docs/adr/`（`Assets/Notes/` 或新镜像库不存代码现状；未落地 Spec 的目标树不写入）。

| 改动类型 | 更新目标 |
|----------|----------|
| 表现层目录 / 程序集边界 / 装配入口（`Setup/`、`CompositionRoot`、场景绑定） | [`docs/code-map/README.md`](docs/code-map/README.md) |
| Controller / System / Command·Query 边界、Hook 矩阵、读写与扩展点 | [`docs/code-map/presentation.md`](docs/code-map/presentation.md) |
| 测试分层、结构护栏、关闭门槛 | [`docs/code-map/tests.md`](docs/code-map/tests.md) |
| 长期行为不变量（Batch-ack、占格权威、IntentIntake、卡面 Commit 等） | 新增或修订 [`docs/adr/`](docs/adr/)，并在 code-map 中引用 |

**关闭门槛**（见 [`docs/code-map/tests.md`](docs/code-map/tests.md)）：受影响 EditMode 绿；`recompile` 完成后 Console 无新增 Error / Exception / Assert。全量 PlayMode 终验属 Spec 级，非每张票的默认门槛。硬规则见 [`.cursor/rules/code-map-maintenance.mdc`](.cursor/rules/code-map-maintenance.mdc)。

## 工具与工作流

**严禁 worktree**：禁止 `git worktree add`、旁路克隆、best-of-n 第二目录；所有改动只在本仓库根目录进行。

**Unity CLI（首选）**：与 Editor 协作一律用 `unity` CLI + `com.unity.pipeline`（`unity command …`）；改场景 / 组件优先走 Pipeline 命令。详见 `docs/agents/unity-cli.md`；硬规则 `.cursor/rules/unity-cli.mdc`。

**场景中文名检索**：`.unity` / `.prefab` YAML 里中文 `m_Name` 常写成 `\uXXXX` 转义（如 `作弊工具BG` → `"\u4F5C\u5F0A\u5DE5\u5177BG"`）。用明文中文 `grep` 会误报「不存在」——应搜 Unicode 转义、在 Editor 里 `FindObjectsOfTypeAll` / Pipeline `find_gameobjects`，或先解码再比。

**Pipeline 长命令纪律**：
- `run_tests` 一律走 `--async_tests true`（async 模式响应即时返回），随后轮询 `test_status` 至 `completed`
- `recompile` 后轮询 `recompile_status` 至 `completed`，再发下一个命令
- 命令超时后先查 `test_status` / `recompile_status` / `api/status` 确认执行状态，再决定是否重试，避免重复启动同一命令

**技能与工具**：

| 工具 / 技能 | 何时用 | 位置 |
|-------------|--------|------|
| Editor 启动 | 开 / 重开 Editor：直启 + `-automated`，避免外部改 `.unity` 弹窗卡死 Pipeline；重启前先保存当前工作 | `.cursor/skills/unity-automated-launch/` |
| 多 AI 协调 | 同仓多 Agent：先 `claim`；EditMode 测走 `ai-workspace test`；关 / 重开 Editor 前先 `gate-restart` | `.cursor/skills/ai-workspace/` |
| 日志分析 | 战斗 / 流程 / 表现日志（Play 结束导出至 `Assets/Notes/Logs/`） | `.cursor/skills/table-nine-battlelog-analysis/` |
| 像素描边 | 像素画 snap 且不糊 SDF / TMP 文本 | `.cursor/skills/sprite-owned-pixel-snap/` |
| QuickTest 通道 | 主菜单 `\` + `\0`–`\9` 效果体验通道（JSON 空怪、动态技能组装、卡面技能描述；近义技能占位规则） | `.cursor/skills/quick-test-effect-channels/` |
| Issue tracker | 本项目 Issue 走 GitHub Issues（`gh`） | `docs/agents/issue-tracker.md` |
| Triage labels | 标签词表：`needs-triage`、`needs-info`、`ready-for-agent`、`ready-for-human`、`wontfix` | `docs/agents/triage-labels.md` |
| Domain docs | 单一上下文：根 `CONTEXT.md` + `docs/adr/` | `docs/agents/domain.md` |
