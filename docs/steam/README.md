# Steam 接入与上架准备

> 代码架构的权威描述见 [ADR-0043](../adr/0043-steam-platform-bridge.md) 与
> [`docs/code-map/README.md`](../code-map/README.md)（`NineGrid.SteamBridge` 行）。
> 本文是操作指南：已有什么、怎么用、注册开发者后要做什么。

## 已落地的基础设施（2026-08-12）

| 组件 | 位置 | 说明 |
|------|------|------|
| Steamworks.NET 2025.164.1 | `Packages/manifest.json`（UPM git 包 `com.rlabrecque.steamworks.net`） | Valve 官方 SDK 1.64 的 C# 包装 |
| 开发 AppId | 仓库根 `steam_appid.txt` = `480`（Spacewar 测试 App） | 只在开发期生效；正式 AppId 拿到后替换 |
| 平台抽象层 | `Assets/Scripts/NineGrid.Presentation/Flow/Platform/` | 业务只依赖这层；Steam 不可用时自动 no-op |
| Steam 桥 | `Assets/Scripts/NineGrid.SteamBridge/` | 初始化 / 成就 / 统计 / 云存档 / 平台信息，全部自动自举，场景无需摆任何东西 |

### 各系统行为

- **初始化**：进 Play / 启动游戏时自动 `SteamAPI.Init`。Steam 客户端没开、Init 失败 → 打一条
  Warning 后以无 Steam 模式继续，游戏行为完全不变。
- **成就 / 统计**：业务代码调 `PlatformAchievements.Unlock(AchievementIds.Xxx)` /
  `AddStat(StatIds.Xxx)` 即可（后端缺位时安全跳过）。成就 ID 目录在
  `Flow/Platform/AchievementIds.cs`，目前是占位草案。
- **云存档**：自动包在现有存档系统外面（跑图检查点、教学完成标记等全部经
  `RunSaveStoreHook` 的读写都被镜像）。写=本地+云双写；启动时双向同步、时间戳新者胜；
  云不可用时纯本地。**不需要**再在 Steamworks 后台配 Auto-Cloud 路径（我们走 Cloud API）。
- **平台信息**：`PlatformInfo.PlayerDisplayName` / `LanguageCode`（如 `schinese`，以后做本地化
  默认语言可直接用）/ `SetRichPresence`。

### 开发期怎么验证

Steam 客户端登录任意账号并保持运行，Editor 进 Play（Console 应出现
`[Steam] 初始化完成：<玩家名> (AppId 480)`），然后用菜单 **NineGrid → Steam**：

- **打印平台状态**：玩家、语言、云开关、云配额
- **解锁测试成就**：用 Spacewar 自带的 `ACH_WIN_ONE_GAME`，会真的弹 Steam 通知
- **重置全部成就与统计 (Dev)** / **列出云存档文件**

注意：挂 480 时 Steam 好友会看到你「正在玩 Spacewar」，正常现象。
占位成就（`ACH_TUTORIAL_COMPLETE` 等）在 480 上解锁会失败并打 Warning，也是正常现象。

## 注册开发者后的待办清单

### 1. 注册与建 App

- [ ] 注册 [Steamworks 合作伙伴](https://partner.steamgames.com/)（个人可注册；每个 App 100 美元入驻费，可退还条件见官方说明）
- [ ] 建 App 拿到正式 **AppId**

### 2. 代码侧替换（两处）

- [ ] `Assets/Scripts/NineGrid.SteamBridge/SteamAppIds.cs` → `Current = 正式 AppId`
- [ ] 仓库根 `steam_appid.txt` → 正式 AppId

### 3. Steamworks 后台配置

- [ ] **成就**：App Admin → Stats & Achievements，逐条创建成就，**API Name 必须与
  `AchievementIds.cs` 逐字一致**（先定稿游戏内成就设计，再同步两边；每个成就需
  已解锁/未解锁两张 64×64 图标）
- [ ] **统计**（如需要）：同页创建 INT 统计，对齐 `StatIds.cs`；成就可绑统计做进度条
- [ ] **Steam Cloud**：App Admin → Steam Cloud，设置「每用户字节配额」与「文件数上限」
  （存档是小 JSON，如 1MB / 100 个文件已非常宽裕）。我们用 Cloud API，
  **不要**配置 Auto-Cloud 根路径，避免与代码镜像重复同步
- [ ] 每次后台改动记得点 **Publish** 才生效

### 4. 构建与上传

- [ ] Windows 64 位 Build：Steamworks.NET 会自动把 `steam_api64.dll` 放进 Plugins，无需手动
- [ ] **不要**把 `steam_appid.txt` 拷进发行包（它会让正式版跳过「须经 Steam 启动」检查；
  代码里 `RestartAppIfNecessary` 已处理盗启重定向）
- [ ] 用 SteamPipe（`steamcmd` + app_build/depot_build 脚本）上传 Build 到 depot，设为 default 分支
- [ ] 后台勾选 DRM 与否随意（本项目未接 Steamworks DRM wrapper，单机小体量一般不接）

### 5. 商店与发行流程（与代码无关，供排期参考）

- [ ] 商店页素材（胶囊图、截图、预告片）、文案、标签、内容调查问卷（成人内容自评）
- [ ] 商店页提审（约 2-5 个工作日）+ Build 提审
- [ ] 「即将推出」页公开至少 2 周才能发售（Steam 规则）
- [ ] 定价、区域价、发售日期

## 后续开发建议（在现有基座上做）

1. **成就触发点接线**：成就设计定稿后，在流程事件处（教学完成 `TutorialProgressStore.MarkCompleted`、
   胜负收口 `GameFlowOrchestrator.ShowBattleEndAndReturnAsync` 等）调
   `PlatformAchievements.Unlock(...)`，一处一行，无需再动桥程序集。
2. **Rich Presence**：后台配置本地化 token 后，在进房/战斗等处调 `PlatformInfo.SetRichPresence`。
3. **Steam Deck**：主要是手柄输入与字体大小验证；如需官方「Deck 已验证」标记，走后台申请。
4. 玩家音频设置目前存 PlayerPrefs，不入云——属常见做法（设置跟设备走），如需云同步再议。
