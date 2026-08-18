# 特殊素材备份（Apollo 全量映射还原前）

备份时刻：2026-08-18。这些文件属于 `77e49b3c4` 全量映射批次，映射后像素又被改过。还原其余 PNG 时跳过它们，避免冲掉后续改图。

源提交：`77e49b3c4`（映射）← 父提交 `fd3c7878d`（本色）。当前分支 `dev`。

备份目录在 `Assets/` 外，Unity 不会当新图导入。相对路径与仓库内路径一致。

## 仍接线（7 张）

### Icey idle 01–05（刺客待机帧）

改动提交：`f8bb6709c`（2026-08-18，差不多了）

表现层：

- `Assets/Scripts/NineGrid.Presentation/Ui/CharacterSelectPanel.cs`  
  `AssassinIdleKey = "ContentArt/Png/像素怪物合集/sprites/Icey_idle_01"`
- `Assets/Scripts/NineGrid.Presentation/Ui/RunSummaryPanel.cs`  
  同源 `AssassinIdleKey`

内容 JSON（`animations.slots[].path`，folder 源）：

- `Assets/Arts/ContentVisual/cards/avatar_layla.json`
- `Assets/StreamingAssets/ContentVisual/cards/avatar_layla.json`  
  path：`Assets/Resources/ContentArt/Png/像素怪物合集/sprites/Icey_idle_01`

| 文件 | guid | 字节 |
|------|------|------|
| `Assets/Resources/ContentArt/Png/像素怪物合集/sprites/Icey_idle_01/Icey_idle_01_01.png` | `52e836939c9b82b448063d974a66827c` | 2362 |
| `…/Icey_idle_01_02.png` | `dbd0c845f3e23374e935be5469463b5f` | 2297 |
| `…/Icey_idle_01_03.png` | `76b9982e6c905dc4aadd410366778898` | 2316 |
| `…/Icey_idle_01_04.png` | `3a7440be66e65f0479225582ea310739` | 2376 |
| `…/Icey_idle_01_05.png` | `f436cbd04ccaf81458a45be91a7bd22c` | 2348 |

同目录 `.meta` 已一并复制。运行时按文件夹载入序列帧，不经 MainScene guid。

### F_UI_MenuIcons_A13.png

改动提交：`186798051`（2026-08-18，准备制作教程）

路径：`Assets/Arts/Images/Png/2D Pixel Quest Vol3_ The UI-GUI/Menu Icons/F_UI_MenuIcons_A13.png`  
guid：`74de33d5df2252846b937a8c482acd93`（16×16，229 字节）

MainScene 接线：

- GameObject `菜单按钮`
- GameObject `设置面板`

### F_UI_MenuIcons_A8.png

改动提交：`186798051`

路径：`Assets/Arts/Images/Png/2D Pixel Quest Vol3_ The UI-GUI/Menu Icons/F_UI_MenuIcons_A8.png`  
guid：`98397bacc2714df418e133febf55bbf1`（16×16，251 字节）

MainScene 接线：

- GameObject `回收图标`

## 像素改过、当前未接线（1 张）

### 6f3a84bd-…fixed.unfake.edit.png

改动提交：`448fe51e5`、`1e33df80b`（主菜单调整）

路径：`Assets/Arts/Images/Png/UI/6f3a84bd-413e-4f6d-b4b7-cc15b122e8bd.fixed.unfake.edit.png`  
guid：`7dbc8681828cc534a80556e10753d22c`（13315 字节）

当前 `MainScene.unity` 无此 guid。仍备份并跳过还原，避免误伤。

## 还原策略

对其余映射 PNG：`git checkout fd3c7878d -- <file.png>`，不改 `.meta`。上列 8 张保持 HEAD 像素；本目录是它们在还原前的最终形态副本。
