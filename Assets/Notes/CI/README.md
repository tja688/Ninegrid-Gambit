# NineGrid Core 测试验收（本地）

## 结论：Unity EditMode Test Runner（本地验收，无 CI / 无 dotnet test）

项目**不维护**独立 `dotnet test` 工程，**不接入 CI**。Core 验收统一走 Unity EditMode，开发时在编辑器 Test Runner 跑，需要命令行时在本地执行 batchmode 脚本。

当前仓库实际情况：

| 因素 | 说明 |
|:--|:--|
| `NineGrid.Core` | `noEngineReferences: true`，源码本身不引用 `UnityEngine` |
| `NineGrid.Core.Tests` | Editor + `TestAssemblies`，走 Unity Test Framework |
| `QFramework` | 框架源码含 `using UnityEngine`，Core 无法脱离 Unity 程序集单独编译 |
| 根目录 `*.csproj` / `*.sln` | 由 Unity 按需生成，已在 `.gitignore` 中忽略，不入库 |

因此 **正式验收链路定为 Unity EditMode Test Runner**（编辑器内或本地 batchmode），不维护独立的 `NineGrid.Core.csproj` / `NineGrid.Core.Tests.csproj`，也不做 CI 集成。

## 覆盖范围

程序集：`NineGrid.Core.Tests`（EditMode）

| 测试类 | 阶段 |
|:--|:--|
| `P0InfrastructureTests` | P0 |
| `P0ArchitectureGuardTests` | P0（架构守门） |
| `P1ModelTests` | P1 |
| `P2StatPipelineTests` | P2 |
| `P3ActionPipelineTests` | P3 |
| `P4NodeFlowTests` | P4 |

当前共 **28** 条用例（以 Test Runner 实际列出为准）。

## 架构守门（P3/P4）

在跑 EditMode 测试前，可先执行静态守门（无需打开 Unity）：

PowerShell：

```powershell
.\Assets\Notes\CI\check-core-guards.ps1
```

Bash：

```bash
./Assets/Notes/CI/check-core-guards.sh
```

守门规则（`rg` 扫描 `Assets/Scripts/NineGrid.Core`）：

| 检查 | 说明 |
|:--|:--|
| `noEngineReferences` | `NineGrid.Core.asmdef` 必须为 `true` |
| 禁止 `UnityEngine` | Core 源码不得 `using UnityEngine` 或写 `UnityEngine.*` |
| System 不经 Action 改 Model | `Systems/*.cs` 中不得直接调用棋盘/牌堆/玩家等突变 API；白名单：`ActionPipelineSystem`、`TriggerSystem`、`StatSystem`（P2 修饰器入口） |

同一规则在 EditMode 中有镜像测试：`P0ArchitectureGuardTests`（3 条）。

`run-core-tests.ps1` / `run-core-tests.sh` 会在启动 Unity batchmode **之前**自动跑守门脚本。

## 本地验收

### 方式 A：Unity 编辑器（开发时）

1. 打开项目 `MainScene` 所在工程。
2. **Window → General → Test Runner**。
3. **EditMode** 页签，筛选程序集 `NineGrid.Core.Tests`，运行全部。

或通过 Unity MCP：`run_tests(mode=EditMode, assembly_names=["NineGrid.Core.Tests"])`。

### 方式 B：batchmode（本地命令行，可选）

**先关闭已打开本工程的 Unity 编辑器**（同一工程不能双开）。

PowerShell（Windows）：

```powershell
.\Assets\Notes\CI\run-core-tests.ps1
```

Bash（macOS / Linux 本地）：

```bash
./Assets/Notes/CI/run-core-tests.sh
```

可选环境变量：

| 变量 | 含义 | 默认 |
|:--|:--|:--|
| `UNITY_PATH` | `Unity.exe` / `Unity` 可执行文件完整路径 | 按 `ProjectSettings/ProjectVersion.txt` 在 Hub 目录查找 |
| `NINEGRID_TEST_ASSEMBLY` | 测试程序集名 | `NineGrid.Core.Tests` |

产物目录：`Assets/Notes/CI/artifacts/`（已 gitignore）

- `ninegrid-core-editmode-results.xml` — NUnit 格式结果
- `unity-test.log` — Unity 日志

退出码：

| 码 | 含义 |
|:--|:--|
| `0` | 全部通过 |
| `1` | 脚本/环境错误（找不到 Unity 等） |
| `2` | 有用例失败 |
| 其他 | Unity batchmode 原始退出码 |

## 维护说明

- 新增 Core 单测：放在 `Assets/Scripts/NineGrid.Core.Tests/`，保持 `NineGrid.Core.Tests.asmdef` 引用。
- 修改验收命令时同步更新本 README 与 `run-core-tests.ps1` / `run-core-tests.sh`。
- batchmode 失败时先查看 `artifacts/unity-test.log` 与 `artifacts/ninegrid-core-editmode-results.xml`。
