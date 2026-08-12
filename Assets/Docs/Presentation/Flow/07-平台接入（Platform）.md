# Flow/Platform/ —— 平台成就 · 统计 · 平台信息抽象层

> 权威代码：`Assets/Scripts/NineGrid.Presentation/Flow/Platform/`（命名空间 `NineGrid.Flow.Platform`，共 3 个文件）
> 关联 ADR：[ADR-0043 Steam 平台桥](../../../../docs/adr/0043-steam-platform-bridge.md)

## 职责综述

本目录是业务侧对「平台系统」（当前即 Steam）的**唯一依赖面**：成就解锁、统计上报、玩家名/语言/Rich Presence 读取写入。全部走「接口 + 静态 Hook 装配缝 + 门面」三件套（与存档的 `IRunSaveStore` / `RunSaveStoreHook` 完全同款范式）：

- **接口**声明能力；
- **Hook** 是装配缝——生产后端由桥程序集 `NineGrid.SteamBridge` 在 `RuntimeInitializeOnLoad(BeforeSceneLoad)` 自举时注册（`SteamPlatformBootstrap`），本目录不认识任何 Steam 类型；
- **门面**是业务调用入口——后端缺位（Steam 未运行 / Init 失败 / 非桌面平台）时一律安全 no-op 或返回安全默认值。

**行为不变量（ADR-0043）**：平台可用性不得影响游戏规则与流程行为。业务程序集**不得 `using Steamworks`**——Steam 类型只允许出现在桥程序集。

## 关键类型表

| 类型 | 文件 | 一句话职责 |
|------|------|-----------|
| `AchievementIds`（静态类） | `Platform/AchievementIds.cs` | 成就 API Name 唯一代码目录（占位草案），须与 Steamworks 后台逐字一致 |
| `StatIds`（静态类） | `Platform/AchievementIds.cs` | 统计 API Name 目录（占位草案），约定同上 |
| `IPlatformAchievements` | `Platform/PlatformAchievements.cs` | 成就/统计后端接口：Unlock / IsUnlocked / SetStat / AddStat / GetStat / Flush / ResetAllForDev |
| `PlatformAchievementsHook`（静态类） | `Platform/PlatformAchievements.cs` | 装配缝：桥程序集注册后端；`BackendOrNull()` 取用 |
| `PlatformAchievements`（静态门面） | `Platform/PlatformAchievements.cs` | 业务调用入口；后端缺位时静默 no-op（`GetStat` 返回 0） |
| `IPlatformInfo` | `Platform/PlatformInfo.cs` | 平台信息接口：玩家显示名 / 语言代码 / PlayerId / Overlay 可用性 / Rich Presence 读写 |
| `PlatformInfoHook`（静态类） | `Platform/PlatformInfo.cs` | 装配缝，同上 |
| `PlatformInfo`（静态门面） | `Platform/PlatformInfo.cs` | 业务读取入口；缺位时返回 `string.Empty` / no-op |

## 核心流程与数据流

1. **装配**（发生在桥程序集，本目录只被动接收）：`SteamPlatformBootstrap`（`Assets/Scripts/NineGrid.SteamBridge/`）在 `RuntimeInitializeOnLoad(BeforeSceneLoad)` 依次 `RestartAppIfNecessary`（仅 Player）→ `SteamAPI.Init` → 挂每帧 `RunCallbacks` 泵 → `PlatformAchievementsHook.Set(new SteamAchievementsService())`、`PlatformInfoHook.Set(new SteamPlatformInfo())`。Init 失败则什么都不注册，门面全程 no-op。
2. **业务触发**：任何流程事件处（胜利 / 通关 / 教学完成等）直接调 `PlatformAchievements.Unlock(AchievementIds.XXX)` / `AddStat(StatIds.XXX)`，无需判空、无需感知平台状态。截至本文档写作时，**Flow 内尚无任何实际调用点**（`AchievementIds` 为占位草案，接线属纯业务改动，见 ADR-0043「后果」节）。
3. **退出收口**：由桥程序集的回调泵在应用退出（含 Editor 退 Play）时还原全部 Hook 并 `SteamAPI.Shutdown`，保证关闭 Domain Reload 时二次进 Play 状态干净。

## 对外通信面

- **被谁调用**：任何业务代码（门面是静态类，直接调用）。
- **调用谁**：仅调用 Hook 上注册的后端接口实现；本目录零依赖 Core / QFramework / Unity 场景对象。
- **Hook 性质**：`PlatformAchievementsHook` / `PlatformInfoHook` 是**装配缝而非业务 Sink**——只承载「谁来记成就」的装配，不读写任何规则状态（对齐 code-map「静态 Hook」纪律）。

## 关联 ADR

- ADR-0043（Steam 平台桥）：三件套范式、平台缺位零影响、AppId 单点（`SteamAppIds.Current` + 根目录 `steam_appid.txt`，当前 480 Spacewar 占位）、云存档装饰器（在 SteamBridge 侧，不在本目录）。
- ADR-0041（跑图存档）：`IRunSaveStore` / `RunSaveStoreHook` 是本范式的先例。

## 不变量与坑

- `AchievementIds` / `StatIds` 的字符串必须与 Steamworks 后台 App Admin → Stats & Achievements 配置的 API Name **完全一致**；注册正式 AppId、定稿成就设计后按最终清单增删。
- 当前开发用 AppId 480（Spacewar）只有它自带的测试成就；本目录里的占位 ID 在 480 上解锁会失败并打 Warning（**正常现象**）。`AchievementIds.DevSpacewarWinOneGame`（`ACH_WIN_ONE_GAME`）是 Spacewar 真实成就，开发期可用它验证解锁链路（会真的弹 Steam 通知；Editor 菜单 `NineGrid/Steam/`）。
- `IPlatformAchievements.Flush()` 对应 Steam `StoreStats`——成就/统计变更是本地缓存的，须 Flush 才推送平台。
- `ResetAllForDev()` 是 Dev 专用清空当前账号全部成就统计，切勿接进正式 UI。
