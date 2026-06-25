# LEGACY_V1_PERFORMANCE

V1 烘焙的表演黑盒（DOTween / Timeline）。待 V2 `PlanStep` + `PerformancePlanBuilder` 改造调用契约。

- **禁止**新代码依赖已删除的 `Adaptors` / `FSM` / `Registry`
- V2 导演层请新建 `Director/`、`Effects/`，勿在本目录叠补丁
- 离线试演：见 `Tools/*PreviewTool.cs`
