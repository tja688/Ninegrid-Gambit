# 工具与实验（VisualFxLab + VisualLook/UI + Temporary Test）

> 权威代码事实快照 · 2026-08-12 · 覆盖 `Assets/Scripts/VisualFxLab/`（8 个 .cs，含 Editor 子程序集）、`Assets/Scripts/UI/`（5 个 .cs：VisualLook 4 + 根级 1）、`Assets/Scripts/Temporary Test/`（2 个 .cs），全部逐文件通读核实。

这三块都是**表现侧工具/实验代码**，不承载任何游戏规则。共同特征：不进 Core、不进 IntentIntake 输入链、不改卡牌 L0–L3 变换塔与卡级排序黑盒（详见各节不变量）。

---

## 一、画面实验室 NineGrid.VisualFxLab（试验性，非正式接线）

### 职责综述

一键装配/一键还原的视觉效果试装场：F9 总开关；F10 循环 5 个全屏 Look（CRT 显像管 / 烛光酒馆 / 辉光绽放 / 深渊地牢 / 老式胶片）；F11 循环强度（100%/70%/40%）；F6/F7/F8 分别控制机关卡光环、氛围浮尘余烬、卡面流光三个模块。全部视觉产物（贴图/材质/精灵）运行时程序化生成（`hideFlags = DontSave`），卸载即拆净，不新增美术资产。控制入口 `FxLabHotkeyHost` 仅在 `UNITY_EDITOR || DEVELOPMENT_BUILD` 下自举——**正式包中无热键宿主，`FxLabState.Installed` 恒为 false，渲染 Feature 零 Pass 零开销**。

### 关键类型表

| 类型 | 文件（相对 `Assets/Scripts/VisualFxLab/`） | 一句话职责 |
|------|------|------|
| `FxLabState` / `FxLabLook` | `FxLabState.cs` | 全静态运行时状态：总开关、当前 Look、强度、三模块布尔、`FeatureLoaded` 标志与 Look 中文名 |
| `FxLabHotkeyHost` | `FxLabHotkeyHost.cs` | 唯一控制入口：Editor/Dev `AfterSceneLoad` 自举常驻宿主；New Input System 轮询 F6–F11；OnGUI 状态叠层（开场 12 秒提示） |
| `NineGridFxLabFeature` | `NineGridFxLabFeature.cs` | URP RenderGraph 全屏 Look Feature：挂 Renderer2D、事件 600（AfterRenderingPostProcessing，在 Selective Look 之后，扫描线跟随 CRT 畸变）；未装配/Look=None/Overlay 相机不入队；Bloom 走「预滤波→半分辨率横竖模糊→合成」四段 |
| `FxLabTrapHalo` | `FxLabTrapHalo.cs` | F6 模块：0.6s 定期扫描场上机关卡（`CardPresentationKind.Trap` + GroundCardMode + 未死 + 非背面），在卡面根下挂「呼吸底环（组内 order −60）+ 轨道火花粒子（+55）」，颜色按 defId 区分（离开=翠绿/烈焰=橙红/治疗泉=水青/复活石=紫/其余=暖金） |
| `FxLabAmbientDust` | `FxLabAmbientDust.cs` | F7 模块：两套粒子挂主相机视野平面——半透明暖白浮尘（UI 层 260）+ 加色暖橙余烬（Main 层 140，HDR 亮度配合辉光 Look 自然发光）；关闭整棵销毁 |
| `FxLabCardFoil` | `FxLabCardFoil.cs` | F8 模块：为手牌/场上/拖拽卡面的「卡框」「主图标」精灵各挂一层同精灵加色覆盖层（`NineGrid/FxLab/CardFoil` shader 斜向流光+像素闪点），每帧同步 sprite/flip/排序，相位按 uid 错开 |
| `FxLabRuntimeAssets` | `FxLabRuntimeAssets.cs` | 程序化贴图（柔圆光斑/四芒星/光环）、粒子材质工厂（SrcAlpha + One/OneMinusSrcAlpha）、流光共享材质；全部 DontSave |
| `FxLabRendererFeatureInstaller` | `Editor/FxLabRendererFeatureInstaller.cs` | Editor 菜单 `NineGrid/画面实验室/`：向 `Assets/Settings/Renderer2D.asset` 安装/移除 Feature（含 `m_RendererFeatureMap` 序列化修复），彻底移除即还原资产原状 |

### 核心流程与数据流

`FxLabHotkeyHost`（写 `FxLabState` 静态字段 + 开关三个模块组件）→ `NineGridFxLabFeature.AddRenderPasses` 每帧读 `FxLabState` 决定入不入队 → `ApplyLookParams` 按 Look × 强度写材质参数 → RenderGraph blit（Bloom 另走多 Pass 并经 `SetGlobalTextureAfterPass` 发布 `_FxLabBloomTex`）。三个模块组件只读表现层公开面（`CardManagerSingleton.CardsByUid`、`ManagedCard` 的 Kind/DisplayMode/FaceUp/MountedFaceRoot），单向消费，不回写。

### 对外通信面

- 被谁调用：无——完全自治，靠热键驱动。
- 调用谁：`NineGrid.Presentation`（`NineGrid.Cards` 命名空间的 `CardManagerSingleton` / `ManagedCard` / `CardPresentationKind` / `CardDisplayMode`）、URP RenderGraph API、New Input System。
- 资产依赖：`Assets/Arts/VisualProfiles/NineGridFxLabLooks.shader`（全屏 Look）、`NineGrid/FxLab/Particle` 与 `NineGrid/FxLab/CardFoil` shader、`Assets/Settings/Renderer2D.asset`（Feature 宿主）。

### 关联 ADR / 文档

无专属 ADR（试验性模块）。排序纪律对齐 [ADR-0002](../../../docs/adr/0002-card-chassis-and-face-templates.md) 卡级排序黑盒（只用组内相对 order）；使用说明见 `Assets/Notes/画面实验室-视觉效果试装-2026-08-11.md`（过程笔记，非权威）；code-map 条目见 `docs/code-map/README.md` 程序集一览。

### 不变量与坑

1. **只添加渲染子物体，不触碰卡牌 L0–L3 变换塔与排序黑盒**（`FxLabTrapHalo` 注释明示；光环/流光都只用组内相对 sortingOrder）。
2. **正式包安全性靠两道闸**：热键宿主编译期排除（`#if UNITY_EDITOR || DEVELOPMENT_BUILD` 只包住 Bootstrap，类型本身仍编入但无实例）+ `FxLabState.Installed` 默认 false 时 Feature 不入队。
3. 彻底移除路径：菜单 `NineGrid/画面实验室/移除全屏渲染特性` + 删 `Assets/Scripts/VisualFxLab/` 目录与 `Assets/Arts/VisualProfiles/NineGridFxLab*.shader`。
4. `FxLabTrapHalo` / `FxLabCardFoil` 用 `FindFirstObjectByType<CardManagerSingleton>` 定位宿主——这违反 MainScene 装配卫生（#141 禁 Find）的字面约定，但该约定针对生产装配；实验模块属豁免灰区，转正时必须改走显式绑定。
5. 粒子速度模块所有曲线须同一模式（代码注释踩坑记录：orbital/z 也要显式给同型曲线，否则 Unity 报错）。
6. F1 已被伤害日志、F12 已被作弊面板占用，实验室只用 F6–F11。

### 文件覆盖清单（8 / 8）

| 文件 | 说明 |
|------|------|
| `Assets/Scripts/VisualFxLab/FxLabState.cs` | 静态状态 + Look 枚举与中文名 |
| `Assets/Scripts/VisualFxLab/FxLabHotkeyHost.cs` | 热键宿主与状态叠层（Editor/Dev 自举） |
| `Assets/Scripts/VisualFxLab/NineGridFxLabFeature.cs` | Renderer2D 全屏 Look RenderGraph Feature（5 种 Look + Bloom 多 Pass） |
| `Assets/Scripts/VisualFxLab/FxLabTrapHalo.cs` | 机关卡光环 rig（呼吸底环 + 轨道火花，按 defId 配色） |
| `Assets/Scripts/VisualFxLab/FxLabAmbientDust.cs` | 全场氛围浮尘与余烬粒子（挂主相机） |
| `Assets/Scripts/VisualFxLab/FxLabCardFoil.cs` | 卡框/主图标加色流光覆盖层（逐帧同步底精灵） |
| `Assets/Scripts/VisualFxLab/FxLabRuntimeAssets.cs` | 程序化贴图/材质/精灵工厂（DontSave） |
| `Assets/Scripts/VisualFxLab/Editor/FxLabRendererFeatureInstaller.cs` | Renderer2D Feature 安装/卸载菜单 |

---

## 二、视觉 Look 管线 NineGrid.VisualLook + UI 根级杂项

### 职责综述

`Assets/Scripts/UI/VisualLook/`（独立程序集 `NineGrid.VisualLook`，不引用任何 NineGrid 业务程序集）是**像素风 Look 的正式渲染管线**：核心思路为「Sprite 材质顶点 snap 提供像素感，全屏 UV snap 默认关闭（会毁掉 TMP/SDF 文字），扫描线由全屏 Pass 统一施加」。这就是技能 `sprite-owned-pixel-snap` 描述的那套已出货方案。`Assets/Scripts/UI/` 根级另有一个无 asmdef 文件（编入 Assembly-CSharp）：`TmpBitmapPixelOutline`，做 Bitmap TMP 的八向像素黑边。

### 关键类型表

| 类型 | 文件（相对 `Assets/Scripts/UI/`） | 一句话职责 |
|------|------|------|
| `TableNineSelectiveLookFeature` | `VisualLook/TableNineSelectiveLookFeature.cs` | URP RenderGraph Feature：默认稳定路径 = Base 相机 Pass1 snap + Pass2 scanline；可选 `buildNoSnapMask`（同相机把 NoPixelSnap 层的世界 TMP 重绘进 `_NoSnapMask`，snap 时保护这些像素）；实验开关 `overlayOwnsScanline`（Overlay 栈末统一扫描线，当前 URP 栈不稳定，默认关） |
| `TableNineLookRig` | `VisualLook/TableNineLookRig.cs` | 场景装配壳（`ExecuteAlways`）：驱动 Look/Sprite-snap 材质参数（分辨率/snap/扫描线三参数）、单相机 NoSnapMask 模式 vs 旧双相机栈模式切换、TMP 扫描线材质同步（谁拥有扫描线的三分支裁决，避免叠双份） |
| `TableNineFinalScanlineMarker` | `VisualLook/TableNineFinalScanlineMarker.cs` | 空标记组件：标出「应在栈末做最终扫描线 blit」的 Overlay 相机（仅 `overlayOwnsScanline` 实验路径消费） |
| `LivingTextWorldBinder` | `VisualLook/LivingTextWorldBinder.cs` | 把 Screen Space Camera 下的 RectTransform 逐帧跟随到世界目标的屏幕投影点；进 Play 时自动捕获编辑器摆位偏移（所见即所得），`worldOffset` 留作运行时微调 |
| `TmpBitmapPixelOutline` | `TmpBitmapPixelOutline.cs`（UI 根级，Assembly-CSharp） | Bitmap TMP 像素黑边：`OnPreRenderText` 把每个可见字形八向平移复制上色（先黑边后本体）；不采样 atlas 邻域避免串色；内联 Sprite 跳过描边；顺带关 UV 外扩（extraPadding / `_OutlineWidth`） |

### 核心流程与数据流

`TableNineLookRig`（场景组件，Update 持续写材质参数）→ 三份材质：`TableNineSelectiveLook.mat`（全屏 Look）、legacy `TableNinePixelSnap.mat`、`TableNineSpriteLitPixelSnap.mat`（Sprite 顶点 snap）→ `TableNineSelectiveLookFeature` 按材质参数决定入队哪种 PassMode（SnapThenScanline / SnapOnly / ScanlineOnly）。当前权威构型：`useSnapMaskSingleCamera = true`（单相机 + NoSnapMask 遮罩；世界字留在 Base 相机，排序层可自然遮挡文字），`pixelSnap = 0`（全屏 snap 关）、`spritePixelSnap = 1`。TMP 扫描线归属裁决：单相机模式或统一扫描线模式下 TMP 自带扫描线关闭（全屏 Pass 已盖），仅旧双相机路径 TMP 自补。

### 对外通信面

- 被谁调用：场景（MainScene/UITestSence）序列化装配 `TableNineLookRig` / Feature 挂 `Renderer2D.asset`；`StartRunHoverScale` 等无关。
- 调用谁：URP RenderGraph、TMP。**不引用任何 NineGrid 业务程序集**（asmdef 仅引 RenderPipelines + TextMeshPro），是完全独立的渲染层。
- `TmpBitmapPixelOutline` 由场景中带 Bitmap 字体的 TMP 对象挂载，无代码调用方。

### 关联 ADR / 文档

无专属 ADR。可移植方案说明见 `.cursor/skills/sprite-owned-pixel-snap/`（技能文档）；`NineGridFxLabFeature` 与本 Feature 同构且排在其后（事件 600）。

### 不变量与坑

1. **全屏 UV Pixel Snap 会毁掉 TMP/SDF 文字**——`pixelSnap` 参数注释明示保持 0；像素感只能走 Sprite 材质顶点 snap（`spritePixelSnap`）。
2. **扫描线只能有一个拥有者**：单相机模式 / 统一扫描线模式下必须关 TMP 自带扫描线，否则叠双份（`SyncTmpMaterials` 的三分支裁决是这条不变量的实现）。
3. `overlayOwnsScanline`（Overlay AfterRendering blit）在当前 URP 栈上不可靠，**默认保持关闭**，除非验证过。
4. NoSnapMask 路径要求 TableNine TMP shader 声明 `LightMode=Universal2D`，否则遮罩 RendererList 找不到文字网格。
5. 场景须有名为 `NoPixelSnap` 的 Layer；缺失时 `TableNineLookRig.ApplyRig` 直接警告返回。
6. `TmpBitmapPixelOutline` 的实现坑（代码注释记录）：`TextMeshProUGUI` override 了 `OnPreRenderText`，必须订阅派生类事件；`TMP_MeshInfo` 是 struct，`ResizeMeshInfo` 后必须写回数组；Resize 参数是四边形数而非顶点数。

### 文件覆盖清单（5 / 5）

| 文件 | 说明 |
|------|------|
| `Assets/Scripts/UI/VisualLook/TableNineSelectiveLookFeature.cs` | 选择性 Look Feature：snap/scanline 双 Pass + NoSnapMask 文字保护 + Overlay 实验路径 |
| `Assets/Scripts/UI/VisualLook/TableNineLookRig.cs` | 场景装配壳：材质参数驱动、单相机/双相机模式切换、TMP 扫描线同步 |
| `Assets/Scripts/UI/VisualLook/TableNineFinalScanlineMarker.cs` | Overlay 栈末扫描线相机标记（空组件） |
| `Assets/Scripts/UI/VisualLook/LivingTextWorldBinder.cs` | UI 文字跟随世界目标投影点（捕获编辑器摆位偏移） |
| `Assets/Scripts/UI/TmpBitmapPixelOutline.cs` | Bitmap TMP 八向像素黑边（OnPreRenderText 顶点复制；Assembly-CSharp） |

---

## 三、临时测试 NineGrid.TemporaryTest

### 职责综述

两个一次性实验/验收脚本的收容所，独立程序集 `NineGrid.TemporaryTest`（引用 `NineGrid.Presentation` + `Unity.Pipeline`）。**不属于正式玩法链路**，随时可删。

### 关键类型表

| 类型 | 文件 | 一句话职责 |
|------|------|------|
| `StartRunHoverScale` | `Assets/Scripts/Temporary Test/StartRunHoverScale.cs` | 热重载 override 工作流实验：主菜单 StartRun 按钮 hover 放大；用 `WorldPointerUtility.TryOverlapColliderOnPlane` 轮询（遵守 ADR-0023 禁 legacy `OnMouse*`），Enter/Exit 经 `HotReloadHelper.ExecuteWithHotReload` 包装以演示 `com.unity.pipeline` 热重载 |
| `UITestLivingFormChoiceTest` | `Assets/Scripts/Temporary Test/UITestLivingFormChoiceTest.cs` | UITestSence 专用：`Flow.UITestBootstrap` 转发小键盘 1，驱动 `SelectorManagerSingleton.BeginBounceChoice` 做 3 选 → 6 选 → 关闭的 Bounce 扇形形态选择验收（实现 `Flow.IUITestKeyConsumer`） |

### 对外通信面

- `StartRunHoverScale`：挂在主菜单场景对象上；只读 `WorldPointerUtility`（`NineGrid.Flow`）与自身 Collider2D。
- `UITestLivingFormChoiceTest`：只在 UITestSence 生效；调用 `SelectorManagerSingleton`（Bounce 门面）；候选 defId 里的 `Attack`/`Armor`/`Hp` 是内容条目遗留（其正式 UI 入口 #90 已退役），仅作视觉验收素材。

### 关联 ADR

- [ADR-0023](../../../docs/adr/0023-slot-hit-frame-and-claim.md)：`StartRunHoverScale` 的指针轮询方式即其「退役野生拾取 / 禁 OnMouse*」条款的合规示例。
- [ADR-0015](../../../docs/adr/0015-card-slot-placement-local-space.md)：BounceFan 验收间接覆盖其零缩放锚定约束。

### 不变量与坑

1. **可疑点**：`NineGrid.TemporaryTest.asmdef` 未限制 `includePlatforms` 且引用 `Unity.Pipeline`（`com.unity.pipeline` 热重载工具包）。若发布构建裁掉该包或其不支持 Player 编译，正式出包可能因此断链——预发布清理时建议整目录删除或限 Editor。
2. `UITestLivingFormChoiceTest` 依赖场景手挂或 `FindFirstObjectByType` 兜底找 `SelectorManagerSingleton`，仅测试场景可接受。

### 文件覆盖清单（2 / 2）

| 文件 | 说明 |
|------|------|
| `Assets/Scripts/Temporary Test/StartRunHoverScale.cs` | StartRun hover 放大 + com.unity.pipeline 热重载 override 实验 |
| `Assets/Scripts/Temporary Test/UITestLivingFormChoiceTest.cs` | UITest 小键盘 1 驱动的 Bounce 扇形 3/6 选验收脚本 |
