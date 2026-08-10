Part of #192.

## 目标

从硬编码 Audio 的工作台服务器中抽取可复用 transport/host，不改变现有声音工作台行为。

## 范围

- 参数化 host state、web root、菜单入口、协议命令和端口分配。
- 共享 loopback/Host/Origin 校验、Bearer token、CSP/no-store、1 MiB 门禁、revision envelope、WebSocket+长轮询、主线程泵和程序集重载关闭。
- AudioWorkbenchEditorState、Audio DTO、Audio 页面和命令保持独立。
- 先补/保留 Audio 协议与安全契约，再迁移 Audio 使用抽取层。

## 验收

- Audio 工作台 snapshot/delta/command、WS 回退、token、冲突恢复与启动菜单行为不回归。
- transport 不引用 AudioBinding、MMSoundManager 或 VFX 语义类型。
- 可用假 host 启动第二套独立页面与命令协议。

## 通用约束

- Part of #192。
- 遵守 ADR-0040、ADR-0001、ADR-0007、ADR-0018 与 `CONTEXT.md` 的 VFX 领域词汇。
- VFX 不成为规则权威，不占主线 ack；失败可见但不得阻断游戏流程。
- 不接管普通旧 FX 路径；仅金币是本 Spec 的明确迁移例外。
- 完成后同步实际受影响的 `docs/code-map/`，并满足 Unity recompile 后 Console 无本票新增 Error/Exception/Assert。
