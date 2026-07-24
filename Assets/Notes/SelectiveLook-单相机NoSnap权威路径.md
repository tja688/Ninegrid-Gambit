# SelectiveLook：世界文字权威路径（单相机 + NoSnap Mask）

> 更新：2026-07-24  
> 取代旧笔记「双机 UICamera 逃 Snap」作为**生产权威**。归档 spec（`Assets/Notes/归档/灵动UI架构-spec-2026-07-17.md`）里仍写的 Overlay `UICamera` 仅作历史。

## 目标组合

| 需求 | 做法 |
|------|------|
| 世界空间 TMP，无 Canvas | `TextMeshPro` + `MeshRenderer`，挂载体子树 |
| 文字不吃 PixelSnap | 层 `NoPixelSnap` + Feature `buildNoSnapMask` |
| Sprite 按 SortingOrder 可挡字 | **同一 Base 相机**绘制；TMP 材质 `Queue=Transparent`、`ZTest Off` |
| 扫描线 | SelectiveLook Pass2 全屏；单相机下 TMP `_ScanlineEnabled=0`（Rig 同步） |

## 管线

1. `TableNineLookRig.useSnapMaskSingleCamera = true`（MainScene 默认）
2. Base 相机 **不剔除** `NoPixelSnap`；旧 Overlay `UICamera` 禁用并移出 stack
3. `TableNineSelectiveLookFeature`：`NoSnapMaskPass` 重绘 NoPixelSnap → `_NoSnapMask` → Pass1 Snap（mask 保护）→ Pass2 Scanlines

关键代码：`TableNineLookRig`、`TableNineSelectiveLookFeature`、`TableNineSelectiveLook.shader`、`TableNineNoSnapMask.shader`。

## TMP 材质约束

- Shader：`TextMeshPro/TableNine Bitmap|Distance Field Scanline Overlay`（名称保留 Overlay，**队列已是 Transparent**）
- `Queue=Transparent`、`ZTest Off`、`LightMode=Universal2D`（供 2D 批与 NoSnap Mask 抽到）
- 描边：`TmpBitmapPixelOutline`（几何），不是 shader outline width

## Legacy / 不要做

- **朴素双机**：UI 相机只画字 → 字永在最上，无法挡字  
- **UI 相机再画一遍世界**：未 Snap 世界会盖掉 Main 已 Snap 画面  
- 真正能挡字的双 RT 需要 occluder ColorMask0/Stencil + 只合成字，成本高；仅当 Mask 路径验证失败再评估  

## 验收清单

- [ ] 字形清晰（相对卡面 PixelSnap 后仍锐利）
- [ ] 更高 `SortingGroup` / order 的卡或书能盖住较低 order 的字
- [ ] 扫描线观感可接受（字随全屏 Pass2）
- [ ] Frame Debugger：存在 `TableNine NoSnap Mask`，mask 上可见字形 alpha
