# Unity CLI（MCP 回退通道）

协作政策见 [`.cursor/rules/unity-cli.mdc`](../../.cursor/rules/unity-cli.mdc)。本文仅列 **CLI 回退**时的命令。

## 前提

- `unity` CLI 可用；项目已装 `com.unity.pipeline`
- Editor 已开本工程且带 `-automated`（见 `.cursor/skills/unity-automated-launch/`）

## 常用

```bash
unity pipeline list
unity command recompile --project-path "<repo>"
unity command recompile_status --project-path "<repo>"
unity command console --project-path "<repo>" --format json
unity command eval "return UnityEngine.Application.unityVersion;" --project-path "<repo>" --format json
```

场景/GO/组件：`find_gameobjects`、`create_gameobject`、`add_component`、`set_component_properties` 等。

## Development Player 打包

`unity command build --options Development`（无 JSON 数组）**不可靠**——`Development` 会被静默丢弃，`DEVELOPMENT_BUILD` 未定义。

**推荐**：`NineGrid/Build/Development Windows64 Player` 菜单，或：

```bash
unity command eval "return NineGrid.Presentation.Editor.DevPlayerBuild.QueueDevelopmentWindows64(\"Builds/DevWin64/NinegridGambit.exe\", true);" --project-path "<repo>" --format json
```

**备选**：`--options '["Development","CleanBuildCache","AllowDebugging","DetailedBuildReport"]'`（须为 JSON 数组字符串）。

## Release Player 打包

`NineGrid/Build/Release Windows64 Player` 菜单，或桌面正式版 `NineGrid/Build/Release Windows64 Player (Desktop/game2)`：

```bash
unity command eval "return NineGrid.Presentation.Editor.ReleasePlayerBuild.QueueReleaseWindows64(NineGrid.Presentation.Editor.ReleasePlayerBuild.GetDesktopGame2OutputPath(), true);" --project-path "<repo>" --format json
```

桌面纯净存档正式版 `NineGrid/Build/Release Windows64 Player (Desktop/game3 - Clean Save)`（自动清空存档与教学标记）：

```bash
unity command eval "NineGrid.Presentation.Editor.ReleasePlayerBuild.ClearAllSaveDataAndTutorialProfile(); NineGrid.Presentation.Editor.ReleasePlayerBuild.CleanDesktopGame3OutputFolder(); return NineGrid.Presentation.Editor.ReleasePlayerBuild.QueueReleaseWindows64(NineGrid.Presentation.Editor.ReleasePlayerBuild.GetDesktopGame3OutputPath(), true);" --project-path "<repo>" --format json
```

## 参考

- `Library/PackageCache/com.unity.pipeline@*/Documentation~/index.md`
