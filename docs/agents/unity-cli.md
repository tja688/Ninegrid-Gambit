# Unity CLI 协作（MCP 回退通道）

本项目 Agent **优先 Unity MCP**；本文档描述 **CLI + `com.unity.pipeline` 回退** 时的用法。硬规则见 [`.cursor/rules/unity-cli.mdc`](../../.cursor/rules/unity-cli.mdc)。

## 何时走 CLI

- MCP 未连接、缺能力、或调用超时 / 卡死
- MCP 无法完成的 Pipeline 专有命令（如批量 `recompile_status` 轮询）
- 需要 Shell 脚本化的一长串 Pipeline 命令

MCP 可用且能完成任务时，**不必**改走 CLI。

## 前提

1. 本机已安装 `unity` CLI（`unity --version` 可用）。
2. 项目已装 `com.unity.pipeline`（`unity pipeline install --project-path <本仓库>`）。
3. **Unity Editor 已打开本工程**，且须带 **`-automated`**。启动见 [`.cursor/skills/unity-automated-launch/`](../../.cursor/skills/unity-automated-launch/)。
4. `unity pipeline list` 显示 Pipeline 可达。
5. Editor **不得处于 Safe Mode**（见 rule）。

## 严禁 Safe Mode

- 编译错误弹「Enter Safe Mode」时：选 **Ignore** 或 **Quit**。
- 确认不在 Safe Mode：

```bash
unity command eval "return UnityEditor.EditorUtility.isInSafeMode;" --project-path "<本仓库绝对路径>" --format json
```

- 若已是 Safe Mode：停手 → 退出 Editor → 修好编译 → `-automated` 重开。

## 常用调用

```bash
unity pipeline list
unity command --project-path "<本仓库绝对路径>"

unity command <name> [args...] --project-path "<本仓库绝对路径>" --format json

unity command eval "return UnityEngine.Application.unityVersion;" --project-path "<本仓库绝对路径>" --format json
```

改 C# → 编译 → Console：

```bash
unity command recompile --project-path "<本仓库>"
unity command recompile_status --project-path "<本仓库>"
unity command console --project-path "<本仓库>" --format json
```

改场景 / GameObject / 组件：`create_gameobject`、`find_gameobjects`、`add_component`、`set_component_properties` 等。

验证约定见 [`docs/code-map/tests.md`](../code-map/tests.md)。

## Development Player 打包（重要）

Pipeline 的 `build --options Development`（普通字符串）**不可靠**：CLI 把 `options` 当 `string` 传入，服务端转 `string[]` 失败后**静默丢弃**，落到默认的 `DetailedBuildReport`（不含 `BuildOptions.Development`）。重复 `--options` 也只留最后一个。结果是包能打出来，但 `DEVELOPMENT_BUILD` 未定义，`#if DEVELOPMENT_BUILD` 热键/DevTest 被剥掉（典型症状：`NineGrid.DevTest.dll` ≈ 4KB）。

**推荐**：项目内 `DevPlayerBuild`（菜单或 eval）：

```bash
unity command eval "return NineGrid.Presentation.Editor.DevPlayerBuild.QueueDevelopmentWindows64(\"Builds/DevWin64/NinegridGambit.exe\", true);" --project-path "<本仓库>" --format json

unity command eval "return NineGrid.Presentation.Editor.DevPlayerBuild.GetStatusJson();" --project-path "<本仓库>" --format json
```

菜单：`NineGrid/Build/Development Windows64 Player`。

**也可用** Pipeline `build`，但必须把 options 写成 JSON 数组字符串：

```powershell
unity command build --project-path "<本仓库>" --format json `
  --target StandaloneWindows64 `
  --outputPath "Builds/DevWin64/NinegridGambit.exe" `
  --options '["Development","CleanBuildCache","AllowDebugging","DetailedBuildReport"]' `
  --confirm true
```

打完后自检（期望 DevTest DLL 远大于 4KB）：

```powershell
$dev = "Builds/DevWin64/NinegridGambit_Data/Managed/NineGrid.DevTest.dll"
$ascii = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($dev))
"size=$((Get-Item $dev).Length) DevTestGate=$($ascii.Contains('DevTestCompileGate'))"
```

不要写 `unity command build --options Development`（无方括号）。

## MCP 与 CLI 对照

| | Unity MCP | Unity CLI (`unity command`) |
|---|---|---|
| 默认 | **首选** | MCP 不可用 / 缺能力 / 卡死时回退 |
| 典型用法 | Cursor MCP 工具、`mcpforunity://` 资源 | Shell + Pipeline 命令 |
| 能力来源 | MCP 服务器包装 Editor / Pipeline | `com.unity.pipeline` 直连 |

`mcp.json` 若存在，勿被 `unity mcp configure cursor` 覆盖成错误的顶层键 `servers`（Cursor 需要 `mcpServers`）。

## 参考

- 包内命令索引：`Library/PackageCache/com.unity.pipeline@*/Documentation~/index.md`
- 包内 agent skill：`Library/PackageCache/com.unity.pipeline@*/.claude/skills/unity-pipeline/SKILL.md`
