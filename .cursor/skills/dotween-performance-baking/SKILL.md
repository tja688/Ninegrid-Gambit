---
name: dotween-performance-baking
description: >-
  Translate a human's hand-authored DOTween Animation / DOTween Timeline
  performance (one core "slice", e.g. a single canonical direction or variant)
  into ONE pure-DOTween runtime black-box control script that reproduces the
  exact feel and exposes a parameter (direction / variant / N-way / arbitrary
  angle) to cover all the other cases the human did NOT hand-author. Use when
  the user has手搓好 an atomic 动效/反馈/编排 in DOTween Timeline and asks AI to
  翻译/聚合/烘焙/落地/做成运行时脚本, or mentions 方向路由 / 各个方向 / 黑盒动效脚本
  / 把 timeline 翻译成纯代码. Engine-general (Unity + DOTween); not tied to any
  one project.
---

# DOTween 表演烘焙（手搓切片 → 纯代码运行时黑盒）

## 先理解人想干什么（背景与诉求，最高优先级）

人**热爱手搓手感**：缓动曲线、时序、squash/punch、音效点、受击反馈，这些他用
DOTween Animation + DOTween Timeline 的可视化编辑器调到爽，并在编辑器/运行时反复
预览微调。这是他的创作方式，**不要试图替他改手感、也不要劝他放弃可视化工具**。

人的痛点只有一个：**可视化工具会把"方向/变体"和"手感"焊死在绝对关键帧里**，导致
每个方向（右/左/上/下，甚至 8 向、16 向、360°）都被迫手搓一遍，组合时爆炸。

人的诉求（你要落地的事）：

> 人只手搓**最核心的一份（或少数几份）切片**当"原子标准"。然后把场景、用途、可能
> 的方向/变体讲给你。**你负责把它翻译、聚合成一个纯 DOTween 的运行时"黑盒调控脚本"**，
> 内部按运行时传入的需求（哪个方向/哪种变体）路由到对应表演——而这些表演正是人在外部
> 手搓好的那一份的"忠实代码还原 + 参数化推广"。最后人会对这个脚本做单元测试/对照预览，
> 检验是否真实还原了他的摆设和手感。

一句话心智模型：**人手搓"形状与手感"（一份），你用代码把它"还原 + 沿参数推广"（全部）。**

## 黄金法则

1. **解耦"手感"与"方向/变体"。** 手感（曲线/时序/punch/闪白/音效）通常与方向无关，
   做一份通用；只有空间位移/朝向那一小层吃方向。先识别哪些层与方向无关，别把通用层
   也按方向复制。
2. **忠实翻译，不要重新设计。** 你的产物必须在基准切片上**逐参数还原**人手搓的值
   （duration / ease / 力度 / vibrato / delay / 颜色…），不是"差不多的另一版"。
   人会做单元测试比对，对不上就是失败。
3. **沿参数推广，别按个例写死。** 人只给了"右"和"上"，不代表你只写 4 个 case。优先
   把方向表达成**连续参数（角度/向量）**，用变换（旋转 Rig / 镜像 / 相对向量）覆盖任意
   方向，这样人将来扩到 8/16/360° **不需要你重写**。详见"方向/变体路由"。
4. **多了吃掉、少了去补、对不上就问。** 内核/数据给的超出表演所需就忽略；表演需要的
   数据没有就去上游补、**绝不在表现层造假**；映射有歧义、不确定舍弃哪种表现，用
   AskQuestion 让人拍板。

## 关键技术事实：DOTween Timeline 的内幕（决定翻译是否无损）

读过插件源码后已确认（任何项目都适用，除非插件版本差异很大）：

- **Timeline 在运行时就是一条原生 `DOTween.Sequence()`。** 它遍历同物体上的
  `DOTweenAnimation` / 实现 `IDOTweenAnimation` 的组件，逐个 `Sequence.Insert(0, tween)`。
  没有任何自研运行时系统。→ 翻译成纯代码 = 重建一条等价 `Sequence`。
- **Callback clip 是原生回调 + 一个 `UnityEvent`**：
  `DOTween.Sequence().InsertCallback(delay, () => onCallback.Invoke())`。
  → 在黑盒里用 `seq.InsertCallback(t, () => ...)` 平滑替代。
- **时间位置编码在每个 clip 自己的 `delay` 字段里**，不是 Insert 的位置参数（所有 clip
  都 `Insert(0,…)`，靠各自 `SetDelay/delay` 错开）。→ **还原时序必须逐个读 clip 的
  `delay`**，不要照搬它在 Timeline UI 上的视觉位置去猜。
- **Frame（开关物体/属性 scope）用原生 `OnComplete`/`OnRewind`** 双向开闭。→ 黑盒里
  用 `InsertCallback` 设置目标状态即可（若需倒放再补 `OnRewind`）。

两个必须告诉自己也告诉人的**坑**：

- **`UnityEvent` 内容不会自动搬过来。** callback 里指向的"放哪个音效 / 触发哪个受击
  脚本"，要在代码里**重新用 C# 直接调用**那些方法。人最清楚每个回调点干嘛，问清楚即可。
- **原生 `InsertCallback` 只在播放头正向越过时触发一次**，不在 rewind 触发。战斗反馈
  一般只正向播一次，不受影响；若某效果要可倒放，单独用 `OnRewind` 处理。

结论：**Timeline 退化为纯 authoring/预览工具，运行时完全可抛弃，迁移到纯 DOTween 无损。**

## 工作流（按需，别死板）

1. **听描述**：人会说——这是什么场景的动效（选中/受击/攻击/发牌补位…）、用什么做的
   （Animation/Timeline、是否带音效与受击反馈）、入口在哪、想表达什么、**有哪些方向/
   变体、他亲手做了哪几份当基准切片**。
2. **读切片**：去读人指定的那份手搓资产/物体，**逐 clip 提取**目标对象、tween 类型、
   duration、ease、力度/vibrato/elasticity、颜色、以及 `delay`（时序）；列出 callback
   点和它们各自触发什么。
3. **对齐映射**：逐条用 AskQuestion 确认歧义——哪些层与方向无关、哪个 callback 调什么、
   方向用"旋转整体"还是"镜像"还是"相对向量"、写死的坐标对应哪个锚点。**多余的剪掉、
   缺的请人补上游。**
4. **写黑盒**：产出一个纯 DOTween 的运行时调控脚本（形态见下），在基准切片上忠实还原，
   并把方向/变体做成参数化路由。
5. **交付给人验证**：明确告诉人怎么对照——播某个方向应与他手搓那份逐帧一致；非基准
   方向是基准的变换结果。配合人的单元测试。落地后触发引擎刷新、读 Console 排错。

## 方向 / 变体路由（核心：不要写死 N）

按"与方向的关系"分两类层，分别处理；二者常组合：

- **A. 参数化（位移/突刺/击退/朝向旋转等空间层）**——首选。把绝对终点改成
  `基准方向 × 距离`，方向是运行时传入的**向量或角度**：
  - `target.DOLocalMove(canonicalForward * distance, dur)`（而非写死终点）
  - `DOPunchPosition(dirVector, …)` / `DORotate(toAngle, …)`
  人调的是 `distance`/曲线，方向是数据。**支持任意角度，天然覆盖 8/16/360°。**
- **B. Rig 容器（复杂联动/整段 Timeline 重建）**——把表演永远在一个父节点
  `PerformanceRig` 的**本地空间**朝"基准方向"重建；播放前把 `Rig` 旋转到目标角度
  （或 `scale.x = -1` 做左右镜像）。**整段编排一份代码，任意方向白嫖。**

**强约束：不要用 `switch(4 个固定方向)` 这种把数量写死的写法**当唯一手段。即便人当前
只给 4 向，也要让接口能接受任意方向（角度/向量），内部用变换求解。若某些变体在美术
语义上**本就不是对方向的变换**（如"向上攻击"需独立预备姿势），用**可扩展的注册表/字典
按变体键路由**，对新增变体开放——而不是把变体数量焊死在 if/switch 里。

## 黑盒脚本应满足的契约

- 一个挂载式 `MonoBehaviour`（纯表现，不含游戏逻辑）。
- **对外入口可被代码和 UnityEvent 调用**：如 `Play(Vector2 direction)` /
  `Play(float angleDeg)` / `Play(string variant)` 以及无参/枚举重载，方便接线。
- 内部用 `DOTween.Sequence()` 重建编排；用 `Insert(t, …)` / `InsertCallback(t, …)`
  还原时序与回调；ease/力度/时长等**暴露为 `[SerializeField]`**，让人能继续微调。
- **可打断、可重入**：重播前 `Kill` 旧 tween（用 `SetTarget(this)` + `DOTween.Kill(this)`），
  缓存基线并在停止时还原（参考工程里既有原子脚本的 baseline/restore 模式）。
- 方向/变体作为参数贯穿，**不在内部硬编码个例数量**。
- 把人手搓 callback 指向的音效/受击脚本，用直接方法调用重新表达。

## 验证（人会做单元测试）

交付时说明对照方式，便于人验真：

- **基准方向逐参数一致**：黑盒播基准方向应与人手搓那份在时序、缓动、力度、回调时机上
  一致（可用 EditMode/PlayMode 测试断言关键参数，或运行时与 Timeline 并排预览比对）。
- **非基准方向是变换结果**：抽查若干角度，确认是基准的旋转/镜像/向量推广，无错位。
- 对不上 → 回到第 2 步重读 clip 参数修正，**不要"调一个差不多的"糊弄过去**。

## 边界 / 反模式

- 不替人重新设计手感；只忠实翻译 + 沿参数推广。
- 不写死方向数量；不用个例 switch 当唯一路由手段。
- 不在表现层造假数据；缺的去上游补，歧义用 AskQuestion 问。
- 不手改 `.unity`，相关改动走引擎的 MCP/正规流程。
- 保持**项目无关**：脚本不耦合特定游戏的内核类型；要接内核时，方向/触发由外部传入，
  黑盒只管"按参数还原表演"。这样它能跨项目复用。
- 不自动扩展到人没要求的表演；只接当前这一件作品。
