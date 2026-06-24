# agents.md

## 项目介绍

**TableNine（九宫牌局）**：Unity 6.3 LTS · URP · 2D 卡牌像素风 Roguelike。核心玩法为九宫格棋盘驱动的卡牌战斗。

- **框架**：QFramework
- **代码入口**：`Assets/Scripts`
- **唯一游戏场景**：`Assets/Scenes/MainScene.unity`（原型期）
- **全局约束**：开发前阅读 [`rules.md`](rules.md)（架构分层、禁止手改 `.unity` 等）

---

## 文档地图（按用途选读）

### 架构与设计

| 文档 | 何时读 |
|------|--------|
| [`Assets/Notes/九宫牌局权威顶层架构设计.md`](Assets/Notes/九宫牌局权威顶层架构设计.md) | 总览 Core 契约、三窗口、分层 |
| [`Assets/Notes/表现层方法论.md`](Assets/Notes/表现层方法论.md) | 锚点/演员/表演/导演四盒子、补位与首帧 delta 坑 |
| [`Assets/Notes/Core表现层Command与Event消费清单-2026-06-20.md`](Assets/Notes/Core表现层Command与Event消费清单-2026-06-20.md) | Command / Event 与表现消费对照 |
| `Assets/Docs/` | 策划设计案、效果语义 |
| 表现层蓝图 canvas（工作区外） | 全局表现层规划；上下文大，必要时再读 |
| [`Assets/Notes/归档/表现层蓝图落地对照表.md`](Assets/Notes/归档/表现层蓝图落地对照表.md) | 蓝图 ↔ 代码「★已落地」同步表 |

### 开发与冲刺

| 文档 | 何时读 |
|------|--------|
| [`Assets/Notes/Demo落地冲刺开发计划.md`](Assets/Notes/Demo落地冲刺开发计划.md) | 当前 Demo 阶段任务（P0–P3）与优先级 |
| [`Assets/Notes/数据表工作流落地.md`](Assets/Notes/数据表工作流落地.md) | Luban / 内容表管线 |
| [`Assets/Notes/`](Assets/Notes/) | 过程性笔记；用户要求落地汇报统一放此目录 |

### 表现层排障（P2 重点）

| 资源 | 何时读 |
|------|--------|
| **[`Assets/Notes/表现层流程卡点排查手册.md`](Assets/Notes/表现层流程卡点排查手册.md)** | **流程卡住、点不了、节点不推进、批次锁死** — 首选 |
| [`Assets/Settings/PresentationTraceConfig.asset`](Assets/Settings/PresentationTraceConfig.asset) | Trace 通道、Watchdog 阈值、Runtime HUD |
| `Assets/Scripts/NineGrid.Presentation/Diagnostics/` | Trace / Watchdog / 适配器超时埋点实现 |
| Unity 菜单 **NineGrid → Presentation Trace** | Play Mode 实时 tail、复制 Stall dump |

### AI 协作 Skill（表现层落地）

| Skill | 泳道 / 盒子 |
|-------|-------------|
| [`.cursor/skills/table-nine-performance-crafting`](.cursor/skills/table-nine-performance-crafting) | 盒子③ 表演黑盒 |
| [`.cursor/skills/table-nine-interaction-crafting`](.cursor/skills/table-nine-interaction-crafting) | 泳道 B 交互 FSM |
| [`.cursor/skills/table-nine-adapter-crafting`](.cursor/skills/table-nine-adapter-crafting) | 盒子④ 适配器 / 批次播放 |
| [`.cursor/skills/table-nine-effect-landing`](.cursor/skills/table-nine-effect-landing) | 效果 DSL 端到端落地 |

---

## 表现层蓝图落地工作流

实现必须以蓝图为锚，保证「规划 ↔ 代码」可追溯：

1. **开工前查蓝图**：定位泳道 A/B/C、脊柱、适配器、表演黑盒；对照 `表现层蓝图落地对照表.md`。
2. **蓝图无对应项**：先与用户确认是命名未对齐还是设计变更，再实现。
3. **完工后双向同步**：蓝图黄色「★已落地」注释 + 对照表更新 + `updatelog` 摘要。
4. **粒度**：一项落地一条注释；表演黑盒与适配器分开标缺口。

---

## 工具与工作流

- **Unity MCP**：改场景/组件用 MCP，**禁止**手改 `.unity`；改脚本后 `refresh_unity` 并读 Console。
- **CodeGraph MCP**：查符号、调用链、影响面；混合 pending ID / 设计文案 / JSON 行时用 `rg`。
- **内核测试**：`NineGrid.Core.Tests`（含 `P7PresentationContractTests` 表现契约）。

---

## 协作约定

- 用户明确要求时再 **git 提交**；提交信息简洁概括目的。
- 表现层 **流程类 bug** 优先用 Trace 手册 + `PRES` 日志定位，再改代码。
