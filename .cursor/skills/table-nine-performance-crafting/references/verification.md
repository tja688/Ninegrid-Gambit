# 验证与交付

Performance 落地后，向人说明如何验真。人会配合单元测试 / PlayMode 并排预览。

## 按路由的验证标准

### Route A / B — 可逐参数对照

- **基准方向/变体逐参数一致**：黑盒播基准应与源（Timeline 或脚本）在时序、缓动、力度、回调时机上一致
- **非基准方向是变换结果**：抽查若干角度，确认是基准的旋转/镜像/向量推广，无错位
- 手段：EditMode 断言关键参数 / PlayMode 与源并排预览 / 人眼逐帧比对
- **对不上** → 回到读基准步骤重读 clip/源码，**不要「调一个差不多的」**

### Route C — 人标注验收

- 人提供验收帧或时间点 + 可接受误差范围
- AI 交付时标注 **「非逐帧无损」**
- PlayMode 人眼验收；不通过则调 exposed 参数，不擅自改验收标准

## 交付清单（每次落地须说明）

1. **基准视觉来源**（路由 A/B/C + 路径）
2. **Play 入口** — RuntimeChannel 签名与 PreviewChannel 入口（ContextMenu 名）
3. **预览方式** — 需要关联什么模板/锚点；`EnsurePreviewActors` 行为简述
4. **播放安全** — 是否 defer / Pause；见 [`playback-safety.md`](playback-safety.md)
5. **对照步骤** — 例如：「PlayMode 中 PlayPreview，基准方向应与 XXX Timeline 一致；Rotate Rig 45° 抽查」
6. **已知局限** — 如 Route C 误差、尚未实现的预览自给项

## 开发预览 vs 验收对照

| | 开发预览 | 验收对照 |
|--|----------|----------|
| 目的 | 调参、快速迭代 | 证明忠实复刻 |
| 通道 | PreviewChannel | 与原始源并排 |
| Route A | 可用 | 必须 clip 级一致 |
| Route C | 可用 | 按人标注标准，非 clip 级 |

## 单元测试建议（人可选做）

- 断言 `ComputeTotalDuration(n)` 与手工计算一致
- 断言 Sequence 构建后 tween 数量、首个 Insert 时间、duration/ease 与基准 clip 一致
- PlayMode：播完后 `StopAndRestore` 演员位置/材质回到基线

## 落地后工程检查

- 触发 Unity 刷新，读 Console 无编译错误
- 若改了 DOTween 相关代码，PlayMode 冷启动场景复现一次（验证首帧尖峰已处理）
