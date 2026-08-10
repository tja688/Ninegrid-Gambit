Part of #192.

## 目标

交付首个素材型播放器，并建立不越界的空间/排序/遮罩契约。

## 范围

- 定义 `VfxSpatialContext`：语义角色、可选受控域宿主/格位、发射时位置快照及诊断身份。
- 定义 `IVfxDomainHost` 能力协商：父级、坐标转换、遮罩、跟随和允许排序范围。
- 实现 `sprite-sheet` player：Resources 帧加载、确定帧序、FPS/速度/缩放/色调、有限循环、scaled/unscaled（默认 scaled）与 Completed 回报。
- 默认安全池化并完整 Reset；记录 create/reuse/release/destroy 事实。
- Attached 宿主丢失正常结束；Independent 在接纳时确定空间并自然播完。

## 验收

- atlas/folder 帧序一致，`spritesheet_N` 数字排序稳定。
- 播放器不能修改共享相机、Canvas、卡级 SortingGroup 或 L0-L3 变换塔。
- Attached/Independent 生命周期、循环结束、时间基和池化 Reset 有契约测试。
- Editor 预览可从素材索引建立临时 Binding 试听，但运行时不读取索引默认值。

## 通用约束

- Part of #192。
- 遵守 ADR-0040、ADR-0001、ADR-0007、ADR-0018 与 `CONTEXT.md` 的 VFX 领域词汇。
- VFX 不成为规则权威，不占主线 ack；失败可见但不得阻断游戏流程。
- 不接管普通旧 FX 路径；仅金币是本 Spec 的明确迁移例外。
- 完成后同步实际受影响的 `docs/code-map/`，并满足 Unity recompile 后 Console 无本票新增 Error/Exception/Assert。
