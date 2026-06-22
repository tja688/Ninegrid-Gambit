---
name: table-nine-performance-crafting
description: >-
  Craft TableNine/NineGrid Performance black-boxes from any verifiable visual
  source: DOTween Timeline baking, existing script replication, or external
  reference visuals. Produces parameterized *Performance scripts with preview
  and runtime channels. Use when the user asks to 烘焙/翻译/复刻/做成 Performance/
  参数化表演/预览通道/把 timeline 或已有动效落地为黑盒, or mentions 方向路由 /
  黑盒动效脚本 / 表演器. Unity; output lands in
  Assets/Scripts/NineGrid.Presentation/Performance.
---

# TableNine Performance 通用工作流

**心智模型：已有可验证视觉 = 形状与手感标准；AI 负责忠实复刻 + 沿参数推广，产出自带预览通道的 `*Performance` 黑盒。**

人手搓/已有可验证视觉（Timeline、脚本、录屏、设计稿……）是创作方式，**不要试图替人改手感、也不要劝人放弃可视化工具**。你的工作是把它翻译成运行时黑盒，并沿参数推广到全部方向/变体。

## Required Context

做 Performance 前，若涉及联动或多演员调度，先读：

- 四盒子总览 — [`Assets/Notes/表现层方法论.md`](Assets/Notes/表现层方法论.md)
- 内核接入（盒子④）— [`.cursor/skills/table-nine-adapter-crafting/SKILL.md`](../table-nine-adapter-crafting/SKILL.md)

**禁止在 Performance 里写 `PresentationBatch` / 内核事件消费逻辑。** 接入交给 adapter skill。

落地前读 `rules.md`；不手改 `.unity`；代码落地后触发 Unity 刷新并读 Console。

## 黄金法则

1. **解耦手感与方向/变体。** 手感（曲线/时序/punch/闪白/音效）通常与方向无关，做一份通用；只有空间位移/朝向那一小层吃方向。
2. **忠实翻译，不要重新设计。** 产物必须在基准视觉上**逐参数还原**（duration / ease / 力度 / vibrato / delay / 颜色…），不是"差不多的另一版"。
3. **沿参数推广，别按个例写死。** 优先把方向表达成**连续参数（角度/向量）**，用 Rig 旋转/镜像/相对向量覆盖任意方向。详见 `references/dotween-timeline-route.md` 方向路由节。
4. **多了吃掉、少了去补、对不上就问。** 表演所需数据没有就去上游补，**绝不在表现层造假**；歧义用 AskQuestion 让人拍板。
5. **预览自给，运行时等待。** 表演器必须能独立呈现自己的视觉；正式运行时通道不依赖外部导演喂「当前有哪些卡 uid」这类业务状态（锚点等场景引用可关联）。

## 通用工作流（5 步）

1. **听描述** — 场景、用途、变体/参数、**基准视觉在哪、怎么验证**
2. **识别来源路由** — 见 [`references/source-routes.md`](references/source-routes.md)
3. **读基准 & 对齐映射** — AskQuestion 消歧；列出 callback/依赖/锚点
4. **写 Performance 黑盒** — 落盘 `Assets/Scripts/NineGrid.Presentation/Performance/`，类名 `*Performance`，命名空间 `NineGrid.Presentation.Performance`
5. **交付验证** — 基准对照 + 预览通道说明；见 [`references/verification.md`](references/verification.md)

## 来源路由（速查）

| 路由 | 基准视觉 | 细则 |
|------|----------|------|
| **A** DOTween Timeline + Animation | 场景内手搓 Timeline/Animation | [`references/dotween-timeline-route.md`](references/dotween-timeline-route.md) |
| **B** 现有脚本复刻 | 项目内 MonoBehaviour / Coroutine / Animator | [`references/source-routes.md`](references/source-routes.md) §B |
| **C** 外部参考视觉 | 录屏、截图、别引擎、Web/CSS、设计稿 | [`references/source-routes.md`](references/source-routes.md) §C |
| **D** 预留 | 新来源只加路由条目 | [`references/source-routes.md`](references/source-routes.md) §D |

## Performance 产出契约

### 落盘与命名

- 路径：`Assets/Scripts/NineGrid.Presentation/Performance/`
- 类名后缀 `Performance`（如 `CardDeckEntryPerformance`）
- 命名空间：`NineGrid.Presentation.Performance`
- 勿放到 `AtomicRepresentationTools` 等目录；勿用 `Tween` / `Effect` / `Player` 后缀

### 脚本形态

- 挂载式 `MonoBehaviour`，纯表现，不含游戏逻辑
- 对外 `Play(...)` 多重重载（方向/变体/演员列表等），可被 UnityEvent 调用
- 手感参数暴露为 `[SerializeField]`，人可继续微调
- **可打断、可重入**：`SetTarget(this)` + `DOTween.Kill(this)`；缓存基线，`StopAndRestore` 还原
- 方向/变体作为参数贯穿，**不在内部硬编码个例数量**
- callback 里的音效/受击反馈用 C# 直接调用重新表达

### 双通道

```text
RuntimeChannel  — 外部（导演/适配器）调用 Play(params)，传入真实演员与参数
PreviewChannel  — ContextMenu / Inspector 按钮；表演器自行 EnsurePreviewActors()
```

预览契约详见 [`references/preview-contract.md`](references/preview-contract.md)。

### 播放安全（DOTween 必遵）

凡用 `DOTween.Sequence` 且存在「同帧重对象生成 + 立即播放」，必须遵守 [`references/playback-safety.md`](references/playback-safety.md)。

工程范例：[`CardDeckEntryPerformance.cs`](../../Assets/Scripts/NineGrid.Presentation/Performance/CardDeckEntryPerformance.cs)（`deferPlayOneFrame` + Sequence `.Pause()` 后 `.Restart()`）。

## 与相邻 skill 的边界

| Skill | 盒子 | 分工 |
|-------|------|------|
| **本 skill** | ③ Performance | 视觉复刻 + 参数化 + 预览 |
| `table-nine-adapter-crafting` | ④ Director | 内核事件 → 调 `Performance.Play(真实演员)` |
| `表现层方法论.md` | ①②③④ 总览 | 锚点/演员/表演/导演四盒子 |

## 反模式 / 注意

- 不替人重新设计手感；只忠实翻译 + 沿参数推广
- 不写死方向数量；不用 4 向 switch 当唯一路由
- **预览不是验收替代品** — Route C 无法逐 clip 无损；人须标注验收标准
- **EditMode 预览局限** — DOTween/Coroutine 忠实预览在 **PlayMode**；EditMode 只适合参数校验
- **预览自动生成有上限** — 只生成「能看懂运动」的最小占位，不要求预览复刻正式卡牌美术
- **CardDeckEntry 现状** — 播放安全已达标；`previewCards` 依赖场景预摆，预览自给待后续迭代
- **smoothDeltaTime vs defer** — defer 是根治首帧尖峰，smooth 是项目级兜底，二者不互相替代
- **Route B 边界** — 源脚本若已是 DOTween 但散落多 MonoBehaviour，合并进单一 Performance，不复制 Adapter 调度逻辑
- 不自动扩展到人没要求的表演；只接当前这一件作品

## References

| 文件 | 内容 |
|------|------|
| [`references/source-routes.md`](references/source-routes.md) | 各路由 intake 清单 |
| [`references/dotween-timeline-route.md`](references/dotween-timeline-route.md) | Timeline 翻译、方向 Rig 路由 |
| [`references/preview-contract.md`](references/preview-contract.md) | PreviewChannel vs RuntimeChannel |
| [`references/playback-safety.md`](references/playback-safety.md) | 首帧 delta 尖峰机理与破解 |
| [`references/verification.md`](references/verification.md) | 对照验证与交付说明 |
