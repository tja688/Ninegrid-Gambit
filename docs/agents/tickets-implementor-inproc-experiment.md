# Tickets Implementor In-Process 实验报告

日期：2026-08-11

## 结论

内部子代理版值得保留，适合作为当前 Agent 会话内的 ticket 编排路径，但它不是外部 worker 的完全替代品。

它解决了外部版最明显的沟通摩擦：父 Agent 直接拿到子代理的 terminal result，不需要通过 stdout、PID、session 文件和 watcher 间接猜测“还活着还是已经死了”。Review 的 Standards / Spec 两轴也可以在同一父会话中并行启动，随后由父 Agent统一聚合。

它没有解决工作区隔离和硬取消。内部子代理与父 Agent共享同一 Git 工作区、Unity Editor 和宿主环境；提示词中的“只读”不是可靠的物理隔离。阶段安全必须继续由父 Agent持有的 `runId`、dirty baseline、exact HEAD、candidate artifact 和 Unity/Deliver 门禁保证。

推荐的实际定位：

- 干净工作区、希望少管理一个外部进程的普通 ticket：优先尝试内部模式。
- 需要跨宿主续跑、明确的进程树 kill、长于宿主 task runtime 的 Implement，或需要保留 OMP/OpenCode 原生 session：继续使用外部 `tickets-implementor`。
- 不要因为内部 task 返回成功就跳过 Git、Unity 和 artifact 验收。

## 落地产物

新增了用户级技能，不修改原外部技能：

```text
C:\Users\jinji\.agents\skills\tickets-implementor-inproc\SKILL.md
C:\Users\jinji\.agents\skills\tickets-implementor-inproc\reference.md
C:\Users\jinji\.agents\skills\tickets-implementor-inproc\scripts\inproc-state.ps1
C:\Users\jinji\.agents\skills\tickets-implementor-inproc\tests\test-inproc-ticket.ps1
```

内部版的关键协议：

- 父 Agent 是唯一编排器、artifact 接受者、Unity 验证拥有者和 Deliver 调用者。
- Implement / Fix 只能一个修改型子代理串行执行。
- Review 的 Standards / Spec 两个只读轴可以并行执行。
- 子代理写 `candidates/<runId>-<phase>-<attempt>.json`，父 Agent接受后才生成 canonical `implementation.json`、`review.json` 或 `fix.json`。
- 恢复不是外部 session resume，而是读取持久状态后启动 fresh task；同一阶段最多自动重派一次。
- `inproc-state.ps1` 默认拒绝 dirty 实施；只读 run 会记录并保护 dirty baseline。
- 阶段接受要求当前 `HEAD` 与 candidate commit 精确相等，不能只做 ancestor 检查。

## 实验记录

### 1. 内部状态脚本与 dirty gate

实验票：`9900`、`9901`

- `9900` 以 `-ReadOnly` 开始成功，记录了当时的 `baseSha=38afbe141b11ef7f8978ea3da52ab347273f200d` 和三处既有 dirty path。
- `9901` 不带 `-ReadOnly` 开始时被拒绝，说明修改型内部 ticket 不会在脏工作区直接派发子代理。
- `9900` 锁可显式释放，实验结束后没有留下活动 `inproc-worker.lock`。

### 2. 外部 OpenCode 默认 worker

实验票：`9911`

调用使用原技能默认配置：

```text
Platform: opencode
Model: opencode-go/deepseek-v4-flash
Session: ses_00edf1ffaffeiNV6M0U08PokFH
```

结果：

- worker 正常启动，日志中确认了工作目录、HEAD 和 `opencode · deepseek-v4-flash` 身份。
- worker 最终输出 `BACKEND_SMOKE_OK platform=opencode ticket=9911`，`worker-state.json` 的 `exitCode=0`。
- `session-smoke.txt` 正确捕获了 OpenCode session ID。
- 因为 smoke 阶段没有 `implementation.json` / `review.json` / `fix.json` 这类 phase artifact，`probe-worker.ps1` 最终输出 `FINAL DEAD ... exitCode=0`。

这个 `DEAD` 是监督器对“未知阶段产物”的保守结果，不是后端启动失败。它证明外部版的完成判据仍然绑定 ticket phase artifact；做 smoke 时应使用专门的 smoke artifact 或专门的 probe 语义，不能直接把该结果当成模型失败。

### 3. 外部 OMP 默认 worker

实验票：`9912`

调用使用原技能默认配置：

```text
Platform: omp
Model role: @ticketWorker
```

结果：

- worker 正常启动，OMP session JSONL 被写入 ticket 目录并被 `session-smoke.txt` 捕获。
- 原始 session 记录解析到实际模型 `portkey/gpt-5.6-terra`，thinking level 为 `high`。
- worker 日志输出 `BACKEND_SMOKE_OK platform=omp ticket=9912`，`worker-state.json` 的 `exitCode=0`。
- 与 OpenCode 一样，因为没有 smoke phase artifact，`probe-worker.ps1` 输出 `FINAL DEAD ... exitCode=0`。

结论是两个外部默认后端在本机都可用；但外部模式的正常完成仍要经过文件产物与 watcher 规则，额外的 session/PID/日志管理成本确实存在。

### 4. 内部 Implement candidate 通道

实验票：`9920`

通过当前会话的内部 `task` 子代理启动一个只读 fake Implement：

- 子代理直接返回了结构化的 candidate 路径、commitSha 和 changedPaths。
- 子代理只创建了指定 candidate，没有改业务文件、运行 Unity 或提交。
- `inproc-state.ps1 -Action check` 返回 `ok=true`，同时确认 `headMatches=true`、`baselineMatches=true`。
- 父 Agent释放了内部运行锁。

这验证了内部模式不需要通过外部 stdout 或 session list 获取阶段结果；父 Agent可以直接把 task terminal result 与磁盘 candidate、Git 状态对账。

### 5. 并行 Review 与共享工作区边界

实验票：`9921`、`9922`

`9921` 从带 dirty baseline 的旧 HEAD 启动两个并行只读 Review 轴。其中一个轴报告在审查期间观察到 HEAD 从 `38afbe1` 变为 `27eaa36`。该提交不是本实验创建的，实验没有回滚；父 Agent因此没有接受这次 review，随后释放锁。

`9922` 从当时干净的 `27eaa36a42400073d21b8a55f67280eab79c8640` 启动两个并行只读 Review 轴：

- Standards 和 Spec 两个 candidate 都正常生成。
- `aggregate-review` 正常合并两个轴，生成 `status=passed`、`findings=[]` 的 review candidate。
- 在接受 review 时，状态脚本发现 `Assets/Scripts/NineGrid.Presentation/Flow/Presentation/BattleVfxCues.cs` 作为新的未跟踪文件出现在工作区，拒绝接受 review。
- 该文件不是本实验脚本创建的，实验没有删除、修改或加入 Git。

这两个结果共同证明：内部 task 的上下文可以隔离，文件系统不能隔离。来源不明的提交或未跟踪文件都必须让父 Agent暂停，而不是依赖子代理的“只读”声明或自然语言结果。

## 当前工作区说明

实验期间检测到并发工作变化：

- `HEAD` 从 `38afbe1` 前进到 `27eaa36a4`，提交标题为 `准备接线特效`，对应远端 `origin/dev` 也已前进。
- 当前仍存在未跟踪文件 `Assets/Scripts/NineGrid.Presentation/Flow/Presentation/BattleVfxCues.cs`。
- 这些变化不是本次实验创建的，未被回滚或覆盖。
- 本次实验的运行时文件位于被 `.gitignore` 忽略的 `.opencode/tickets/`；原项目中三处用户修改也没有被本次实验重置。
- 最终状态检查还观察到其它 VFX 相关修改、`docs/code-map/presentation.md` 修改和 `.opencode` 临时文件；它们同样不是本实验创建的，未被纳入、清理或回滚。

## 已知限制

1. 当前内部 task 工具调用是父 Agent等待子代理 terminal 的同步协作形态；没有像外部 worker watcher 那样的独立 probe 进程。
2. 如果宿主只返回“取消请求已发送”而不能确认子代理已经停止，父 Agent不能安全地启动第二个修改型 task。
3. 内部 task 的实际 runtime、token、上下文和递归限制由宿主配置决定。本技能不设置预算，不等于宿主真的无限制。
4. 内部 task 的模型选择由当前宿主 task 配置决定，不能用外部 `-Platform opencode|omp` 参数改变。OpenCode/OMP 的默认模型只在外部后端实验中验证。
5. 目前 `deliver-ticket.ps1` 仍是原外部版脚本；内部状态脚本在 Deliver 前负责更严格的 run/candidate/exact HEAD 预检，父 Agent仍必须执行完整门禁。

## 后续建议

1. 真实 ticket 首次使用内部版时，只在 clean worktree 上做一张小票，观察子代理是否按 candidate 协议提交和结束。
2. 若 ticket 需要超过宿主内部 task 的实际 runtime，或必须可杀进程/可按 provider session resume，切回外部版。
3. 后续可以给父 Agent增加一个更高层的 orchestrator skill，让它自动调用 `inproc-state.ps1`、生成 capsule、等待 task、执行 Unity 验证和 Deliver；当前版本已经把协议和确定性状态边界准备好，但没有把宿主差异伪装成一个统一 API。
