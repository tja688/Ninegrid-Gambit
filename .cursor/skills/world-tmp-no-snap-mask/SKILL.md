---
name: world-tmp-no-snap-mask
description: >-
  Land world-space TextMeshPro that stays crisp under a full-screen PixelSnap
  post-process, still receives scanlines, and occludes correctly with sprites
  via sorting layers. Prefer same-camera NoSnap mask over Overlay UICamera.
  Use when fixing snap-blurred world text, card/book text poke-through, dual-
  camera occlusion bugs, or when the user mentions 世界字/不吃snap/吃扫描线/
  自然遮挡/NoSnapMask/单相机遮罩.
---

# 世界 TMP：同相机 NoSnap 遮罩

把「逃 PixelSnap」「吃扫描线」「与精灵自然遮挡」解耦：字留在 Base 相机里，用遮罩豁免 Snap，扫描线交给全屏 Pass。

## 钥匙句

逃 Snap 靠「换相机」和遮挡靠「同相机排序」互斥——保遮挡就把字留在 Base，逃 Snap 改用 NoSnap 遮罩。

## 问题与失败路径

全屏 PixelSnap 是屏幕空间后处理：把整张颜色缓冲拧到像素格，**不认物体**。字被拧糊与物体是谁无关。

| 做法 | 清晰 | 遮挡 | 说明 |
|------|------|------|------|
| Overlay / 盖层 UI | ✓ | ✗ | 难做卡面字；控制成本高 |
| Base + Overlay `UICamera` 专画「免 Snap 层」 | ✓ | ✗ | 字在后处理之后另画 → 永远盖最上、叠卡穿透 |
| **同相机 + NoSnap 遮罩（默认）** | ✓ | ✓ | 字参与 Sorting Layer；Snap 按遮罩跳过拧格 |

根因：用 Overlay 逃 Snap，等于把字踢出世界画家序。

## 默认解法

1. **遮挡**：世界空间 3D `TextMeshPro`（网格）挂在载体 Transform 下；Layer 标为「免 Snap」层；**Base 相机 culling 包含该层**；关掉/移出专画该层的 Overlay 文字相机。
2. **逃 Snap**：Renderer Feature 在透明之后用 `RendererList` 把免 Snap 层重绘进 `_NoSnapMask`；Snap Pass 对 mask>0 的像素用原 UV，其余走 snap UV。
3. **扫描线**：字已在 Base 颜色缓冲里 → 全屏 Scanline Pass 统一打；关掉 TMP 材质自带扫描线，避免叠双份。

### Snap 伪代码

```hlsl
protect = sample(_NoSnapMask, uv).r;          // 需 _UseNoSnapMask
sampleUv = lerp(SnapUv(uv), uv, protect);
return tex2D(_BlitTexture, sampleUv);
```

### Mask Pass 要点（URP RenderGraph）

- 时机：透明物体画完之后、Snap blit 之前
- RT：全屏 `R8`，清黑；`SetGlobalTextureAfterPass` → `_NoSnapMask`
- 过滤：`FilteringSettings` 只含免 Snap 层
- ShaderTag：覆盖 `Universal2D`、`SRPDefaultUnlit`、`UniversalForward`（TMP 常无 LightMode → 走 `SRPDefaultUnlit`）
- `overrideMaterial`：遮罩多为**整字 quad**（绑不上 font atlas）——防拧糊够用；精确字形是优化项

## 落地清单

在目标仓库里先搜：`_NoSnapMask`、`buildNoSnapMask`、`NoPixelSnap`、LookRig / SelectiveLook 一类装配入口。

- [ ] 字是 **3D TMP 网格**，父挂载体；靠 Transform 跟随，勿再投影 Binder
- [ ] Layer = 免 Snap 层；MeshRenderer 的 **Sorting Layer / Order** 与卡面/书一致
- [ ] Feature：`buildNoSnapMask`（或等价）= on，且已挂 mask 材质
- [ ] Rig / 相机：单相机模式 on → Base 含该层；旧 Overlay 文字相机 disabled 且不在 stack
- [ ] TMP `_ScanlineEnabled` = 0（全屏 Pass 负责）；分辨率等参数仍可同步
- [ ] Bitmap 像素描边若需要：挂八向顶点描边组件，勿只靠材质 `_OutlineWidth`
- [ ] Play 验收：清晰、有扫描线、低排序字被盖住、高排序字可见

验收模板（叠书/叠卡）：低 Sorting 字应不可见，高 Sorting 字可见；二者都清晰且扫描线相位与世界一致。

## 回退（旧双相机）

仅当遮罩路径不可用时：Base cull 免 Snap 层 + Overlay 专画该层；扫描线改由 TMP 材质补。接受遮挡失效。不要与单相机 mask 同时开。

## 反模式

- 用 Overlay 逃 Snap 却要求卡叠卡遮挡
- 字在 Default 层却指望不糊（会被全屏 Snap）
- 开 mask / 单相机后仍让 TMP 自带扫描线 → 双份压暗
- 只换材质、不加像素描边组件 → Bitmap 丢黑边
- 手改 `.unity`（有 Unity MCP 时走 MCP）
- 已是载体子物体的世界字再绑投影跟随
