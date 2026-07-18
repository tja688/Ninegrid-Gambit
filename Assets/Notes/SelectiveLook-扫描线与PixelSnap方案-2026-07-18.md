# Selective Look：扫描线全局 × PixelSnap 可选

> 日期：2026-07-18 · 场景验证：`UITestSence` / `大盘构型0-主菜单`

## 旧债

| 层 | 做法 | 问题 |
|----|------|------|
| 世界 | `FullScreenPass`（`TableNinePixelSnap`）对整帧 UV snap + 扫描线 | 文字一旦进同一 RT 就被 snap 糊掉 |
| 文字 | `ScreenSpaceOverlay` + TMP 自带扫描线材质 | 与世界完全脱域，灵动跟随极难受 |

## 新方案（已在主菜单落地）

```
Main Camera (Base)
  cull: ~NoPixelSnap
  feature: TableNine Selective Look
           → PixelSnap（整帧世界）
           → Scanlines（整帧世界）
  stack → UICamera (Overlay)
            cull: NoPixelSnap only
            无 PixelSnap feature（Overlay 被跳过）
            Canvas: Screen Space Camera → UICamera
            TMP 扫描线材质：与 Rig 参数同步，补齐文字侧扫描线
```

- **扫描线**：世界靠 Selective Look Pass2；文字靠 TMP Scanline（同 `_PixelResolution` / intensity，对齐）。
- **PixelSnap**：只打在 Base 相机世界内容；`NoPixelSnap` 层（文字）走 Overlay 相机，不被 UV snap。
- **跟随**：`LivingTextWorldBinder` 投影跟随世界背板；Play 时自动捕获编辑器相对偏移（主菜单已绑 标题→4 / 开始→12 / 设置→11 / 结束→2）。

## 关键资产

- `Assets/Scripts/UI/VisualLook/TableNineLookRig.cs` — 相机栈 + Canvas 换域 + 参数同步
- `Assets/Scripts/UI/VisualLook/TableNineSelectiveLookFeature.cs` — Base-only 全屏 Look
- `Assets/Scripts/UI/VisualLook/LivingTextWorldBinder.cs` — 文字跟随世界
- `Assets/Arts/VisualProfiles/TableNineSelectiveLook.shader/.mat`
- Layer：`NoPixelSnap`（slot 8）
- `Renderer2D`：旧 `TableNine Pixel Snap Post` 已禁用；新 `TableNine Selective Look` 启用

## 用法

1. 需要 snap 的对象：保持 Default（或任意非 `NoPixelSnap`）层。
2. 不要 snap 的对象（文字/细 UI）：放到 `NoPixelSnap`，并由 `TableNineLookRig` 管到 UICamera。
3. 调参：Main Camera 上 `TableNineLookRig`（resolution / snap / scanline）。
4. 跟随：在 Text Overlay 根上配 `LivingTextWorldBinder` bindings。

> **批量处理其它大盘构型（AI SOP）**：见  
> [`LivingText-UICamera换域与背板绑定操作指南-2026-07-18.md`](./LivingText-UICamera换域与背板绑定操作指南-2026-07-18.md)  
> （独占激活构型、激活失活 Overlay、屏幕 AABB 可靠配对、截图防绑错、Play 无跳变验收）。

## 后续可选项

- 若要把扫描线也合成到最终一帧（彻底去掉 TMP 扫描线材质）：给 UICamera 单独 Renderer，或在 stack 之后做一次 scanline-only blit。
- `buildNoSnapMask` 预留给同相机 mesh 路径；UGUI CanvasRenderer 不进 `DrawRenderers`，主路径是相机栈。
- Unity 6 下本 Feature 走 `RecordRenderGraph` + `AddBlitPass`（不再依赖 Compatibility Mode / 旧 `Execute`）。
