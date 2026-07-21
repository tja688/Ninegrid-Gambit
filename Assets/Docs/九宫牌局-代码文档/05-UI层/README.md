# 05 · UI 层

> **2026-07-21**：灵动 UI（LivingUI）已抽出为嵌入式 UPM 包  
> **`Packages/com.livingui.stage/`** — 权威代码与包内文档在该路径。  
> 本目录只保留 **本仓库仍在用的 UI/Look 代码事实**，以及指向包的路由。

---

## 一句话

- **Living UI Stage**（独立包）：世界空间大盘载体构型转场与随行内容投影。  
- **VisualLook**（仍在 `Assets/Scripts/UI/VisualLook`）：URP 像素对齐/扫描线与文字逃逸相机栈。  
- **TmpBitmapPixelOutline**（`Assets/Scripts/UI/`，默认程序集）：Bitmap TMP 像素描边。

LivingUI 与 VisualLook **互不引用**；二者与 Flow/Cards **无编译耦合**。

---

## Living UI（已外置）

| 项 | 位置 |
| --- | --- |
| 包 | `Packages/com.livingui.stage/` |
| 安装说明 / 现状 | [`Packages/com.livingui.stage/README.md`](../../../../Packages/com.livingui.stage/README.md) |
| 包内索引 | [`Documentation~/README.md`](../../../../Packages/com.livingui.stage/Documentation~/README.md) |
| Spec 差距 / 解耦 | [`Documentation~/STATUS.md`](../../../../Packages/com.livingui.stage/Documentation~/STATUS.md) |
| 历史 Spec | [`Assets/Notes/归档/灵动UI架构-spec-2026-07-17.md`](../../Notes/归档/灵动UI架构-spec-2026-07-17.md) |
| 类型细表（迁移前快照） | [LivingUI/活字与世界UI.md](./LivingUI/活字与世界UI.md)（路径已过时，以包内文档为准） |

本仓库现状：

- `MainScene`：可玩主循环 **不依赖** `LivingUiDirector`；可有 VisualLook。  
- `UITestSence`：完整 LivingUI 验收舞台。  
- 业务壳（`UiPanelRouter` / `Panels` / `Anchors`）在 **Flow/Cards**，不是本包。

---

## VisualLook（仍在 Scripts）

见 [VisualLook/视觉Look.md](./VisualLook/视觉Look.md)。

程序集：`NineGrid.VisualLook`  
路径：`Assets/Scripts/UI/VisualLook/`  
依赖：URP Core/Universal、TextMeshPro。

---

## 默认程序集

| 路由 | 类型 |
| --- | --- |
| `Assets/Scripts/UI/TmpBitmapPixelOutline.cs` | `NineGrid.UI.TmpBitmapPixelOutline` |

---

## 目录现状

```
Packages/com.livingui.stage/     ← LivingUI 独立包（Runtime/Editor/Tests/Documentation~）
Assets/Scripts/UI/
├── VisualLook/                  ← 仍属本仓库 Look 管线
└── TmpBitmapPixelOutline.cs
```
