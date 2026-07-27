# Unity CLI 协作

本项目 Agent **优先用 Unity CLI + Pipeline** 驱动已打开的 Editor；不要依赖 Unity MCP。

项目级硬规则见 `.cursor/rules/unity-cli.mdc`（alwaysApply）。

## 前提

1. 本机已安装 `unity` CLI（`unity --version` 可用）。
2. 项目已装 `com.unity.pipeline`（可用 `unity pipeline install --project-path <本仓库>`）。
3. **Unity Editor 已打开本工程**，且须带 **`-automated`**（Hub 双击不够）。启动见 `.cursor/skills/unity-automated-launch/`（通用 skill，可移植）。
4. `unity pipeline list` 显示 Pipeline 可达。
5. Editor **不得处于 Safe Mode**（见下节）。

## 严禁 Safe Mode

**不准以 Safe Mode 启动，也不准在 Safe Mode 下继续 Agent 操作。**

- 编译错误弹「Enter Safe Mode」时：选 **Ignore** 或 **Quit**，不要 Enter Safe Mode。
- 启动或重开后用下面命令确认（期望 `false`）：

```bash
unity command eval "return UnityEditor.EditorUtility.isInSafeMode;" --project-path "<本仓库绝对路径>" --format json
```

- 若已是 Safe Mode：停手 → 退出 Editor → 修好编译错误 → 再用 `unity-automated-launch` 以正常模式（`-automated`）重开。

## 首选调用

```bash
# 发现实例与命令面
unity pipeline list
unity command --project-path "<本仓库绝对路径>"

# 执行（一律建议 --format json）
unity command <name> [args...] --project-path "<本仓库绝对路径>" --format json

# 无现成命令时：Roslyn eval（不触发整项目重编）
unity command eval "return UnityEngine.Application.unityVersion;" --project-path "<本仓库绝对路径>" --format json
```

无头/后台改脚本、跑测前先开 autotick：

```bash
unity command set_autotick --enable true --project-path "<本仓库绝对路径>"
```

## 常见循环

改 C# → 编译 → 看 Console / 跑测：

```bash
unity command recompile --project-path "<本仓库>"
unity command recompile_status --project-path "<本仓库>"   # 轮询至完成
unity command console --project-path "<本仓库>" --format json
unity command run_tests --mode editor --filter <Name> --project-path "<本仓库>" --format json
```

多 AI 同仓时：**不要**直接 `run_tests`；用 `.cursor/skills/ai-workspace/` 的 `test`（互斥 + 可共享结果），关/重开 Editor 前先 `gate-restart`。


改场景 / GameObject / 组件：用 `create_gameobject`、`find_gameobjects`、`add_component`、`set_component_properties` 等 Pipeline 命令。

## Development Player 打包（重要）

Pipeline 的 `build --options Development`（普通字符串）**不可靠**：CLI 把 `options` 当 `string` 传入，服务端转 `string[]` 失败后**静默丢弃**，落到默认的 `DetailedBuildReport`（不含 `BuildOptions.Development`）。重复 `--options` 也只留最后一个。结果是包能打出来，但 `DEVELOPMENT_BUILD` 未定义，`#if DEVELOPMENT_BUILD` 热键/DevTest 被剥掉（典型症状：`NineGrid.DevTest.dll` ≈ 4KB）。

**推荐**：项目内 `DevPlayerBuild`（菜单或 eval），不依赖 CLI 数组绑定：

```bash
# 排队异步 Development 包（含 CleanBuildCache）
unity command eval "return NineGrid.Presentation.Editor.DevPlayerBuild.QueueDevelopmentWindows64(\"Builds/DevWin64/NinegridGambit.exe\", true);" --project-path "<本仓库>" --format json

# 轮询至 completed
unity command eval "return NineGrid.Presentation.Editor.DevPlayerBuild.GetStatusJson();" --project-path "<本仓库>" --format json
```

菜单：`NineGrid/Build/Development Windows64 Player`。

**也可用** Pipeline `build`，但必须把 options 写成 JSON 数组字符串（服务端见 `[` 才会重解析）：

```powershell
unity command build --project-path "<本仓库>" --format json `
  --target StandaloneWindows64 `
  --outputPath "Builds/DevWin64/NinegridGambit.exe" `
  --options '["Development","CleanBuildCache","AllowDebugging","DetailedBuildReport"]' `
  --confirm true
# 再 poll: unity command build_status ...
```

打完后自检（期望 DevTest DLL 远大于 4KB；`boot.config` 应有 `player-connection-debug=1`）：

```powershell
$dev = "Builds/DevWin64/NinegridGambit_Data/Managed/NineGrid.DevTest.dll"
$ascii = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($dev))
"size=$((Get-Item $dev).Length) DevTestGate=$($ascii.Contains('DevTestCompileGate'))"
```

不要写 `unity command build --options Development`（无方括号）——那不会带上 Development。

## 与 MCP 的关系

| | Unity CLI (`unity command`) | Unity MCP |
|---|---|---|
| 本项目默认 | **是** | 否（可选） |
| 能力来源 | `com.unity.pipeline` | 多为同一能力的协议包装 |
| Agent 用法 | Shell 直接调用 | Cursor Tools 面板 |

Cursor 的 `mcp.json` 若存在，勿被 `unity mcp configure cursor` 覆盖成错误的顶层键 `servers`（Cursor 需要 `mcpServers`）。能力协作以 CLI 为准即可。

## 参考

- 包内命令索引：`Library/PackageCache/com.unity.pipeline@*/Documentation~/index.md`
- 包内 agent skill：`Library/PackageCache/com.unity.pipeline@*/.claude/skills/unity-pipeline/SKILL.md`
