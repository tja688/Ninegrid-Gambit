# Mission: Pixel Snap that spares text

## Why
TableNine 要同时保住像素卡面的「硬边」和世界空间 TMP 小字的可读性。你已经在实验分支验证了「全屏不 snap、素材自己 snap」有效；下一步要把原理变成可诊断能力——以后再糊、再闪、再和扫描线打架时，能自己定位是哪一层在量化。

## Success looks like
- 能用一句话区分 **几何吸附（vertex snap）** 与 **采样吸附（UV / framebuffer snap）**，并指出各自打在谁身上
- 能解释为什么 SDF 小字吃全屏 UV snap 会糊，而同相机下的 Sprite 顶点 snap 不会
- 面对回归（字又糊 / 卡面又抖）时，能按「全屏 Look → Sprite 材质 → TMP 材质」三条路径排查

## Constraints
- 教学绑在本仓库已落地的实现上（`TableNineSprite-Lit-PixelSnap` + SelectiveLook），不另开引擎 demo
- 中文为主；公式与代码保留英文标识符
- 先不深挖 SortingGroup / 牌堆遮挡（你已明确暂缓）

## Out of scope
- 完整 CRT / 扫描线美学调参史
- Godot 引擎源码级 transform snap 实现细节（只作对照）
- 写一套新的低分 RT 分层渲染管线
