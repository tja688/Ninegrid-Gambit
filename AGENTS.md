# agents.md

## 项目介绍

Unity 6.3 LTS · URP · 2D 像素风游戏原型底座。当前仓库仅保留像素视觉管线，便于从零重建玩法。

- **框架**：QFramework（插件保留，暂无业务脚本依赖）
- **代码入口**：`Assets/Scripts/PixelVisuals`
- **唯一游戏场景**：`Assets/Scenes/MainScene.unity`
- **全局约束**：开发前阅读 [`rules.md`](rules.md)

---

## 像素视觉（保留）

| 内容 | 路径 |
|------|------|
| 后处理 Shader / 材质 | `Assets/Arts/VisualProfiles/` |
| 运行时控制器 | `TableNinePixelSnapController`、`TableNineTmpScanlineBinder` |
| URP Renderer Feature | `Assets/Settings/Renderer2D.asset` → TableNine Pixel Snap Post |
| 一键安装菜单 | `Tools/TableNine/Install Pixel Snap Post` |
| 场景清理菜单 | `Tools/Project/Cleanup MainScene` |

## 工具与工作流

- **Unity MCP**：改场景/组件用 MCP，**禁止**手改 `.unity`；改脚本后 `refresh_unity` 并读 Console。
- **CodeGraph MCP**：查符号、调用链、影响面。

---

## 协作约定

- 每次任务后进行全量 **git 提交**；提交信息简洁概括目的。