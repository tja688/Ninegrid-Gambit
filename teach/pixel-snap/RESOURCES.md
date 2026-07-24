# Pixel Snap Resources

## Knowledge

- [Unity Manual: Pixel Perfect Camera component reference (URP)](https://docs.unity3d.com/Manual/urp/2d-pixelperfect-ref.html)
  官方说明 Pixel Snapping：在**渲染时**按 PPU 网格吸附，不改 Transform。用来对照「引擎提供的几何/相机级吸附」与你们自定义的材质顶点吸附。
- [TextMesh Pro: About SDF fonts](https://docs.unity3d.com/Packages/com.unity.textmeshpro@4.0/manual/FontAssetsSDF.html)
  SDF atlas 存的是到字形边缘的距离场，边缘靠连续采样做 AA。用来理解：为什么把屏幕 UV 折到低分辨率格会毁掉小字。
- [TextMesh Pro: Distance Field shaders](https://docs.unity3d.com/Packages/com.unity.textmeshpro@3.2/manual/ShadersDistanceField.html)
  Softness / Scale 如何从距离场造出锐利或柔边。用来联系「糊」到底是距离场被错误采样，还是后处理在量化。
- [Godot ProjectSettings: `rendering/2d/snap/snap_2d_transforms_to_pixel`](https://docs.godotengine.org/en/stable/classes/class_projectsettings.html)
  Godot 官方项目级 2D transform snap 开关。用来对照「布局/变换取整」思路，而不是全屏 blit 量化。
- [Godot forum: Snap 2D Vertices vs Transforms](https://forum.godotengine.org/t/difference-between-snap-2d-vertices-to-pixel-and-snap-2d-transforms-to-pixel/78642)
  维护者说明：4.x 应优先用 Transforms snap；Vertices snap 是早期方案。帮助把「顶点 snap」放进正确的引擎语义里。
- [d7samurai: pixel art antialiasing (gist)](https://gist.github.com/d7samurai/9f17966ba6130a75d1bfb0f1894ed377)
  高信号说明：nearest + 亚像素运动为何抖；以及「在采样处修」与「在几何处修」是不同问题。作延伸，非本课必读。

## Wisdom (Communities)

- [Unity Discussions — 2D / URP](https://discussions.unity.com/)
  像素完美与 Cinemachine / PPC 打架的实测帖较多。Use for: 相机 ortho 与 reference resolution 对齐问题。
- [r/Unity2D](https://www.reddit.com/r/Unity2D/)
  像素风落地经验杂、信号一般。Use for: 现象对比，不作为原理权威。

## Gaps

- 尚无一篇把「全屏 UV quantize vs 顶点 clip-space snap vs SDF」三者对照写透的官方长文；本课用仓库实现 + 上述主源拼出模型。
