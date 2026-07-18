---
name: table-nine-world-living-text
description: >-
  Land TableNine living text as world-space TMP mesh on backboards: escape
  PixelSnap via UICamera/NoPixelSnap, keep scanlines via TMP material, pixel
  outline via TmpBitmapPixelOutline. Use when migrating LivingText from Canvas
  to world TMP, parenting text to 背板, fixing snap-blurred text, scanline/outline
  mismatch, or when the user mentions 世界字/灵动文字/只吃扫描线不吃snap/贴背板.
---

# TableNine 世界空间灵动文字

把文字从 Canvas 投影跟随，升级为**背板子物体上的 3D TMP 网格**：真跟随、不吃 PixelSnap、仍吃扫描线与像素描边。

## 钥匙句

字能不能跟着背板走，和字会不会被拧糊，是两件互不相干的事——前者靠是不是背板的小孩，后者靠画它的是不是那台会拧糊的相机。

## 背景（为何走到这）

| 阶段 | 做法 | 痛点 |
|------|------|------|
| Overlay Canvas | 文字完全脱域 | 跟不上灵动背板 |
| Screen Space Camera + Binder | UICamera 画字，投影跟随 | 能逃 Snap，但仍是「追着照」，联动别扭 |
| **世界 TMP（当前）** | 字挂背板下，NoPixelSnap + UICamera | 真父子跟随；逃 Snap；扫描线/描边走字自己的材质与组件 |

目标验收：**清晰（无 Snap 糊）+ 扫描线观感一致 + 移动背板字同步 + 有像素黑边**。

## 解决思路

两件事分开做：

1. **跟着走** → 3D `TextMeshPro`（非 UGUI）父挂到背板 Transform；不必再为该字配 `LivingTextWorldBinder`。
2. **不拧糊** → 物体放 `NoPixelSnap` 层，由 Overlay `UICamera` 画（Base 相机已 cull 该层；Selective Look 的 Snap 只打 Base）。

扫描线（稳定路径）：

- 世界：Base 相机 Selective Look = Snap + Scanline
- 文字：TMP Scanline 材质（`TableNineTmpScanline*`），参数由 `TableNineLookRig` 同步
- **不要**默认开「Overlay 末相机统一 scanline blit」（`overlayOwnsScanline` / `useUnifiedScanline`）——当前 URP 栈上不可靠，字会丢扫描线

描边：

- Bitmap 字挂 `TmpBitmapPixelOutline`（与主菜单「九宫牌局」同款），不是靠材质 `_OutlineWidth`

## 关键资产

| 角色 | 位置 |
|------|------|
| LookRig / 相机栈 | Main Camera → `TableNineLookRig`；子物体 `UICamera` + `TableNineFinalScanlineMarker` |
| Feature | `Assets/Settings/Renderer2D.asset` → TableNine Selective Look |
| Layer | `NoPixelSnap` |
| TMP 扫描线材质 | `Assets/Arts/VisualProfiles/TableNineTmpScanline*.mat` |
| 像素描边 | `Assets/Scripts/UI/TmpBitmapPixelOutline.cs` |
| 脚本目录 | `Assets/Scripts/UI/VisualLook/` |
| 原型参考 | `UITestSence` → `大盘构型0-主菜单/4/WorldTitle_Prototype` |

## 落地清单（新世界字）

场景改动走 **Unity MCP**，禁止手改 `.unity`。

1. 在目标背板下新建子物体，挂 **3D `TextMeshPro`**
2. Layer = `NoPixelSnap`；字体/材质对齐同构型 UGUI 字（多用 `TableNineTmpScanlineBitmapOutlined`）
3. 加 `TmpBitmapPixelOutline`（黑、1px、enabled），`ForceMeshUpdate` 后顶点约为可见字数 × 9 × 4
4. 字号用世界单位目视贴合；位置用背板 local，勿再写 Binder
5. 确认 Rig：`useUnifiedScanline = false`；Feature：`overlayOwnsScanline = false`
6. Play 截图验：清晰、有扫描线、有黑边、挪背板字跟着走

## 实验开关（默认关）

若以后要再试「栈末统一扫描线」：Feature `overlayOwnsScanline=true` + Rig `useUnifiedScanline=true`（TMP `_ScanlineEnabled=0`）。必须先截图像素验收整帧扫描线；失败立即关回稳定路径。

## 反模式

- 世界字放 Default 层却指望不糊 → 会被 Base Snap
- 只换材质、不加 `TmpBitmapPixelOutline` → Bitmap 丢黑边
- 手改 `.unity` / 用 Binder 绑已是背板子物体的世界字
- 打开统一 blit 未验收就批量关 TMP 扫描线
