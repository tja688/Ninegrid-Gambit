# 九宫牌局 · 代码文档库（DEPRECATED）

> **DEPRECATED（2026-07-23）**：本页及本树不再是代码现状权威。请改读 [`docs/code-map/`](../../../../docs/code-map/README.md) 与 [`docs/adr/`](../../../../docs/adr/)。  
> 下文为 #28 前镜像快照，可能与源码不符；**冻结，勿再同步。**

---

## 一句话现状

自研代码约 **450+** 个 `.cs`（不含 Plugins / QFramework），按程序集切成：**无引擎依赖的 QFramework 内核（Core）** → **Luban 内容桥（Content）** → **Unity 流程编排（Flow）** → **卡牌视图与场地（Cards）**，辅以 **VisualLook**、嵌入式包 **Living UI Stage**（`Packages/com.livingui.stage`）与 **DevTest** 小键盘层。

---

## 文档库地图（一级路由）

| 编号 | 目录 | 覆盖源码 | 说明 |
|------|------|----------|------|
| 00 | [总览与路由](./) | 全局 | INDEX、程序集依赖、命名空间对照 |
| 01 | [内核 NineGrid.Core](../01-内核-NineGrid.Core/) | `NineGrid.Foundation/NineGrid.Core` | 规则/状态/效果/命令；QFramework 黑盒 |
| 02 | [内容层 Content](../02-内容层-NineGrid.Content/) | `NineGrid.Content` (+ Editor) | Catalog + Luban 生成表 |
| 03 | [流程层 Flow](../03-流程层-Flow/) | `Scripts/Flow` | 对局循环、表现时间线、HUD/选择器 |
| 04 | [卡牌表现 Cards](../04-卡牌表现层-Cards/) | `Scripts/Cards` | 手牌/牌库/场地/飞行/视图（**不引用 Core**） |
| 05 | [UI 层](../05-UI层/) | `Scripts/UI` + `Packages/com.livingui.stage` | VisualLook；LivingUI 已外置为包 |
| 06 | [DevTest](../06-DevTest/) | `NineGrid.DevTest` | 小键盘测试栈 |
| 07 | [测试体系](../07-测试体系/) | `*.Tests` | EditMode 契约/切片测试地图 |
| 08 | [Editor 与工具链](../08-Editor与工具链/) | Editor asmdef、`Assets/Editor`、Luban Tools | 编辑器窗与数据管线 |
| 09 | [跨层契约与依赖](../09-跨层契约与依赖/) | 交叉引用 | 谁调用谁、边界违规点 |
| 10 | [遗留临时与第三方](../10-遗留临时与第三方边界/) | Temporary / Plugins / QF | 清理候选与外部边界 |

---

## 源码树（Scripts 顶层事实）

```
Assets/Scripts/
├── NineGrid.Foundation/
│   ├── NineGrid.Core/          (~66 cs)  程序集 NineGrid.Core
│   ├── NineGrid.Core.Tests/
│   ├── NineGrid.Content/       (~48 cs)  程序集 NineGrid.Content
│   ├── NineGrid.Content.Editor/
│   ├── NineGrid.DevTest/       (~30 cs)
│   └── NineGrid.DevTest.Tests/
├── Flow/                       (~102 cs) 程序集 NineGrid.Flow
├── Cards/                      (~138 cs) 程序集 NineGrid.Cards
├── UI/
│   ├── VisualLook/             程序集 NineGrid.VisualLook
│   └── TmpBitmapPixelOutline.cs（默认程序集）
└── Temporary Test/             程序集 NineGrid.TemporaryTest（1 cs）

Packages/
└── com.livingui.stage/         程序集 NineGrid.LivingUI（+ Editor / Tests）
```

---

## 推荐阅读顺序（摸清现状）

1. 本页 + [程序集与依赖.md](./程序集与依赖.md)  
2. [01 Core README](../01-内核-NineGrid.Core/README.md)（规则真相）  
3. [02 Content README](../02-内容层-NineGrid.Content/README.md)（数据如何进内核）  
4. [03 Flow README](../03-流程层-Flow/README.md) + Presentation 时间线  
5. [04 Cards README](../04-卡牌表现层-Cards/README.md)（纯表现）  
6. [09 跨层契约](../09-跨层契约与依赖/README.md)（重构切割线）  
7. [06 DevTest](../06-DevTest/README.md) / [07 测试](../07-测试体系/README.md)（验证面）

---

## 规模快照（`.cs` 计数，含子目录测试）

| 区域 | 约计 |
|------|------|
| NineGrid.Core | 66 |
| NineGrid.Content (+ Generated) | 48 |
| NineGrid.Content.Editor | 10 |
| NineGrid.Core.Tests | 25 |
| NineGrid.DevTest (+ Editor) | 30 |
| Flow（含 Presentation/Diagnostics/Editor/Tests） | 102 |
| Cards（含 Battle/Convergence/Effects/Editor/Tests） | 138 |
| UI（LivingUI + VisualLook） | 33 |
| Temporary Test | 1 |

*计数来自目录扫描，精确清单见各模块「文件清单」节。*

---

## 文档元信息

| 项 | 值 |
|----|-----|
| 生成方式 | 总代理编排 + 多子代理纯代码考古 |
| 排除输入 | `Assets/Docs/九宫牌局*` 策划文、`Assets/Notes`、skills/规划 |
| 权威优先级 | **本库 > 口头记忆 > 旧笔记**（针对「代码现状」问题） |
| 覆盖校验 | `Assets/Scripts/**/*.cs` 共 454 个文件名均在本库文档中可检索到（0 遗漏） |
| 文档篇数 | 38 篇 Markdown（含本目录树） |
