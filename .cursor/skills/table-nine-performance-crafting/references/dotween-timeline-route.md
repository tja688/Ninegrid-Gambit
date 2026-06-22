# Route A — DOTween Timeline + Animation 翻译

本路由是通用 Performance 工作流的一个来源分支。产出契约与预览/播放安全见主 `SKILL.md` 及同目录其他 reference。

## 背景与诉求

人**热爱手搓手感**：缓动曲线、时序、squash/punch、音效点、受击反馈，用 DOTween Animation + DOTween Timeline 调到满意。这是创作方式，**不要劝他放弃可视化工具**。

痛点：**可视化工具把「方向/变体」和「手感」焊死在绝对关键帧里**，每个方向被迫手搓一遍。

解法：人只手搓**最核心的一份（或少数几份）切片**当原子标准；AI 翻译成纯 DOTween 运行时黑盒，**忠实还原 + 沿参数推广**。

## 关键技术事实：DOTween Timeline 的内幕

读过插件源码后已确认（任何项目都适用，除非插件版本差异很大）：

- **Timeline 在运行时就是一条原生 `DOTween.Sequence()`。** 它遍历同物体上的 `DOTweenAnimation` / 实现 `IDOTweenAnimation` 的组件，逐个 `Sequence.Insert(0, tween)`。没有任何自研运行时系统。→ 翻译成纯代码 = 重建一条等价 `Sequence`。
- **Callback clip 是原生回调 + 一个 `UnityEvent`**：`DOTween.Sequence().InsertCallback(delay, () => onCallback.Invoke())`。→ 在黑盒里用 `seq.InsertCallback(t, () => ...)` 平滑替代。
- **时间位置编码在每个 clip 自己的 `delay` 字段里**，不是 Insert 的位置参数（所有 clip 都 `Insert(0,…)`，靠各自 `SetDelay/delay` 错开）。→ **还原时序必须逐个读 clip 的 `delay`**，不要照搬 Timeline UI 上的视觉位置去猜。
- **Frame（开关物体/属性 scope）用原生 `OnComplete`/`OnRewind`** 双向开闭。→ 黑盒里用 `InsertCallback` 设置目标状态即可（若需倒放再补 `OnRewind`）。

### 两个坑

- **`UnityEvent` 内容不会自动搬过来。** callback 里指向的音效/受击脚本，要在代码里**重新用 C# 直接调用**。问清楚每个回调点干嘛。
- **原生 `InsertCallback` 只在播放头正向越过时触发一次**，不在 rewind 触发。战斗反馈一般只正向播一次；可倒放效果单独用 `OnRewind`。

**结论：Timeline 退化为纯 authoring/预览工具，运行时完全可抛弃，迁移到纯 DOTween 无损。**

## Route A 工作步骤

1. **读切片**：读人指定的手搓资产/物体，**逐 clip 提取**目标对象、tween 类型、duration、ease、力度/vibrato/elasticity、颜色、`delay`；列出 callback 点及触发内容。
2. **对齐映射**：AskQuestion — 哪些层与方向无关、callback 调什么、方向用 Rig 旋转/镜像/相对向量、写死坐标对应哪个锚点。
3. **写黑盒**：`DOTween.Sequence()` 重建；`Insert(t, …)` / `InsertCallback(t, …)` 还原时序；遵守 [`playback-safety.md`](playback-safety.md)。
4. **验证**：见 [`verification.md`](verification.md)。

## 方向 / 变体路由（不要写死 N）

按「与方向的关系」分两类层，常组合使用：

### A. 参数化（空间层）— 首选

把绝对终点改成 `基准方向 × 距离`，方向是运行时传入的**向量或角度**：

- `target.DOLocalMove(canonicalForward * distance, dur)`
- `DOPunchPosition(dirVector, …)` / `DORotate(toAngle, …)`

人调 `distance`/曲线，方向是数据。**支持任意角度，覆盖 8/16/360°。**

### B. Rig 容器（复杂联动）

表演在父节点 `PerformanceRig` 的**本地空间**朝基准方向重建；播放前把 Rig 旋转到目标角度（或 `scale.x = -1` 镜像）。**整段编排一份代码，任意方向白嫖。**

### 强约束

- **不要用 `switch(4 个固定方向)` 当唯一手段。** 接口须接受任意方向（角度/向量），内部用变换求解。
- 变体在美术语义上**不是方向的变换**（如「向上攻击」需独立预备姿势）时，用**可扩展注册表/字典按变体键路由**，不把变体数量焊死在 if/switch 里。

## 与播放安全的关系

Timeline 编辑器预览场景通常已 warm，首帧 delta 正常；烘焙后的代码若在「同帧实例化多对象 + 立即 Play」场景运行，可能踩 [`playback-safety.md`](playback-safety.md) 的首帧尖峰坑。**这不是翻译错误，是运行时上下文差异。**
