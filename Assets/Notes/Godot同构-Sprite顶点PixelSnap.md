# Godot 同构 Pixel Snap（实验）

> 分支：`尝试用godot方式处理pixel-snap`  
> 目标：全屏不走 snap，素材（Sprite）自己 snap，文字（TMP）保持正常。

## 结论对应

Lost For Swords 的像素感来自**资产 + 整数布局**，不是全屏 UV 量化。Unity 侧同构：

| 层 | 做法 |
|----|------|
| 全屏 SelectiveLook Pass1 | `_PixelSnap = 0`（默认关） |
| Sprite | `TableNine/Sprite-Lit-PixelSnap` 顶点吸附到 960×540 格 |
| TMP / 文字 | 不换 snap 材质，继续吃全屏扫描线 |

## 关键资产

- `Assets/Arts/VisualProfiles/TableNinePixelSnapVertex.hlsl`
- `Assets/Arts/VisualProfiles/TableNineSprite-Lit-PixelSnap.shader`
- `Assets/Arts/VisualProfiles/TableNineSpriteLitPixelSnap.mat`（Renderer2D / 场景默认 Sprite 材质）
- `TableNineSpriteHitFlash` 同步带顶点 snap
- `TableNineLookRig`：`pixelSnap`（全屏）默认 0，`spritePixelSnap` 默认 1

## 暂不做

- SortingGroup / 牌堆排序细化
- 布局级 `RoundToPixelGrid` 脚本吸附
- Shader Graph 书材质（`M_BookPixelHue`）顶点 snap
