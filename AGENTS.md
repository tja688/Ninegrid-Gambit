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
| [`Assets/Notes/Core表现层Command与Event消费清单-2026-06-20.md`](Assets/Notes/Core表现层Command与Event消费清单-2026-06-20.md) | Command / Event 与表现消费对照 |
| `Assets/Docs/` | 策划设计案、效果语义 |
| `Assets/Notes/` | 进行时过程性开发笔记，归档里面有历史记录 |
| `C:\Users\jinji\Desktop\文档\MyNote\游戏开发项目\引擎工作区\九宫牌局架构\九宫牌局表现层（第二版）.canvas`| 全局表现层规划蓝图 |

---

## 工具与工作流

- **Unity MCP**：改场景/组件用 MCP，**禁止**手改 `.unity`；改脚本后 `refresh_unity` 并读 Console。
- **CodeGraph MCP**：查符号、调用链、影响面；混合 pending ID / 设计文案 / JSON 行时用 `rg`。
- **内核测试**：`NineGrid.Core.Tests`。

---

## 协作约定

- 用户明确要求时再 **git 提交**；提交信息简洁概括目的。