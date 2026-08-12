# ADR-0043 Steam 平台桥：成就 / 统计 / 云存档经装配缝接入，平台缺位零影响

- 状态：Accepted（2026-08-12）
- 关联：[ADR-0041](0041-run-save-battle-start-checkpoint.md)（跑图存档与 `IRunSaveStore` 装配缝）

## 背景

游戏为单机 Roguelike，准备上架 Steam，需要成就、统计、云存档等常规平台系统；不做联机。
仓库已有「接口 + 静态 Hook 装配缝 + 桥程序集」范式（`IRunSaveStore` / `RunSaveStoreHook` → ES3 桥），
Steam 集成复用同一范式，避免业务代码与 Steamworks SDK 耦合。

## 决策

### 1. 业务只依赖平台抽象，禁止直接引用 Steamworks

平台抽象在 `NineGrid.Presentation` 的 `Flow/Platform/`（命名空间 `NineGrid.Flow.Platform`）：

- `IPlatformAchievements` + `PlatformAchievementsHook` + 门面 `PlatformAchievements`（成就 / 统计）
- `IPlatformInfo` + `PlatformInfoHook` + 门面 `PlatformInfo`（玩家名 / 语言 / Rich Presence）
- `AchievementIds` / `StatIds`：成就与统计 API Name 的**唯一**代码目录，须与 Steamworks 后台配置逐字一致

业务触发点（胜利 / 通关 / 教学完成等）只调门面；门面在后端缺位时安全 no-op。
**业务程序集不得 `using Steamworks`**——Steam 类型只允许出现在桥程序集。

### 2. Steam 实现隔离在 `NineGrid.SteamBridge` 桥程序集

- 依赖 Steamworks.NET UPM 包（`com.rlabrecque.steamworks.net`），仅 Editor + 三大桌面平台编译；
  文件级 `DISABLESTEAMWORKS` 守卫对齐 Steamworks.NET 官方模式。
- `SteamPlatformBootstrap` 在 `RuntimeInitializeOnLoad(BeforeSceneLoad)` 自举：
  `RestartAppIfNecessary`（仅 Player）→ `SteamAPI.Init` → 挂常驻回调泵（每帧 `RunCallbacks`）→ 注册各 Hook。
- **行为不变量：平台可用性不得影响游戏规则与流程行为。**
  Steam 客户端未运行 / Init 失败 / 原生库缺失时静默降级：Hook 不注册、本地存档照常，游戏与无 Steam 完全一致。
- 应用退出（含 Editor 退 Play）经回调泵显式还原全部 Hook 并 `SteamAPI.Shutdown`，
  保证关闭 Domain Reload 的 Enter Play Mode 下二次进 Play 状态干净。

### 3. 云存档 = 既有本地后端的装饰器，冲突新者胜

`SteamCloudRunSaveStore : IRunSaveStore` 包住 ES3 桥（不替换、不感知 ES3 细节，仅按 ADR-0041
的落盘布局取本地文件时间戳）：

- 写：本地 + 云双写（云文件 `saves/slot_{id}.json`，存原始快照 JSON，与 ES3 格式解耦）
- 读：本地优先；本地缺失且云端存在时回填本地
- 启动：双向同步，仅一侧有则补齐，两侧都有按文件时间戳**新者胜**（容差 2 秒）
- 账号或 App 云功能关闭时纯透传本地；云写失败只打 Warning，本地存档不受影响

### 4. AppId 与后台配置的单点替换位

- `SteamAppIds.Current`（桥程序集）+ 仓库根 `steam_appid.txt`：当前为测试 App 480（Spacewar），
  注册正式 AppId 后只改这两处。
- 成就 / 统计后台定义以 `AchievementIds` / `StatIds` 为对照清单。

## 后果

- 后续接成就触发点是纯业务改动：在流程事件处调 `PlatformAchievements.Unlock(...)` 即可，无需再动桥。
- 未注册正式 AppId 前，占位成就在 480 上解锁失败只打 Warning（正常现象）；
  链路验证用 Spacewar 自带 `ACH_WIN_ONE_GAME`（Editor 菜单 `NineGrid/Steam/`）。
- 云存档策略「新者胜」意味着旧设备离线玩后联网，会覆盖云端较旧进度——单机单人场景可接受；
  若未来需要更细冲突 UI，在装饰器一处扩展。
