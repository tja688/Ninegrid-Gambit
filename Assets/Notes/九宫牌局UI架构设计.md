
这套UI架构设计，它真正有价值的地方，不只是“面板会翻转、会移动”，而是让整个 UI 看起来像一台有结构、有机关、有空间关系的实体机器。玩家不是在不同菜单之间切屏，而是在同一个战斗桌面上操作不同功能。

但最大的风险也很明确：

> 技术难点不是动画，而是状态组合失控。

如果每个对象都可以让任意面板变成任意功能，后期很容易出现几十个面板状态、几百条动画路径，以及“鼠标刚移走，动画还没播完，又点了设置”的混乱情况。

所以现在最重要的不是马上做更多动画，而是先确定一套 **UI 语法和组织模型**。

---

# 一、先重新定义“面板”

你现在脑中可能是：

* 玩家信息面板
* 关卡信息面板
* 手牌面板
* 卡组面板
* 战斗场地

建议在 Unity 里不要这样定义。

应该分成三个概念：

| 概念           | 含义              | 例子                  |
| ------------ | --------------- | ------------------- |
| Slot / Panel | 物理载体，决定位置、尺寸、边框 | 左上大格、顶部小格 1、中间大格    |
| Face         | 面板当前显示的内容       | 玩家信息、卡组名称、设置按钮      |
| Mode         | 整个桌面当前的组合状态     | 战斗模式、卡组预览、卡组详情、设置模式 |

核心原则是：

> 面板不是功能，面板只是一个可以承载功能的物理槽位。

比如截图中的左上区域，Unity 对象不应叫：

```text
PlayerInfoPanel
```

而应该叫：

```text
Slot_TopLeftLarge
```

它可以承载：

```text
PlayerSummaryFace
DeckTitleFace
EnemySummaryFace
SettingsCategoryFace
```

这样你以后扩展时，不需要修改面板本身，只需要给它增加新的 Face。

---

# 二、推荐的整体架构

我建议拆成五层。

## 1. Slot：物理面板层

Slot 只管理这些东西：

* 当前 RectTransform
* 边框和底板
* 遮罩区域
* 当前 Face
* 翻转、滑动、缩放等表现
* 是否参与组合、拼接

它不应该知道：

* 玩家有多少生命
* 卡组里有什么牌
* 当前是什么关卡
* 点击之后进入什么游戏逻辑

示意结构：

```text
Slot_TopLeftLarge
├── Frame
├── ContentMask
│   ├── CurrentFaceRoot
│   └── NextFaceRoot
├── Decoration
└── SlotAnimator
```

每个 Slot 最好都提供统一接口：

```csharp
ShowFace(FaceId face);
MoveTo(LayoutPose pose);
PlayTransition(TransitionId transition);
SetInteractable(bool value);
```

---

## 2. Face：内容层

Face 是真正显示内容的部分。

例如：

```text
PlayerSummaryFace
DeckTitleFace
DeckDescriptionFace
DeckGridFace
BattleArenaFace
SettingsButtonsFace
StageInfoFace
```

Face 只负责：

* 接收 ViewModel
* 显示文字、图标和数据
* 自己内部的小动画
* 抛出按钮事件

Face 不负责决定自己应该放在哪个面板上。

例如 `DeckDescriptionFace` 不需要知道自己现在位于左侧小面板，还是右侧大面板。

可以设计一个基础接口：

```csharp
public interface IUIFace
{
    void Bind(object viewModel);
    void OnShown();
    void OnHidden();
}
```

常用 Face 建议预先创建或对象池化。鼠标悬停触发的内容不要每次重新 Instantiate。

---

## 3. Layout：空间布局层

Layout 只描述所有 Slot 的空间状态，例如：

* 位置
* 尺寸
* 是否显示
* 层级
* 是否与其他 Slot 拼接
* 当前边框类型

比如：

```text
CombatLayout
DeckInspectLayout
SettingsLayout
CardDetailLayout
RewardLayout
```

`CombatLayout` 中：

* 中央面板占据战斗区域
* 顶部三个小格独立排列
* 左右侧栏正常显示

`DeckInspectLayout` 中：

* 中央面板仍处于原位置
* 但显示完整牌组
* 左上显示卡组标题
* 左中显示卡组描述
* 右侧显示统计或筛选

`SettingsLayout` 中：

* 顶部三个小格进行内容替换
* 某些侧边面板收起或拼接
* 中央战斗区域变暗或保持冻结

这些数据非常适合做成 ScriptableObject。

```text
UILayoutPreset
├── SlotId
├── AnchoredPosition
├── SizeDelta
├── Scale
├── Rotation
├── Visible
├── SortingOrder
└── GroupId
```

不要直接依赖 LayoutGroup 来完成大型运动。动画开始前先计算目标位置，然后暂时脱离自动布局，使用明确的起点和终点进行插值。

---

## 4. Mode：页面状态层

Mode 决定：

* 每个 Slot 当前显示什么 Face
* 使用什么 Layout
* 哪些输入生效
* 是否暂停战斗
* 如何进入和退出

例如你描述的几个模式，可以这样组织：

### CombatDefault

```text
左上大格：PlayerSummaryFace
左侧中格：StageResourceFace
左侧文字格：StageInfoFace
中央大格：BattleArenaFace
顶部小格：HealthFace / AttackFace / DefenseFace
右侧：DeckFace / HandFace / BattleLogFace
```

### DeckHoverPreview

```text
左上大格：DeckTitleFace
左侧文字格：DeckDescriptionFace
中央大格：仍然是 BattleArenaFace
其他区域：保持原状态
```

### DeckInspect

```text
左上大格：DeckSummaryFace
左侧文字格：DeckDescriptionFace
中央大格：DeckGridFace
右侧区域：DeckStatisticsFace / FilterFace
```

### Settings

```text
顶部三个小格：
MainMenuButtonFace
SaveButtonFace
QuitButtonFace

中央战斗区域：
保持画面但禁止交互，或者显示 SettingsMainFace
```

模式可以做成类似下面的数据：

```csharp
public class UIModeDefinition : ScriptableObject
{
    public UIModeId id;
    public UILayoutPreset layout;
    public List<SlotFaceBinding> slotFaces;
    public UITransitionDefinition enterTransition;
    public UITransitionDefinition exitTransition;
}
```

这样以后增加“卡牌图鉴模式”“敌人观察模式”“奖励选择模式”，主要是增加配置，而不是修改几十个 MonoBehaviour。

---

## 5. Transition Director：动画导演层

不要让每个面板各自决定什么时候运动。

应该有一个中央导演：

```text
UITransitionDirector
```

它接收：

```text
从 CombatDefault
切换到 DeckInspect
```

然后计算差异：

* 哪些 Slot 位置变了
* 哪些 Slot 尺寸变了
* 哪些 Face 需要替换
* 哪些面板需要翻面
* 哪些边框需要隐藏
* 哪些面板需要拼接

一个完整切换可以分成阶段：

```text
1. 暂时锁定相关输入
2. 旧内容退场
3. 面板移动或翻转
4. 在不可见的时刻替换 Face
5. 新内容入场
6. 恢复输入
```

例如翻面：

```text
0°      显示旧 Face
0°→90°  压缩或旋转
90°     替换成新 Face
90°→0°  展开新 Face
```

在 2D 像素风里，不一定真的需要复杂 3D 旋转。可以使用：

* X 或 Y 缩放到接近 0
* 替换内容
* 再缩放回来
* 配合阴影、扫描线或亮边

通常比真正的 3D 翻牌更符合像素画面，也不容易出现透视和采样问题。

---

# 三、你的交互最好分成三种等级

不是所有指向对象的动作都应该让整个 UI 大变形。

## 第一等级：悬停预览

例如鼠标指向牌组：

* 左上显示牌组名称
* 左侧显示简短描述
* 原本的战斗信息暂时被覆盖
* 鼠标移开后自动恢复

建议特点：

* 变化范围小
* 动画较快
* 不改变中央战场
* 不暂停游戏
* 可以随时被取消

最好加入约 100～200 毫秒的悬停延迟，避免鼠标扫过多个对象时整个界面疯狂翻转。

---

## 第二等级：点击查看

例如点击牌组：

* 中央战场翻为完整牌组
* 侧栏变成卡组数据
* 当前模式变成 `DeckInspect`
* 鼠标移开不自动恢复
* 点击返回或再次点击后退出

这是明确的“功能模式切换”，动画可以更完整。

---

## 第三等级：全局模式

例如：

* 设置
* 奖励结算
* 商店
* 游戏结束
* 暂停菜单

这些模式可以允许多个面板一起移动、拼接或重组，因为玩家已经主动表达了明确意图。

这种分级能避免每一次 hover 都像界面地震。

---

# 四、一定要有 UI 状态栈

你描述的操作特别适合状态栈。

例如：

```text
CombatDefault
    ↓ 鼠标指向牌组
DeckHoverPreview
    ↓ 点击
DeckInspect
    ↓ 打开某张牌
CardDetail
    ↓ 关闭
DeckInspect
    ↓ 返回
CombatDefault
```

维护一个类似下面的结构：

```text
UIStateStack
```

基本操作：

```csharp
PushTemporary(DeckHoverPreview);
Push(DeckInspect);
Pop();
ReturnToRoot();
```

其中：

* Hover 是临时状态
* Click 是正式状态
* Settings 是全局状态
* BattleDefault 是根状态

这样“所有东西归位”不需要你手写：

```text
把左上恢复成玩家
把中央恢复成战斗
把右边恢复成手牌
把顶部恢复成生命攻击防御……
```

只需要：

```csharp
UIStateStack.ReturnToRoot();
```

然后系统根据状态差异自动恢复。

---

# 五、鼠标悬停会产生一个非常现实的问题

假设玩家快速进行下面的操作：

```text
指向牌组
移向手牌
点击设置
又移回战场
```

这时候可能出现：

* 卡组翻转还没完成
* 手牌动画已经开始
* 设置动画又覆盖上来
* 旧动画结束后把新内容错误地换了回去

所以动画系统必须支持：

## 可取消

每次状态请求带有一个版本号或 CancellationToken。

新状态到来时：

* 取消旧动画
* 或让旧动画快速抵达安全节点
* 再开始新动画

## 可中断

不要假设所有动画都一定从 0 播到 1。

例如面板当前已经翻了 40%，新状态要求翻回去，应该从当前 40% 平滑返回，而不是瞬间归零后重新播放。

## 最终状态校验

动画完成时必须确认：

```text
我现在完成的，仍然是系统最后要求的状态吗？
```

如果不是，不能写入旧 Face。

这是后期稳定性的关键。

---

# 六、“两个小面板拼成一个大面板”怎么做

有两种实现路线。

## 路线 A：视觉拼接

两个 Slot 仍然是两个对象：

* 移动到一起
* 隐藏中间边框
* 在上面覆盖一个统一的大边框
* 内容由一个 CompositeFace 显示

看起来像一个大面板，但底层仍是两个槽位。

这是我更推荐的方案。

优点：

* 不需要频繁改变层级
* 容易恢复
* 动画稳定
* 配置简单
* 不容易破坏锚点

## 路线 B：真正合并

运行时：

* 创建一个组合父对象
* 将两个 Slot 重新挂到父对象
* 修改尺寸和坐标
* 使用新的内容区域
* 结束后再拆开

适合复杂的大型组合，但状态管理会更难。

你的项目里，大多数情况下视觉拼接就足够。玩家只关心它看起来是否合为一体，不关心层级结构是否真的合并。

可以建立：

```text
PanelAssembly
```

它描述：

```text
由哪些 Slot 组成
组合后的外边框
哪些接缝隐藏
组合内容放在哪里
```

---

# 七、顶部三个小格的“传送门替换”

你说的效果非常适合做成一种可复用动画模板，而不是为设置菜单单独编写。

可以命名为：

```text
PortalReplaceTransition
```

动画过程可以是：

```text
1. 原来的生命、攻击、防御小格向上移动
2. 进入顶部遮罩区后消失
3. 新的三个小格从下方遮罩区出现
4. 移动到原有插槽位置
5. 新 Face 亮起
```

这里有两种设计。

### 物理槽位不动，只替换内容

三个外框始终不动，框内内容上下滚动。

优点是稳定、清晰。

### 整个面板移动

旧的三个面板真的被顶走，新的面板从下面出现。

表现更强，但需要正确处理：

* 遮罩
* 层级
* 输入区域
* 动画中断
* 新旧对象池

建议最初先做第一种。等状态系统稳定后，再升级为整块移动。

---

# 八、不要让每一个面板都完全自由

“每个面板都是多面手”是一个很好的创意方向，但不代表每个面板必须支持所有功能。

建议为 Slot 设置能力范围。

例如：

```text
Slot_TopLeftLarge
适合：
- 人物概要
- 卡组概要
- 敌人概要
- 当前模式标题

不适合：
- 十二张卡牌列表
- 大段设置选项
```

```text
Slot_CenterLarge
适合：
- 战斗场地
- 完整卡组
- 地图
- 奖励选择
- 商店货架
```

```text
Slot_TopSmall
适合：
- 数值
- 图标按钮
- 快捷操作
- 状态指示
```

这相当于给 UI 建立一套“家具尺寸规范”。

否则后期很容易因为一个功能塞不下，而不断创建例外。

---

# 九、为你的游戏建立一套动画语法

你的特色不应该是“哪里都能播放不同动画”，而应该是玩家逐渐能看懂的空间语言。

例如可以规定：

| 动作     | 含义          |
| ------ | ----------- |
| 横向翻面   | 同一个对象的另一类信息 |
| 纵向滚动   | 同一个位置的功能切换  |
| 面板滑入中央 | 进入深入查看模式    |
| 两块拼接   | 信息范围扩大      |
| 面板下沉   | 功能暂时关闭      |
| 红色闪烁   | 战斗或危险       |
| 紫色描边   | 卡牌或魔法信息     |
| 金色边框   | 奖励或高价值信息    |

这样玩家看到面板开始拼接，就会知道：

> 这是一个更大、更深入的功能。

看到小格纵向滚动，就知道：

> 这里的快捷操作正在被替换。

这会让动画不只是漂亮，而是承担交互提示。

---

# 十、Unity 内推荐的目录结构

可以先按下面组织：

```text
UI
├── Core
│   ├── UIRoot
│   ├── UIStateStack
│   ├── UIModeController
│   ├── UITransitionDirector
│   └── UIInputRouter
│
├── Slots
│   ├── SlotView
│   ├── SlotAnimator
│   ├── SlotRegistry
│   └── PanelAssembly
│
├── Faces
│   ├── Battle
│   ├── Player
│   ├── Deck
│   ├── Cards
│   ├── Settings
│   └── Common
│
├── Modes
│   ├── CombatDefault
│   ├── DeckPreview
│   ├── DeckInspect
│   └── Settings
│
├── Layouts
│   ├── CombatLayout
│   ├── InspectLayout
│   └── SettingsLayout
│
├── Transitions
│   ├── Flip
│   ├── PortalReplace
│   ├── Slide
│   ├── Merge
│   └── Fade
│
├── ViewModels
├── Theme
└── Debug
```

---

# 十一、Animator 和程序动画怎么分工

不建议为每个面板创建一个复杂 Animator Controller，再用几十个 Trigger 控制。

后面很容易变成：

```text
Idle
FlipToDeck
FlipToPlayer
FlipToEnemy
MergeLeft
MergeRight
OpenSettings
CloseSettings
InterruptSettings
...
```

状态会迅速膨胀。

建议：

### 程序化过渡系统负责

* 面板移动
* 尺寸变化
* 翻面
* 拼接
* 多面板时间编排
* 中断和恢复

### Animator 或局部动画负责

* 图标跳动
* 数值闪烁
* 按钮呼吸
* 光效循环
* 面板出现后的局部装饰

可以简单理解为：

> 大型舞台调度交给导演系统，小演员的表情动作交给 Animator。

---

# 十二、战斗逻辑必须和 UI 状态分开

中央战斗场地翻成牌组后，战斗数据本身不应该被销毁。

正确关系应该是：

```text
BattleState
    ↓ 提供数据
BattleArenaFace

DeckState
    ↓ 提供数据
DeckGridFace
```

UI 只是决定当前观察哪个数据。

不要让：

```text
关闭 BattleArenaFace
```

等于：

```text
结束或重置战斗
```

设置菜单是否暂停游戏，也应该由 GameFlow 或 PauseService 控制，而不是由设置 Face 自己直接修改 `Time.timeScale`。

---

# 十三、像素风需要额外注意

你的截图有很明确的像素风和扫描线效果。运动 UI 最容易破坏像素感。

建议确保：

* 面板最终位置落在整数像素
* 尽量避免静止时出现 0.3 像素坐标
* 像素纹理使用合适的点采样
* 动画过程中可以是连续坐标，但结束时强制吸附
* 不要频繁用非整数比例缩放像素边框
* 九宫格边框要固定像素宽度
* 字体描边、阴影宽度保持统一
* UI 缩放规则和目标分辨率固定

对于翻面效果，直接把整个像素面板压缩到极窄可能会产生闪烁。可以在接近最窄时提前隐藏旧内容，再显示一个纯色边缘条，之后展开新内容。

---

# 十四、目前最适合你的开发顺序

不要一开始就同时做翻面、拼接、传送门和所有面板自由移动。

## 第一阶段：证明结构

只做四个模式：

```text
CombatDefault
DeckHoverPreview
DeckInspect
Settings
```

暂时只使用：

* 淡入淡出
* 简单压缩翻面
* 面板内容替换

目标是证明状态系统正确。

## 第二阶段：建立动画模板

只实现四种可复用过渡：

```text
FlipReplace
SlideReplace
PortalReplace
MergePanels
```

不要为具体功能单独写动画。

## 第三阶段：处理连续输入

重点测试：

* 快速移入移出
* 动画中点击
* 动画中打开设置
* 动画中返回
* 连续切换不同卡牌
* 同一模式重复请求

## 第四阶段：加入复杂拼接

等基础状态系统稳定后，再做：

* 两格拼一格
* 三格联动
* 中央面板翻转
* 大范围布局变化

## 第五阶段：统一声音和视觉反馈

例如：

* 面板翻转音
* 卡槽吸附音
* 传送门替换音
* 面板合并锁定音

这类效果的“灵动感”很大一部分实际上来自声音和节奏，而不只是位移。

---

# 十五、建议你现在先制作这张状态表

在真正继续制作之前，可以先列出：

| Mode         | 左上   | 左中   | 左下   | 顶部三格      | 中央        | 右上   | 右中 | 右下 |
| ------------ | ---- | ---- | ---- | --------- | --------- | ---- | -- | -- |
| Combat       | 玩家信息 | 资源   | 关卡信息 | 战斗数值      | 战场        | 牌组   | 日志 | 手牌 |
| Deck Hover   | 卡组名称 | 卡组描述 | 保持   | 保持        | 战场        | 牌组高亮 | 保持 | 保持 |
| Deck Inspect | 卡组概要 | 卡组描述 | 卡组分类 | 筛选条件      | 卡组全览      | 统计   | 说明 | 操作 |
| Settings     | 设置标题 | 设置分类 | 版本信息 | 保存/主菜单/退出 | 设置内容或暗化战场 | 音频   | 画面 | 控制 |

每增加一个模式，先填这张表。

填不出来时，说明这个模式的空间设计还没有想清楚，不应该直接开始写动画。

---

# 最重要的结论

你的创意方向是成立的，而且有机会成为整个游戏最有辨识度的部分。

最合适的设计思想是：

> 固定的不是功能，而是空间。
> 面板是空间容器，Face 是内容，Mode 是组合，Transition 是语法。

不要把它做成“一堆会动的菜单”，而是做成：

> 一张永远不离开玩家视线、会根据意图重新组装的战斗桌面。

你当前最应该先完成的，不是更多美术变体，而是：

1. 给截图里的每一个物理面板分配稳定的 `SlotId`
2. 定义 `Combat / DeckPreview / DeckInspect / Settings` 四个 Mode
3. 把面板内容拆成 Face
4. 做一个支持取消的 `UITransitionDirector`
5. 用状态栈保证所有界面可以可靠归位

只要这五件事搭稳，后面的翻面、顶出、拼接、传送门效果都会变成可不断叠加的表现层，而不会把项目拖进状态地狱。
