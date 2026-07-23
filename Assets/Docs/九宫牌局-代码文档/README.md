# 九宫牌局 · 代码文档库

> **DEPRECATED（2026-07-23）**  
> 本目录是 #28 重构前的**源码镜像库**，已失真，**不再是代码现状权威**。  
> **请改读**：[`docs/code-map/`](../../../docs/code-map/README.md) + [`docs/adr/`](../../../docs/adr/) + 根目录 `CONTEXT.md`。  
> **维护约定**：冻结——勿再增补或「同步源码」；需要导航时只更新 `docs/code-map/`。

历史索引仍保留备查（内容可能过期）：

→ [00-总览与路由/README.md](./00-总览与路由/README.md)  
→ [00-总览与路由/现状结论.md](./00-总览与路由/现状结论.md)

## 目录速览（历史）

| # | 文件夹 | 内容 |
|---|--------|------|
| 00 | [总览与路由](./00-总览与路由/) | INDEX、程序集依赖、命名空间对照 |
| 01 | [内核 Core](./01-内核-NineGrid.Core/) | QFramework 规则核 |
| 02 | [内容 Content](./02-内容层-NineGrid.Content/) | Catalog + Luban |
| 03 | [流程 Flow](./03-流程层-Flow/) | 主循环、表现时间线、诊断 |
| 04 | [卡牌 Cards](./04-卡牌表现层-Cards/) | 视图 / 场地 / 飞行 |
| 05 | [UI](./05-UI层/) | VisualLook；LivingUI → `Packages/com.livingui.stage` |
| 06 | [DevTest](./06-DevTest/) | 小键盘测试栈 |
| 07 | [测试体系](./07-测试体系/) | EditMode 用例地图 |
| 08 | [Editor 与工具链](./08-Editor与工具链/) | Editor 窗、Luban Tools |
| 09 | [跨层契约](./09-跨层契约与依赖/) | 依赖方向与枢纽体量 |
| 10 | [遗留与第三方](./10-遗留临时与第三方边界/) | Temporary、Plugins、QF |
