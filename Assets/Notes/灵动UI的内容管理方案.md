我认真看了你的架构文档 ./灵动UI架构-spec-2026-07-17.md 。这个问题其实已经自然地长在现有架构里了：你已经明确区分了世界视觉层与屏幕文字层，也预留了 `HideDuringTransit / FixedSlotSwap / TravelWithCarrier` 三类内容策略；随行内容还会反向抬高面板的流动下限。  

我认为现在缺的不是某一种动画，而是夹在 `TransitionPlan` 和最终渲染之间的一层：

# **内容编舞层 Content Choreographer**

它不决定面板怎么走，也不重新规划布局，只读取已经确定的面板时空轨迹，然后生成：

* 内容什么时候开始退让
* 从哪个方向收缩
* 哪些信息先消失
* 哪些信息必须长存
* Canvas 内容使用什么裁剪窗口
* 快速改道时如何连续重定向

---

# 一、你的“边界驱动收缩”方向是对的，但不要直接缩放内容

最容易落地、但也最容易变难看的方案是：

```text
面板宽度变小
→ 内容 RectTransform 跟着变小
→ TMP 自动换行/缩字号
→ 最后 SetActive(false)
```

这会产生几个问题：

1. 文本不断重排、换行、跳行。
2. 字号或 Transform Scale 持续变化，清晰度和像素感不稳定。
3. 面板快速 A→B→C 改道时，文本突然重新展开或重新排版。
4. 所有内容同时缩成一团，没有信息层级。
5. 面板只是平移时，内容也可能被无意义地淡出。

所以我建议把“收缩”重新定义成：

> **不是内容几何尺寸真的一直缩小，而是内容对边界压力作出退让。**

这种退让可以同时包含四个通道：

```text
边界压力
├─ Clip：边界像窗口一样逐渐吞掉内容
├─ Offset：内容轻微向安全方向退让
├─ Alpha：被吞边缘附近逐渐衰减
└─ Density：从完整信息降级到紧凑信息，再隐藏
```

真正的主体效果应该是 **Clip + 少量位移**，Alpha 只是润色，而不是纯淡出。

---

# 二、核心数学：把面板边界变成一个“压力场”

对于每个内容元素，预先定义一个内容包络 `E`，例如：

```text
标题 Full Envelope
描述 Full Envelope
描述 Compact Envelope
图标 Minimum Envelope
```

面板在任意时刻有一个内部安全矩形：

```text
P(t) = Inset(PanelRect(t), ContentPadding)
```

内容包络与四条边的余量为：

```text
leftClearance   = E.xMin - P.xMin
rightClearance  = P.xMax - E.xMax
bottomClearance = E.yMin - P.yMin
topClearance    = P.yMax - E.yMax
```

但不能只看“当前是否碰撞”，否则反应会太迟。应该加入一个很短的前瞻窗口，例如未来 80–150ms：

```text
predictedClearance(edge, t)
    = min clearance(edge, u)
      where u ∈ [t, t + lookAhead]
```

然后映射为压力：

```text
pressureEdge =
    1 - SmoothStep(hideDistance, safeDistance, predictedClearance)
```

最终压力可以是：

```text
pressure = max(left, right, top, bottom)
```

更重要的是，你同时知道**哪一条边正在逼近**，内容就可以有方向感：

* 右边界逼近：文字从右往左被吞，整体微微向左退。
* 下边界逼近：描述先向上收，底部行先消失。
* 左右同时逼近：进入紧凑内容形态，而不是疯狂横向压缩。
* 四边都逼近：保留图标或核心符号，然后完全隐藏。

这会让玩家感觉是“面板空间在挤压内容”，而不是内容突然收到一个隐藏命令。

---

# 三、不要只有“显示/隐藏”，而要有内容密度阶梯

我建议每个功能面都定义几个有限的内容等级：

```text
Level 3：Full
标题 + 描述 + 辅助值 + 装饰

Level 2：Compact
标题 + 一行短描述或核心数值

Level 1：Identity
图标、短标题、数字、状态符号

Level 0：Concealed
完全隐藏，但内容状态仍存在
```

例如你的卡组入口：

```text
完整：
构筑名称
当前卡组拥有 18 张牌
悬停查看完整卡组

紧凑：
构筑名称
18

最小：
卡组图标 + 18

隐藏：
无内容，仅保留面板背板
```

这些不是运行时自动生成，而是和大盘构型一样，由美术或设计明确授权。

这样面板变小时，不是连续压缩文本，而是：

```text
Full
→ 辅助描述被边界吞掉
→ 与 Compact 短暂交叉淡化
→ Compact 被继续吞掉
→ Identity
→ Hidden
```

恢复时不必机械地完全倒放，可以：

```text
Identity 先出来
→ 标题出来
→ 描述最后展开
```

这通常比严格时间倒放更有生命感。

为了防止面板尺寸在阈值附近抖动，应使用迟滞：

```text
Full → Compact：可用宽度低于 180
Compact → Full：可用宽度重新高于 196
```

进入和退出阈值不能相同。

---

# 四、Canvas/TMP 文字：最佳方案是“文字不动，裁剪窗跟着面板边界动”

这部分正是你架构里的关键。

你现在的原则是屏幕 TMP 使用固定内容槽，不逐帧追逐世界背板。

我建议继续坚守这个原则：

```text
TextOverlay
└─ ContentSlot_PlayerInfo       // 固定位置
   ├─ TMP_Title                 // 不跟面板追踪
   ├─ TMP_Body
   └─ ContentClipDriver         // 控制裁剪，不控制布局
```

每帧做的不是“把 TMP 移到面板上”，而是：

1. 从同一份 `TransitionPlan` 采样当前面板 Rect。
2. 把世界 Rect 投影到 Canvas 空间。
3. 和固定 `ContentSlotRect` 求交集。
4. 将交集作为当前文字的可见裁剪窗口。

```text
PanelScreenRect(t)
        ∩
FixedContentSlotRect
        =
ContentApertureRect(t)
```

于是面板右边界向左收时，文字会自然地从右往左消失；面板重新延伸时，文字又从边界中被释放出来。

Unity 的 `RectMask2D` 可以把子元素限制在一个矩形内，而且矩形裁剪不需要模板缓冲和额外材质切换；如果需要更直接地保持 TMP Transform 不动，也可以通过 CanvasRenderer 的矩形裁剪能力或一层自定义 UI Clip Driver 更新 Canvas 空间裁剪矩形。([Unity 文档][1])

`CanvasRenderer.clippingSoftness` 提供的是以像素为单位的线性 Alpha 裁剪软边，可以给边界增加一小段过渡区。([Unity 文档][2])

不过你的 UI 是像素化方向，我不建议做十几像素的柔和模糊。更适合的是：

```text
1–2 个逻辑像素：硬裁剪
接下来 2–4 个像素：Alpha 或抖动式消隐
```

也就是看起来有缓冲，但依然保持像素边缘。

## TMP 这里尤其不要做的事情

不要在面板转场期间不断修改：

* `fontSize`
* TMP 容器宽度
* 自动字号范围
* 行距
* 自动换行宽度

这会让 TMP 反复重新生成排版。

正确做法是：

```text
文字排版保持终态尺寸
只移动裁剪窗口
必要时切换 Full / Compact 预排版版本
```

TMP 本身具备 Masking 等溢出模式；需要立即获得更新后的文本几何时，也提供 `ForceMeshUpdate`。但更理想的是在构型烘焙阶段就保存内容包络，而不是每次转场临时测量。([Unity 文档][3])

---

# 五、内容不能和面板分别开两条 Tween

这一点非常重要。

不要：

```text
PanelTween.Play()
ContentTween.Play()
```

否则快速 Hover 改道时，两边很容易出现：

* 内容已经开始展开，面板却正在重新收缩。
* 面板速度连续，内容 Alpha 从零速度重新启动。
* 旧 Tween 回调把新内容隐藏。
* 两条曲线时长略有差异，长期积累错位。

正确结构应该是：

```text
TransitionPlanner
    ↓
TransitionPlan
    ├─ CarrierRectTracks
    ├─ Timing / Generation
    └─ SemanticMarkers
             ↓
ContentChoreographer
             ↓
ContentProjectionPlan
    ├─ Clip tracks
    ├─ Alpha tracks
    ├─ Offset tracks
    ├─ Density transitions
    └─ Travel tracks
```

运行时只有一个时间源：

```text
sample = plan.Sample(time)

WorldCarrierProjection.Apply(sample)
CanvasContentProjection.Apply(sample)
```

这样内容和面板永远是同一份计划的两个投影结果。

这也符合你已有的固定身份载体、独立屏幕内容槽和 generation-stamped plan 方向。

---

# 六、“长存内容跟着面板移动”应该单独定义为虚拟随行

你提到的这个需求不能完全用固定槽解决。

我建议把它定义成少数内容才拥有的：

```csharp
ContentPolicy.TravelWithCarrier
```

但要区分世界内容和 Canvas 内容。

## 1. 世界空间随行内容

这类最简单，但不要让它继承面板的尺寸缩放。

推荐层级稍微调整为：

```text
CarrierRoot
└─ LayoutFrame                 // 只负责中心位置/基础姿态
   ├─ PanelGeometryFrame       // 宽高、皮肤、Mask
   ├─ WorldContentFrame        // 跟位置，不跟宽高缩放
   └─ ImpactFrame
```

核心是：

> 面板形变最好通过宽高参数、Sliced Sprite 尺寸或顶点几何实现，而不是缩放整个父 Transform。

这样内容可以随 `LayoutFrame` 平移，但保持自己的尺寸。

如果世界内容必须始终可见，就把它的 Envelope 加入 Planner：

```text
effectiveMinWidth =
    max(panelFlowMinWidth,
        contentEnvelope.width + padding)

effectiveMinHeight =
    max(panelFlowMinHeight,
        contentEnvelope.height + padding)
```

这正是你 Spec 里“随行内容抬高载体尺寸下限”的含义。

## 2. Canvas TMP 随行内容

这里不要用：

```csharp
LateUpdate()
{
    text.position = Camera.WorldToScreenPoint(panel.position);
}
```

这就是你想避免的“追背板”。

应该用**虚拟附着**：

```text
不是读取面板 Transform
而是内容与面板共同采样 TransitionPlan
```

例如：

```csharp
Rect carrierRect = transitionPlan.SampleCarrier(carrierId, time);
Vector2 worldAnchor = carrierRect.GetNormalizedPoint(anchor);
Vector2 screenAnchor = ProjectToCanvas(worldAnchor);

contentSlot.SetPosition(screenAnchor);
```

Unity 提供世界点到屏幕点以及 RectTransform 屏幕/局部空间转换工具；Overlay Canvas 的相关转换不需要 Canvas Camera。([Unity 文档][4])

这虽然也是逐帧设置位置，但它不是“追踪另一个 Transform”，而是：

```text
面板投影 = Sample(plan)
文字投影 = Sample(plan)
```

所以不会有 LateUpdate 顺序差、像素吸附差异或 Tween 进度不一致。

## 3. 随行到固定槽的交接

长存内容经常不是永久跟随，而是：

```text
随面板移动一段
→ 到达某一区域
→ 稳定停靠到固定内容槽
```

建议做一个短交接窗口：

```text
Travel Anchor
     ↓
Handoff Corridor
     ↓
Fixed Slot
```

在交接窗口内：

* Position 从面板锚点过渡到固定槽锚点。
* Clip 从面板裁剪切换到槽裁剪。
* 信息状态保持同一个 Content Instance。
* 不 SetActive，不重新创建文本。

从玩家视觉上看，它会像内容“被面板送到了新位置”。

---

# 七、边界逼近时，具体应该怎么动

一个较稳定的默认组合是：

```text
0%–20% 压力
完全不动

20%–50%
内容向内退 2–6 px
外围装饰开始消退

35%–70%
裁剪窗口开始吞入
描述 Alpha 下降
切换 Compact 的准备阶段

60%–90%
标题仍保留
描述已经隐藏
主体轻微向安全方向集中

90%–100%
标题被裁剪吞没
CanvasRenderer cull / 禁用交互
```

文本本身不建议明显 Scale。最多：

```text
图标、装饰：1.00 → 0.92
文本：1.00 → 0.98，甚至完全不缩放
```

文字的“收缩感”应主要来自：

* 被边界裁切
* 行或信息层级减少
* 向安全区轻移
* 字符/行的轻微错峰消隐

而不是字号缩小。

---

# 八、快速改道是这套方案最需要防守的地方

假设：

```text
A → B
执行到 42%
突然 Hover 到 C
```

内容不能重新从 `Visible` 或 `Hidden` 状态开始。

新内容计划的初始状态应为当前实况：

```text
ContentLiveState
{
    clipRect;
    clipVelocity;
    alpha;
    alphaVelocity;
    offset;
    offsetVelocity;
    densityLevel;
    activeVariantBlend;
}
```

然后新计划从当前值和当前速度重定向。

特别是 Clip Rect 的四条边，也要保留当前速度：

```text
left, right, top, bottom
```

否则面板保持 C1 连续，但文字裁剪边界在改道瞬间停一下再反向，依然会露馅。

语义等级不适合携带“速度”，所以处理方式不同：

* Full 与 Compact 正在交叉淡化时，保留当前 Blend。
* 新计划重新决定 Blend 目标。
* 不重新从 0 或 1 开始。
* 旧 generation 的完成回调必须丢弃。

---

# 九、内容策略最好从三种扩展为五种

你 Spec 当前的三种策略是很好的主干，我会稍微细化成：

```csharp
enum ContentProjectionPolicy
{
    HideDuringTransit,

    // 固定槽，面板边界作为裁剪窗
    FixedSlotBoundaryReveal,

    // 在构型标记处切换另一套语义内容
    FixedSlotSemanticSwap,

    // 跟随同一份计划运动
    TravelWithCarrier,

    // 先跟随，随后交接到固定槽
    TravelThenDockToSlot
}
```

其中：

* 普通说明文本：`FixedSlotBoundaryReveal`
* 页面内容替换：`FixedSlotSemanticSwap`
* 角色名、核心数值、被拖动的功能标题：`TravelWithCarrier`
* 面板移动后形成新布局标题：`TravelThenDockToSlot`
* 无意义的临时装饰：`HideDuringTransit`

一个功能面可以组合策略，而不是整张面板只有一个策略：

```text
图标：TravelWithCarrier
标题：TravelThenDockToSlot
描述：FixedSlotBoundaryReveal
辅助提示：HideDuringTransit
```

这会比“一张面板整体怎么处理”灵活很多。

---

# 十、我建议的代码边界

```csharp
public readonly struct ContentProjectionRequest
{
    public ContentFaceId FaceId;
    public ContentProjectionPolicy Policy;
    public ContentEnvelope FullEnvelope;
    public ContentEnvelope CompactEnvelope;
    public ContentSlotId TargetSlot;
    public CarrierAnchor TravelAnchor;
    public ContentRevealProfile RevealProfile;
}

public readonly struct ContentSample
{
    public Rect CanvasClipRect;
    public Vector2 CanvasPosition;
    public Vector2 LocalOffset;
    public float Alpha;
    public float VariantBlend;
    public ContentDensityLevel DensityLevel;
    public bool Interactable;
    public bool Culled;
}
```

核心模块：

```text
ContentPolicyResolver
负责先确认内容策略和 Envelope
随行内容在这里反向提高 Planner 的尺寸下限

TransitionPlanner
只负责合法面板轨迹

ContentChoreographer
从面板轨迹派生内容轨迹

ContentProjectionPlayer
统一采样

WorldContentPresenter
应用世界空间内容

CanvasContentPresenter
应用 TMP、Clip、Alpha 和固定槽
```

我不建议第一版直接把内容逻辑塞进 `TransitionPlanner`。

Planner 只需要提前知道：

```text
哪些内容是随行的
它们要求多大的最小空间
```

至于 Alpha、Clip、语义切换，是面板轨迹求出之后才能决定的表现问题，放在独立的 `ContentChoreographer` 会更干净。

---

# 十一、第一刀怎么落地最合适

先只实现一个非常窄的案例：

```text
左侧玩家信息面
右边界向左收缩
内容包括：
- 标题
- 两行描述
- 一个图标
```

第一版只做：

1. 烘焙三个 Envelope：标题、描述、图标。
2. 从 TransitionPlan 投影面板内边界到 Canvas。
3. 固定 TMP 完全不移动。
4. 使用动态 Clip Rect 吞掉文字。
5. 描述比标题提前退出。
6. 加 100ms 左右边界前瞻。
7. 加进入/退出迟滞。
8. 测试 A→B→A 和 A→B→C 快速改道。
9. 完全隐藏后才 Cull，隐藏前不 SetActive。
10. Alpha 足够低时由内容状态关闭交互。

`CanvasGroup` 可以统一控制一组 UI 的 Alpha、交互和 Graphic Raycast，但它对 `Physics.Raycast` 不生效，所以输入所有权仍然应该继续交给你已有的 `UiInputArbiter`，不能把 CanvasGroup 当完整输入门禁。([Unity 文档][5])

第一刀效果成立以后，再加入：

```text
Full → Compact 语义降级
TravelWithCarrier
TravelThenDockToSlot
多边同时挤压
```

---

# 最终判断

我会把这套设计总结为一句话：

> **面板负责制造空间，内容负责感知空间；内容不是面板的附属 Transform，而是面板时空轨迹的第二投影。**

你提出的“面板内部边界驱动收缩式内容控制”完全值得作为主方案，但应当落成：

```text
预测边界压力
→ 动态裁剪窗口
→ 定向退让
→ 信息密度降级
→ 迟滞与连续改道
```

Canvas 文字虽然脱离世界面板，反而非常适合这个设计：文字保持稳定、清晰、不追踪背板，只让面板投影出的“可见窗口”扫过它。这样既保留了你架构里双渲染域的优势，又能产生非常自然的“内容被面板吞入、释放”的感觉。

[1]: https://docs.unity3d.com/cn/2022.3/Manual/script-RectMask2D.html?utm_source=chatgpt.com "RectMask2D - Unity 手册"
[2]: https://docs.unity3d.com/cn/2022.2/ScriptReference/CanvasRenderer-clippingSoftness.html?utm_source=chatgpt.com "CanvasRenderer-clippingSoftness - Unity 脚本 API"
[3]: https://docs.unity3d.com/ja/Packages/com.unity.textmeshpro%403.0/api/TMPro.TextOverflowModes.html?utm_source=chatgpt.com "Enum TextOverflowModes
 \| TextMeshPro | 3.0.1"
[4]: https://docs.unity3d.com/ru/current/ScriptReference/RectTransformUtility.ScreenPointToWorldPointInRectangle.html?utm_source=chatgpt.com "RectTransformUtility-ScreenPointToWorldPointInRectangle - Unity Scripting API"
[5]: https://docs.unity3d.com/cn/2022.1/Manual/class-CanvasGroup.html?utm_source=chatgpt.com "画布组 - Unity 手册"
