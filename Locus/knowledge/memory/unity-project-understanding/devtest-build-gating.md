---
id: kd_655c816d-174a-4c19-b519-6f96c70c0e56
type: memory
path: unity-project-understanding/devtest-build-gating.md
title: devtest-build-gating
inheritInjectMode: true
summaryEnabled: true
commandEnabled: false
readOnly: false
inheritAiConfig: true
createdAt: 1785907952002
updatedAt: 1785909213629
---

# devtest-build-gating

## Summary
DevTest 组件 Release 构建门禁：运行时安装宿主模式、构建验证方法与既有 Missing Script 现状。

<!-- locus:body:start -->
# DevTest 构建门禁与运行时安装宿主（#126）

## 现状架构

- 所有 `NineGrid.DevTest` 组件（`*DevKeys`、`QuickTestEntryInputHandler`、`TestKeyStackHost`、`TestKeyInputPoller` 等）均 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 门控，Release 不编译。
- `TestKeyInputPoller` 自举（RuntimeInitializeOnLoadMethod 创建常驻对象）；`TestKeyManager` 是惰性单例；级联栈由 `Resources/DevTest/TestKeyStack` 定义，列表末项优先级最高。
- 场景/预制体序列化这些组件在 Release 即成 Missing Script。

## 运行时安装宿主模式（#126 落地）

- `DevTestSceneInstaller`（MainScene `Directors/DevTestHost`）：始终编译，`#if` 块内把 9 个 DevKeys + QuickTestEntryInputHandler AddComponent 到 6 个导演宿主；Release 空壳。
- `DevTestStandardCardInstaller`（`老Standard Card.prefab` 根）：每个实例 Awake 时补装 `StandardCardViewDevKeys`；该预制体经 `CardManagerSingleton.standardCardPrefab` 进正式构建（每张生成的卡都带）。
- `TestKeyModuleBehaviour.LayerProfile` 兜底：运行时安装（无序列化 profile）按 LayerId 从 `Resources/DevTest` LoadAll 匹配回填，级联栈显示名不变。
- 护栏测试 `DevTestSceneSerializationStructuralTests`：构建启用场景 + 全部 prefab 禁序列化 10 个 DevTest-only 组件 GUID；MainScene 恰好一个 SceneInstaller；底盘预制体须 CardInstaller。

## 构建验证方法（踩坑记录）

- Release：`unity command build --options '[]'`（必须 JSON 数组，裸字符串 `Development` 会被丢弃）；检查 `NinegridGambit_Data/Managed/NineGrid.DevTest.dll` 尺寸：Release ≈5KB（只有两个 Installer 宿主），Dev ≈60KB（含 DevTestCompileGate）。
- Development：`NineGrid.Presentation.Editor.DevPlayerBuild.QueueDevelopmentWindows64(path, true)` + 轮询 `GetStatusJson()`。注意 `player-connection-debug` 可能为 0（无 AllowDebugging 时），以 dll 载荷为准。
- 无头冒烟：`<exe> -batchmode -nographics -logFile <path>` 跑 ~45s 后 taskkill，grep 日志 `NineGrid.DevTest|DevKeys|QuickTest` 与 `different serialization layout`。
- **CLI 限制**：`unity command run_tests` 同步命令 CLI 侧 30s 连接超时（`--timeout` 全局选项无法抬高，放在 command 后无效）；长套件必须 `--async_tests true` 后轮询 `unity command test_status`（结果在 `Temp/pipeline_test_status.json`）。已中断的 sync run_tests 会让 pipeline server 挂死（所有命令 30s 超时、Editor 本体正常），需重启 Editor（先 `ai-workspace gate-restart`，再 `unity-automated-launch`）。
- 全量 EditMode 1258 例有 48 个既有失败（内容 JSON 校验、BattleSessionCheat 旧护栏、Trap/Taunt/Fusion 战斗逻辑），与本票无关，跑全量前先确认受影响套件。

## 既有 Missing Script（#126 已清理）

- MainScene 原有 214 处 Missing Script（已删除脚本 `NineGrid.VisualLook.TableNineSortingKey`，GUID d1f6b4c5…）：107 处 GUID 引用 + 107 处 `m_Script:{fileID:1115186359}` fileID-only 破坏引用 + 1 孤儿文档。**#126 已全部清除（214→0）**，Release/Dev 冒烟缺失行归零（321→0）。
- fileID-only 破坏引用标准 API 无法删除：`RemoveMonoBehavioursWithMissingScript` 与 `SerializedObject.DeleteArrayElementAtIndex`（拒绝 null 元素）均无效；解法是遍历 m_Component 访问 `objectReferenceInstanceIDValue` 后 `ApplyModifiedPropertiesWithoutUndo`，Unity 会自行丢弃破坏引用（会触发一次场景重载，注意 Editor 可能重连）。
<!-- locus:body:end -->
