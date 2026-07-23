# Living UI · 现状与解耦说明

> 对齐日期：2026-07-21  
> 对照：`Assets/Notes/归档/灵动UI架构-spec-2026-07-17.md`（产品 Spec）；抽出前代码文档镜像已删除，现状见仓库 `docs/code-map/`

---

## 1. 已落地 vs Spec

| Spec 能力 | 代码现状 |
| --- | --- |
| 固定身份载体 1…12，不运行时 SetParent | ✅ `CarrierView` + SceneLayoutSource |
| 构型蓝图 → 终态快照 | ✅ `LivingUiSceneLayoutSource.Capture` |
| 纯数据 `TransitionPlanner` + 分代计划 | ✅ + EditMode 测试 |
| 五次位姿 / 有界尺寸 / 保速打断 | ✅ `QuinticMotion` / `BoundedScalarMotion` / Player |
| Commit / Preview / 稳定命中区 | ✅ Director + Demo + `LivingUiStableHitZoneRoot` |
| 随行内容投影（Invariant / Scale） | ✅ Marker / Controller / Driver |
| 完整碰撞校验对搜索 / 失败 witness | ❌ 仍为简化卡农调度，非 Spec 全搜索 |
| 场地覆层 + `UiInputArbiter` | ❌ 未实现 |
| 正式 ScriptableObject 蓝图资产 | ❌ 仍从场景 Transform 抓取 |
| 替换宿主 `UiPanelRouter` 为布局权威 | ❌ 宿主侧未接线（MainScene 仍走壳显隐） |

---

## 2. 与宿主项目的耦合（刻意保持浅）

| 形式 | 有无 | 说明 |
| --- | --- | --- |
| asmdef → Flow/Cards/Core | **无** | Runtime `references: []` |
| `using` 业务命名空间 | **无** | |
| 场景默认根名「大盘」/ 构型名 | 约定默认值 | 可在 Inspector 改绑；Demo/迁移工具写死了样例名 |
| `LivingUiLayoutId` 枚举名 | 语义样例 | MainMenu/Battle… 是布局词汇，不是流程状态机 |
| Editor Legacy 迁移表 | 历史项目名 | `CardHandAnchors` 等 → 菜单 `LivingUI/Legacy/…` |
| VisualLook / PixelSnap / TMP 描边 | **包外** | 渲染例外由宿主提供 |

结论：删除或禁用本包 **不会** 打断 TableNine Core 战斗环；只会失去灵动舞台与相关编辑器菜单。

---

## 3. 跨项目复用清单

带走：

- `Packages/com.livingui.stage/` 整包

不要默认带走（宿主专属）：

- `Assets/Scripts/UI/VisualLook/`
- `Assets/Scripts/UI/TmpBitmapPixelOutline.cs`
- `Assets/Scripts/Flow/Editor/LivingUi/`（Flow 侧布局编辑器，同名不同程序集）
- `Assets/Scenes/UITestSence.unity`（演示场景，可另作 Sample）

建议下一步（真正发版前）：

1. 将 `LivingUiLayoutId` 与默认蓝图根名改为可配置数据（SO / 字符串 id）。
2. 程序集/命名空间去 `NineGrid.` 前缀（需重绑序列化组件）。
3. 把 `Editor` 里 Legacy 迁移工具移出包，或放进 `Samples~`。
