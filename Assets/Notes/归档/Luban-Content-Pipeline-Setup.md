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

### R3 接入程度（2026-06-18 复检）

| 能力 | 状态 | 说明 |
|:--|:--|:--|
| Luban 生成器 + 表定义 + 最小 JSON 样例 | 已落地 | `Assets/Tools/Luban` + `StreamingAssets/TableNine/LubanData` |
| 生成代码 → `GameContentCatalog` 映射 | 已落地 | `TableNineLubanCatalogFactory` |
| `IContentSystem` 消费 Luban catalog | 已落地 | `P6LubanContentTests` 验证 parse/validate/draft |
| 运行时默认内容源 | **未切换** | 仍依赖 `TableNineContentCatalog` 硬编码全量内容 |
| 统一引导入口 | **已补** | `ContentCatalogBootstrap.Load(...)`；注册仍由调用方经 `IConfigUtility` 注入 |
| 全量内容迁表 | **未开始** | Luban 目前仅 3 卡 / 5 效果样例，远小于 catalog |
| 改数值只动配表 | **未达成** | 主内容仍在 C# `TableNineContentCatalog.cs` |

结论：**R3 管线骨架和接入口已通，但尚未成为主内容源**。当前合理策略是 `Hardcoded` 继续服务 P6 全量逻辑，`Luban` 保持样例通道，后续按批次把 `TableNineContentCatalog` 搬进 `Assets/Tools/Luban/Datas`。

当前不足主要不在 Luban 管线，而在内容表达和校验硬度：

- `TableNineContentCatalog` 仍是全量硬编码内容源，本次只接了一块最小真表。
- 大量 P6 内容仍是 `PendingAtom`，迁移到 Luban 后也只会更清楚地暴露 pending，不会自动变成可执行效果。
- `EffectValidator` 已补 atom schema 校验（未知 atom / 缺字段 / 参数范围）；表迁移前可先跑 representative catalog DSL 单测。
- 长远看，效果 DSL 可以从字符串列升级为 Luban 多态 bean；但现在直接字符串承载更适合快速迁移现有 JSON DSL。

建议下一步顺序：

1. 把 `TableNineContentCatalog` 的硬编码内容分批搬进 `Assets/Tools/Luban/Datas`。
2. 每批搬迁后跑 `gen_table_nine.ps1` 和 `NineGrid.Core.Tests`。
3. 等 pending burn-down 稳定后，再把 `P6ContentLandingTests` 从“允许 pending”改成“pending 必须为 0”。
4. 全量迁表完成前，运行时默认 `Load(Hardcoded)` 后注入 `IConfigUtility`。
