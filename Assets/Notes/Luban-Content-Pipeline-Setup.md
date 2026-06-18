# Luban Content Pipeline Setup

日期：2026-06-18

## 本次落地

- 已通过 Unity Package Manager 安装 `com.code-philosophy.luban` runtime。
- 本地 Luban 生成器版本：`4.9.0`，路径默认使用 `C:\Users\jinji\Desktop\应用\Luban\Luban.exe`。
- 新增项目内 Luban 工作区：
  - `Assets/Tools/Luban/luban.conf`
  - `Assets/Tools/Luban/Defines/table_nine.xml`
  - `Assets/Tools/Luban/Datas/*.json`
  - `Assets/Tools/Luban/gen_table_nine.ps1`
  - `Assets/Tools/Luban/gen_table_nine.bat`
- 生成输出目录：
  - 代码：`Assets/Scripts/NineGrid.Content/Generated/Luban`
  - 数据：`Assets/StreamingAssets/TableNine/LubanData`
- 新增 `TableNineLubanCatalogFactory`，负责将 `cfg.Tables` 映射为现有 `GameContentCatalog`。
- 新增 `P6LubanContentTests.GeneratedLubanTablesCanFeedContentSystem`，验证生成 JSON 能进入 `IContentSystem`，并通过现有 DSL 校验和 `CreateDraft`。

## 使用方式

运行：

```powershell
.\Assets\Tools\Luban\gen_table_nine.ps1
```

如果 Luban 不在默认路径，可用环境变量或参数覆盖：

```powershell
$env:LUBAN_EXE = "D:\Tools\Luban\Luban.exe"
.\Assets\Tools\Luban\gen_table_nine.ps1
```

## 当前表形状

本次先建立最小可跑通的 P6 表形状：

- `TbEffect`
- `TbCard`
- `TbSkill`
- `TbRelic`
- `TbRewardPool`
- `TbRewardEntry`
- `TbMonsterDeck`
- `TbNodeDeckRule`
- `TbRoom`
- `TbEconomy`

效果 DSL 当前作为 `json` 字符串列保存，生成后仍交由 Core 的 `EffectSystem.ParseJson` 和 `EffectValidator` 处理。这是最小侵入方案：Luban 负责策划数据管线，Core 继续负责效果语义。

## 探查结论

架构设想中的“策划表落地”可以继续推进，不需要推倒现有 Core。现有程序面已经暴露出足够的接入口：

- `IConfigUtility` 能注入 `GameContentCatalog`。
- `ContentSystem.TryReloadFromConfig()` 已经会从配置取 catalog。
- `ContentSystem.ValidateCatalog()` 能复用现有效果校验。
- `CreateDraft / ApplyContentToCard / ActivateRelic / ActivatePlayerSkill` 都能消费 catalog。

当前不足主要不在 Luban 管线，而在内容表达和校验硬度：

- `TableNineContentCatalog` 仍是全量硬编码内容源，本次只接了一块最小真表。
- 大量 P6 内容仍是 `PendingAtom`，迁移到 Luban 后也只会更清楚地暴露 pending，不会自动变成可执行效果。
- `EffectValidator` 还没有逐 atom schema 校验，表迁移前建议先补“未知 atom / 缺字段 / 参数范围”的硬校验。
- 长远看，效果 DSL 可以从字符串列升级为 Luban 多态 bean；但现在直接字符串承载更适合快速迁移现有 JSON DSL。

建议下一步顺序：

1. 把 `TableNineContentCatalog` 的硬编码内容分批搬进 `Assets/Tools/Luban/Datas`。
2. 每批搬迁后跑 `gen_table_nine.ps1` 和 `NineGrid.Core.Tests`。
3. 等 pending burn-down 稳定后，再把 `P6ContentLandingTests` 从“允许 pending”改成“pending 必须为 0”。
