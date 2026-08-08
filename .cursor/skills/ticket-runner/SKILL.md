---
name: ticket-runner
description: >-
  Serially lands GitHub Issues via Cursor Agent CLI (fresh context per ticket)
  using "/implement #N". Use when the user wants ticket-runner, serial issue
  landing, AFK implement queue, parent-spec child tickets, or to stop manually
  opening a new chat per ticket.
disable-model-invocation: true
---

# Ticket Runner

本地串行状态机：解析 GitHub 票队列 → **每张票新开一次** 受监督的 Cursor Agent CLI（`agent -p`）→ 提示词默认 **`/implement #{number}`** → 上一张交接完成后再跑下一张。

## 强制开场（处理本 skill 的模型必须执行）

在**任何**其它步骤之前，用一两句明文回复（可放在思考可见回复里）：

1. **当前默认模型为 `cursor-grok-4.5-high`。**
2. 若用户本轮用 `--model` / 口述指定了批次模型，同时写明：**本批次覆盖模型为 `<id>`**。
3. 提醒：若 Cursor 日后提供明显更强、且用户账号可用的默认候选，应建议用户更新 skill / `.cursor/ticket-runner.config.json` 的 `model` 字段（不要擅自改）。

然后才进入 doctor / plan / run。

## Critical — orchestrator only

**当前会话不要自己落地业务票。** 只运行本 skill 的脚本；真正的 `/implement` 在子进程里执行。

禁止给子 agent 传 `--worktree`。介入逃生通道**最多一次**（禁止递归介入烧 token）。

## Prerequisites

1. `gh` 已登录。
2. Cursor Agent CLI（非 PATH 上的 grok `agent.exe`）已 `login` 或设置 `CURSOR_API_KEY`。
3. `doctor` →（通过后）`plan` → `run` 全队列串行。仅在用户显式要求时才用 `run --once`。

## Entry

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".cursor/skills/ticket-runner/scripts/ticket-runner.ps1" <command> [options]
```

## Commands

| Command | Purpose |
|---------|---------|
| `doctor` | gh / agent / auth / 默认模型格式与（登录后）list-models |
| `plan` | 打印队列 + 批次模型 |
| `run` | 串行监督落地 |
| `status` | `state.json` |
| `help` | 帮助 |

## Queue sources

| Flag / 配置 | Behavior |
|-------------|----------|
| （默认） | 自动发现**开放**且标题以 `Spec:` 开头（`specTitlePrefix`）的 Spec，及其**未关闭**子票 |
| `--parent <n>` | 显式指定父 Spec；子票来源见下 |
| `--issues a,b,c` | 显式顺序 |
| `--label <name>` | 显式按标签拉票（如 `ready-for-agent`）；不再作为无参默认回退 |

**子票解析顺序**（合并去重）：父 Spec body 的 task list（`- [ ] #N`）→ GitHub `trackedIssues` → 子票 body 里的 `## Parent` / `Part of #<parent>`。

**多 Spec 冲突**：若多个开放 Spec 均有 pending 子票 → `plan`/`run` 退出码 1 并列出候选，**由用户选 `--parent`**；仅一个活跃 Spec 时自动选用，无需追问。

## 批次模型

- **Skill / 配置默认**：`cursor-grok-4.5-high`
- **整批覆盖**：`run ... --model <id>`（整次 `run` 的所有票共用）
- 优先级：`--model` > 项目 `.cursor/ticket-runner.config.json` > skill `config.example.json` > 内置默认

```powershell
# 默认：自动发现唯一活跃 Spec + 全队列串行
... ticket-runner.ps1 run

# 显式父 Spec（多 Spec 冲突时）
... ticket-runner.ps1 run --parent 164

# 整批指定模型（仅用户明确要求时）
... ticket-runner.ps1 run --model cursor-grok-4.5-high
```

## 健壮度（监督循环）

| 信号 | 默认 | 行为 |
|------|------|------|
| 单票硬预算 | 30 min | 不健康 → 一次 escape-hatch |
| stdout 空闲 | 10 min | 同上 |
| stream-json 解析失败 | ≥8 行 | 同上 |
| 登录/token、Agent 追问 | 收紧启发式 | **仅 soft 日志，不触发介入**（避免 Unity 401 / thinking 假阳性） |
| 子进程 stdout | 文件重定向 | 避免父进程管道挂起 |
| 介入 AI | 30 min | `kill_restart` / `continue_wait` / `pause_user` |
| 介入失败或再恶化 | — | **暂停主流程等用户**（exit 3），不二次介入 |
| `continue_wait` 宽限 | 15 min | 仍不结束 → pause_user |
| worker 已结束 | — | 跳过 escape-hatch，直接收尾 |

正常健康落地**不应**触发介入；介入只覆盖卡住 / 预算打满 / 流解析崩坏。

Escape-hatch 会另开一个 one-shot `agent -p`，要求写入 `intervention-decision.json`，**不落地业务**。

## 机器日志（非模型写入）

路径：`.cursor/ticket-runner/runs/<runId>/`

- `run.json`、`tickets/<n>/events.ndjson`、`stdout.log`、`stderr.log`、`meta.json`
- 索引：`runs/index.json`
- **最多保留 3 次 run**；超额删除最旧目录
- 原子写盘 + 失败只告警，不因日志失败中断业务（本地磁盘，无网络依赖）

## Default run (no extra user asks)

用户只说 `/ticket-runner`（或等价，且**没有**额外约束）时的默认：

| 项 | 默认 |
|----|------|
| 队列 | 自动发现唯一开放 `Spec:` 及其 pending 子票；多 Spec 冲突时停并让用户 `--parent` |
| 跑法 | `run` **全队列串行**（不用 `--once`） |
| 模型 | 配置默认 `cursor-grok-4.5-high` |

**不要追问**队列/跑法/模型，除非出现下列例外之一：

- 用户本轮点名了模型（`--model` / 口述 slug）
- 用户对落地顺序或范围有特殊要求（`--once`、`--from`、`--issues`、只要某几张等）
- `doctor` 失败，或 Spec 自动发现失败 / 多 Spec 冲突需用户选 `--parent`

```powershell
... doctor
... plan          # 或 plan --parent <n>
... run           # 全队列；仅用户要求时才加 --once
```

## Agent behavior when invoked

1. **先报默认模型**（见上）；若用户覆盖了批次模型则一并写明。
2. `doctor`；失败则停并报告，成功则继续。
3. 无上述例外时：**直接** `plan` → `run` 全队列（自动发现 Spec 或用户给的 `--parent`），不多问。
4. 有例外时：按用户约束组 flag，再 `plan` → `run`。
5. 回报 exit code 与 `runs/...` 路径。
6. **禁止**在本 chat 落地业务 issue。

## Portable install

复制整个 `ticket-runner/` 到 `~/.cursor/skills/ticket-runner/`。
