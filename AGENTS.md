# agents.md

## 项目介绍

嗨哟，收绞盘！（Heave and Paul!）， Unity 6.3 LTS · URP · 2D 像素风，24h gamejam 作品，在设计系统和落地实现时注意以快速出效果为第一要务。

- **框架**：QFramework
- **代码入口**：`Assets/Scripts`
- **全局约束**：开发前阅读 [`rules.md`](rules.md)

---

## 工具与工作流

- **Unity MCP**：改场景/组件用 MCP，**禁止**手改 `.unity`；改脚本后 `refresh_unity` 并读 Console。
- **CodeGraph MCP**：查符号、调用链、影响面。

---

## 协作约定

- 每次任务后进行全量 **git 提交**；提交信息简洁概括目的。