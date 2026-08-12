# Platform 桥接（NineGrid.SteamBridge + NineGrid.SaveBridge）

> 权威代码事实快照 · 2026-08-12 · 覆盖 `Assets/Scripts/NineGrid.SteamBridge/`（7 个 .cs）与 `Assets/Scripts/NineGrid.SaveBridge/`（1 个 .cs），全部逐文件通读核实。

## 职责综述

这两个目录承载「游戏 ↔ 外部平台/插件」的全部胶水代码，共同遵循仓库的**装配缝范式**：业务程序集只依赖 `NineGrid.Presentation` 内的抽象接口（`IRunSaveStore`、`IPlatformAchievements`、`IPlatformInfo`）与静态 Hook（`RunSaveStoreHook`、`PlatformAchievementsHook`、`PlatformInfoHook`），第三方 SDK 类型（Steamworks.NET、Easy Save 3）被隔离在桥接侧，经 `RuntimeInitializeOnLoad` 自举注册，**无任何场景序列化对象**。

- **NineGrid.SaveBridge**：跑图存档（ADR-0041 战斗开始检查点）的 Easy Save 3 落盘后端。ES3 插件无 asmdef、只能从 Assembly-CSharp 调用，因此本目录**刻意不放 asmdef**（编入 Assembly-CSharp），这是硬约束不是疏漏。
- **NineGrid.SteamBridge**：Steam 平台桥（ADR-0043）。成就/统计、平台信息（玩家名/语言/Rich Presence）、云存档镜像三件事的 Steam 后端，外加每帧回调泵与 Editor 验证菜单。核心行为不变量：**平台缺位零影响**——Steam 客户端未运行 / Init 失败 / 原生库缺失时静默降级，所有 Hook 不注册，游戏与无 Steam 完全一致。

## 关键类型表

| 类型 | 文件（相对 `Assets/Scripts/`） | 一句话职责 |
|------|------|------|
| `Es3RunSaveStore` | `NineGrid.SaveBridge/Es3RunSaveStore.cs` | `IRunSaveStore` 的 ES3 实现；`SubsystemRegistration` 时经 `RunSaveStoreHook.Set` 注册；落盘 `persistentDataPath/NineGridSaves/slot_{id}.es3`，单键 `snapshot` 存快照 JSON |
| `SteamPlatformBootstrap` | `NineGrid.SteamBridge/SteamPlatformBootstrap.cs` | `BeforeSceneLoad` 自举：`Packsize/DllCheck` 校验 → `RestartAppIfNecessary`（仅 Player）→ `SteamAPI.Init` → 挂回调泵 → 注册成就/平台信息后端 → 用云装饰器包住既有存档后端并 `SyncOnBoot`；失败即静默降级；`ShutdownFromPump` 还原全部 Hook + `SteamAPI.Shutdown` |
| `SteamAchievementsService` | `NineGrid.SteamBridge/SteamAchievementsService.cs` | `IPlatformAchievements` 的 Steam 后端（`ISteamUserStats`）：Unlock 幂等（已解锁即返回）、SetStat/AddStat/GetStat/Flush、`ResetAllForDev`；API Name 后台未定义只打 Warning |
| `SteamPlatformInfo` | `NineGrid.SteamBridge/SteamPlatformInfo.cs` | `IPlatformInfo` 的 Steam 后端：玩家名、语言码、SteamID、Overlay 可用性、Rich Presence 读写 |
| `SteamCloudRunSaveStore` | `NineGrid.SteamBridge/SteamCloudRunSaveStore.cs` | `IRunSaveStore` 云镜像**装饰器**（`ISteamRemoteStorage`）：写=本地+云双写；读=本地优先、云端兜底并回填；`SyncOnBoot` 双向同步按文件时间戳新者胜（容差 2 秒）；云不可用时纯透传本地 |
| `SteamCallbackPump` | `NineGrid.SteamBridge/SteamCallbackPump.cs` | 常驻 `DontDestroyOnLoad` MonoBehaviour：每帧 `SteamAPI.RunCallbacks()`；`OnApplicationQuit`（含 Editor 退 Play）调 `SteamPlatformBootstrap.ShutdownFromPump` 收口 |
| `SteamAppIds` | `NineGrid.SteamBridge/SteamAppIds.cs` | AppId 单点常量：`Current = 480`（Spacewar 占位）；正式 AppId 须同步改仓库根 `steam_appid.txt` |
| `SteamDebugMenu` | `NineGrid.SteamBridge/SteamDebugMenu.cs` | Editor 菜单 `NineGrid/Steam/`（须 Play Mode + Steam 客户端）：打印平台状态、解锁 Spacewar 测试成就、重置全部成就统计、列云存档文件 |

## 核心流程与数据流

### 启动装配链（顺序敏感）

1. `Es3RunSaveStore.Install`（`RuntimeInitializeLoadType.SubsystemRegistration`，最早）→ `RunSaveStoreHook.Set(es3)`。
2. `SteamPlatformBootstrap.Install`（`BeforeSceneLoad`，晚于上一步）→ Init 成功后：
   - `SteamCallbackPump.Ensure()` 建常驻泵；
   - `PlatformAchievementsHook.Set(SteamAchievementsService)`、`PlatformInfoHook.Set(SteamPlatformInfo)`；
   - 取出 `RunSaveStoreHook.StoreOrNull()`（即 ES3 后端），构造 `SteamCloudRunSaveStore(inner)` 装饰，`SyncOnBoot()` 后 `RunSaveStoreHook.Set(cloudStore)`。
3. 退出（含 Editor 退 Play）：泵的 `OnApplicationQuit` → `ShutdownFromPump`——Hook 全部还原（云装饰器还原为内层 ES3）+ `SteamAPI.Shutdown()`，保证关闭 Domain Reload 时二次进 Play 状态干净。

### 存档读写路径

`RunSaveService`（`NineGrid.Presentation/Flow/GameFlow/RunSave/`）→ `RunSaveStoreHook` → （有 Steam 时）`SteamCloudRunSaveStore` → `Es3RunSaveStore` → ES3 文件。云文件名 `saves/slot_{id}.json` 存**原始快照 JSON**，与 ES3 格式解耦；两侧 slotId 净化规则（非字母数字下划线连字符一律换 `_`）在两个类中各有一份完全相同的实现，保证文件名对齐。

### 云同步裁决

`SyncOnBoot` 对本地/云槽位取并集：仅一侧有 → 补齐另一侧；两侧都有 → 比时间戳（云取 `GetFileTimestamp`，本地取 `slot_*.es3` 的 `LastWriteTimeUtc` 转 unix 秒），差距 >2 秒者以新盖旧。任何异常只降级为本地模式，不阻断启动。

## 对外通信面

- **被谁调用**：无人直接调用这两个程序集——全部经抽象缝反向注册。存档消费方是 `RunSaveService`（检查点/自动档/手动槽/读档）与 `TutorialProgressStore`（教学完成标记，独立槽 `tutorial_profile`，同走 `RunSaveStoreHook`）；成就/平台信息消费方是业务侧门面 `PlatformAchievements` / `PlatformInfo`（`NineGrid.Presentation/Flow/Platform/`，后端缺位时安全 no-op）。
- **调用谁**：Steamworks.NET UPM 包（`com.rlabrecque.steamworks.net`）、ES3 全局 API、`NineGrid.Presentation` 的接口与 Hook 类型。
- **程序集边界**：`NineGrid.SteamBridge.asmdef` 只引用 `NineGrid.Presentation` + Steamworks 包，`includePlatforms` 限 Editor + 三大桌面平台；每个文件另有 `DISABLESTEAMWORKS` 平台守卫（非桌面平台整文件空编译）。业务程序集**不得** `using Steamworks`。

## 关联 ADR

- [ADR-0043](../../../docs/adr/0043-steam-platform-bridge.md) — Steam 平台桥：装配缝接入、平台缺位零影响、云=装饰器、AppId 单点。
- [ADR-0041](../../../docs/adr/0041-run-save-battle-start-checkpoint.md) — 跑图存档：战斗开始检查点、快照内容单一真源在 Core（`RunSaveGame.cs`）、ES3 桥装配缝、自动档生命周期。
- [ADR-0042](../../../docs/adr/0042-tutorial-level-module.md) — 教学完成标记走同一存档后端的独立槽位。

## 不变量与坑

1. **`NineGrid.SaveBridge` 目录必须保持无 asmdef**。加了 asmdef 就引用不到 ES3（ES3 编入 Assembly-CSharp），编译直接断。
2. **平台可用性不得影响游戏规则与流程行为**（ADR-0043）。任何新平台功能都要保证「Init 失败 = 完全没有这回事」。
3. **AppId 换正式号要改两处**：`SteamAppIds.Current` + 仓库根 `steam_appid.txt`（后者仅开发期生效）。当前 480 占位下解锁项目自定义成就会失败打 Warning，属正常现象。
4. **新增跨战斗持久状态必须同步 `RunSaveSnapshot` + `RestoreAfterCreate`**（并递增 `version`），否则读档静默丢失（ADR-0041 后果条款，review 检查单）。
5. **云冲突「新者胜」依赖本地系统时钟**：本地时间戳来源是文件 `LastWriteTimeUtc`，改时钟可导致旧进度覆盖云端新进度——ADR 已声明单机场景可接受。
6. **可疑点（阅读代码时发现，未见 ADR 覆盖）**：`SteamCloudRunSaveStore.Delete` 仅在 `CloudAvailable` 时删云文件。若删除发生在 Steam 离线/云暂不可用时（例如终局清自动存档那一刻），云端旧档残留，下次 `SyncOnBoot` 会把已删除的存档从云端拉回本地——已结束的 run 自动档可能「复活」。目前无墓碑（tombstone）机制。
7. `SteamDebugMenu` 全部功能须 Play Mode（Steam 自举发生在进 Play 时），非 Play 下只打 Warning。
8. 云读取回填（`TryRead` 云命中时 `mInner.Write`）会刷新本地文件时间戳——之后的新者胜比较里本地会显得「更新」，正常情况下两侧内容一致无影响，但排查同步问题时要意识到这点。
9. Steamworks SDK 1.61 起用户统计随 Init 自动拉取，代码里**没有也不需要** `RequestCurrentStats`（`SteamAchievementsService` 注释已声明）。

## 文件覆盖清单（8 / 8）

| 文件 | 说明 |
|------|------|
| `Assets/Scripts/NineGrid.SaveBridge/Es3RunSaveStore.cs` | 跑图存档 ES3 落盘后端；SubsystemRegistration 自举注册进 `RunSaveStoreHook` |
| `Assets/Scripts/NineGrid.SteamBridge/SteamPlatformBootstrap.cs` | Steam 自举与收口总入口；Init 失败静默降级；注册/还原全部平台 Hook |
| `Assets/Scripts/NineGrid.SteamBridge/SteamAchievementsService.cs` | 成就/统计 Steam 后端（ISteamUserStats），Unlock 幂等、失败只警告 |
| `Assets/Scripts/NineGrid.SteamBridge/SteamPlatformInfo.cs` | 玩家名/语言/SteamID/Overlay/Rich Presence 的 Steam 后端 |
| `Assets/Scripts/NineGrid.SteamBridge/SteamCloudRunSaveStore.cs` | 云存档镜像装饰器：双写/本地优先/启动双向同步/时间戳新者胜（容差 2s） |
| `Assets/Scripts/NineGrid.SteamBridge/SteamCallbackPump.cs` | 常驻回调泵：每帧 RunCallbacks；OnApplicationQuit 收口 Shutdown |
| `Assets/Scripts/NineGrid.SteamBridge/SteamAppIds.cs` | AppId 单点常量（当前 480 Spacewar 占位） |
| `Assets/Scripts/NineGrid.SteamBridge/SteamDebugMenu.cs` | Editor 菜单 `NineGrid/Steam/`：状态打印/测试成就/重置/列云文件（须 Play Mode） |
