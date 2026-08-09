# 会话 ses_020c0d422fferFahsBPpIUS3KQ

- **标题**: Ticket Implementor 稳健拆分与 Unity 自动化改进
- **目录**: C:/Users/jinji/Documents/GitHub/Ninegrid Gambit
- **创建**: 2026-08-08 10:41
- **更新**: 2026-08-08 11:58

## 文件改动

（无文件改动）

## 玩家指令

### 指令 1

我在实际跑 ticket implumentor 技能时发现会话时间过长（omp后端+opencode的那个，不是cursor的那个），让AI深度分析探查后给出的解决方案，你抓到这个技能，结合unity 本身的一些情况，还有 ticket implumentor 涉及到的两个真实的外部技能，去落地下一些改进，我这边主要的担心在两个点：1、AI提出的unity自动化log流程脚本很不错，节约了很多思考轮次，但是稳
 定性我有点担心，比如多个AI同时跑、竞态如何处理、卡了超时如何处理等。2、AI提出的拆分code review流程我觉得不错，但是我以前经验告诉我好像是
 implument这个外部技能触发的 code review，你看下到底能不能稳定的拆分？需不需要涉及对外部skill
 的修改？这个skill是人家的，我还会跟着同步更新，所以我比较抗拒涉及侵入性修改的方案，你看下我们自己的编排有没有方法比较做到稳健的这种拆分式的
 任务编排？ 其他的没啥问题，你在简要思考分析上述两个担忧后自己去下一个判断，落地改进，落地完了简单虚构任务串来测试下流程稳定性

### 指令 2

Called the Read tool with the following input: {"filePath":"C:\\Users\\jinji\\Documents\\GitHub\\Ninegrid Gambit\\Assets\\Notes\\票据技能改进.md"}

### 指令 3

**结论**
这次 OMP worker 并没有长期卡死。真正的问题是：**一个 ticket 被执行成了 200 到 286 次串行模型响应，每次约 30 到 40 秒**。工具本身通常只运行几秒，但每次读文件、改代码、查编译、轮询测试、等待 reviewer 后，都会再次调用 Luna Max。

所以 2 到 3 小时不是模型完成业务所需的“硬耗时”，而是：

> 大量细粒度操作 × 每轮 Luna Max 响应延迟 × 超长上下文 × 重复验证与审查。

会话2抓到了“模型档位、review、测试、探活”这些方向，但对各项贡献的判断需要修正。

**运行实账**

| Ticket | 主会话墙钟 | 主模型响应次数 | 模型响应累计 | 工具实际累计 | 结束时上下文 |
|---|---:|---:|---:|---:|---:|
| #167 | 210.1 分钟 | 286 | 183.8 分钟 | 25.4 分钟 | 464K tokens |
| #168 | 128.9 分钟 | 206 | 120.8 分钟 | 8.3 分钟 | 469K tokens |
| #169 | 137.7 分钟 | 230 | 136.4 分钟 | 1.3 分钟 | 491K tokens |
| #170 | 171.1 分钟 | 283 | 165.1 分钟 | 5.9 分钟 | 501K tokens |

模型延迟分布：

- 单轮平均约 35 到 39 秒。
- 中位数约 29 到 33 秒。
- P95 约 68 到 94 秒。
- 2 到 4.5 分钟是少数极端响应，不是每轮常态。
- 每票主模型生成了约 20 万到 30 万 output tokens。
- 模型累计与工具累计可能超过墙钟，因为子代理、并行工具和主代理存在重叠。

这证明：**主耗时是模型被调用了几百轮，而不是工具执行慢。**

**阶段拆账**
#167：

| 阶段 | 墙钟 | 主模型轮次 |
|---|---:|---:|
| 实现 | 112.7 分钟 | 150 |
| 验证 | 53.8 分钟 | 78 |
| Review 与修订 | 40.6 分钟 | 51 |
| 关票 | 1.2 分钟 | 2 |

#168：

| 阶段 | 墙钟 | 主模型轮次 |
|---|---:|---:|
| 探索 | 9.1 分钟 | 18 |
| 首次实现 | 25.2 分钟 | 29 |
| 首次验证 | 20.7 分钟 | 36 |
| Review、修订、再验证 | 70.0 分钟 | 114 |
| 关票 | 1.4 分钟 | 2 |

#169：

| 阶段 | 墙钟 | 主模型轮次 |
|---|---:|---:|
| 探索 | 19.3 分钟 | 19 |
| 首次实现 | 23.6 分钟 | 34 |
| 验证、两轮 Review、修订 | 91.6 分钟 | 170 |
| 关票 | 不到 2 分钟 | 3 |

#170：

- 探索约 23 分钟。
- 后续实现、验证、Review 共约 146 分钟。
- 这一段产生了 243 次主模型响应。

#168 和 #169 的首次实现其实都只用了约 25 分钟。**把票拖到两小时以上的是后面的验证和 review 循环。**

**主要根因**

1. **数百次模型回合**
当前 OMP 模式大致是：

```text
模型思考 35 秒
→ 读一个或几个文件
→ 模型思考 35 秒
→ 再读
→ 模型思考 35 秒
→ 改一小块
→ 模型思考 35 秒
→ 编译
→ 模型思考 35 秒
→ 查编译状态
...
```

四票分别调用了 376 到 434 次工具，但主模型每轮平均只发出约两个工具调用。工具结果回来后，Luna Max 会重新进行一次响应。

最典型的是验证相关调用：

| Ticket | 编译/刷新 | 启动测试 | 查询测试 | Console | 验证调用总数 |
|---|---:|---:|---:|---:|---:|
| #167 | 48 | 6 | 7 | 10 | 71 |
| #168 | 14 | 13 | 24 | 2 | 53 |
| #169 | 31 | 14 | 23 | 4 | 72 |
| #170 | 19 | 11 | 0 | 16 | 46 |

如果每个工具结果只触发一次约 35 秒的模型响应，这部分就会制造约 27 到 42 分钟的模型等待。问题不是单次编译，而是**每个编译、状态查询、测试轮询都经过模型编排**。

2. **全程使用 Luna Max**
当前配置：

- `ticketWorker = portkey/gpt-5.6-luna:max`
- `reviewer = portkey/gpt-5.6-luna:max`
- `subagent_capable = portkey/gpt-5.6-luna:max`
- `defaultThinkingLevel = max`

见 `C:\Users\jinji\.omp\agent\config.yml:10-13,31`。

Max 并不是只用于架构决策，而是用于：

- 看 `git status`
- 查测试状态
- 读一个文件
- 修一个 using
- 判断是否继续轮询
- 写关票说明

绝大多数这些回合不需要 Max。

3. **Review 实际跑了两轮**
#167、#168、#169 都产生了四个 reviewer session：

```text
StandardsReview + SpecReview
→ 主代理修复
→ Verify/FixStandards + Verify/FixSpec
→ 主代理继续修复和验证
```

旧配置下每个 reviewer 都被 15 分钟截止：

| Ticket | 第一对 Review | 第二对 Review |
|---|---:|---:|
| #167 | 15 分钟 | 15 分钟 |
| #168 | 15 分钟 | 15 分钟 |
| #169 | 15 分钟 | 15 分钟 |
| #170 | 10.8 / 13.3 分钟 | 未跑第二轮 |

同一对是并行的，所以两轮约增加 30 分钟墙钟，而不是 60 分钟。但 reviewer 返回后，主代理还会继续修订、重编译和重测，因此 #168 的完整 review 阶段达到 70 分钟。

将 `task.maxRuntimeMs` 提高到 60 分钟可以减少强杀和报告丢失，但它只是**可靠性修复，不是性能修复**。如果 reviewer 仍然拿 Luna Max 扫大 diff，长尾反而可能从 15 分钟扩大到 40 到 60 分钟。

4. **超长上下文持续膨胀**
四票结束时主 session 都达到约 460K 到 500K prompt tokens。

原因包括：

- 票全文和技能全文。
- 大量代码读取结果。
- Unity 编译和测试输出。
- reviewer 状态和报告。
- 重复的 `git status`、测试状态、Console。
- 修订前后的代码内容。

后期一个简单决定也要在接近 500K 的会话上执行。缓存降低了输入成本，但没有消除模型生成时间和复杂上下文带来的决策迟滞。

5. **一条 session 承担了太多职责**
当前 worker 一次性负责：

```text
读票
→ 探索
→ 设计
→ 实现
→ 验证
→ 提交
→ 启动两个 reviewer
→ 接收报告
→ 修复
→ 再启动 reviewer
→ 再验证
→ 回填 issue
→ 关票
```

这使后半程必须在已经非常膨胀的上下文里继续工作。`implement` 技能也明确要求全量测试和 code review，见：

- `implement/SKILL.md:11-13`
- `code-review/SKILL.md:58-80`

严格“一票一个长寿命 worker”对 OMP 不是理想粒度。

6. **Session 定位存在真实 bug**
`launch-worker.ps1:176-191` 每秒递归寻找“最新写入的 JSONL”，并写入 `session.txt`：

```powershell
Get-ChildItem ... -Filter "*.jsonl" -Recurse |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1
```

一旦主 worker 启动 reviewer，最新 JSONL 就会变成 `StandardsReview.jsonl` 或 `SpecReview.jsonl`。因此 `session.txt` 会从主 session 漂移到子 session。

这在 #167 已经真实发生，watcher 最初抓到的是 `AudioStandardsReview.jsonl`，而不是主 session。

#168 更值得注意：

- 台账记录 20:07 启动了一次 resume。
- 主 JSONL 从 18:08 到 20:17 连续存在。
- 主 JSONL 只有最初一条 user prompt，没有 resume prompt。
- 主 session 最终在
…[截断，共 9633 字]

### 指令 4

我开启了会话：串行 ticket 落地工作流 

他开了：#173 已派发（OMP, pid=98560）

你去抓一眼，我感觉没啥动静啊（30分钟 git 0 变动），一个是看他的技能是不是加载的你改过后的最新的技能，二个是看一眼他的worker的情况，看是不是出问题了

## AI 总结返回

### 回复 1 

我会先核对 `tickets-implementor`、`implement`、`code-review` 三个真实技能的调用边界，再检查当前票据 runner/watchdog 与 Unity 验证入口。重点是把改动限制在我们自己的编排层，不分叉外部技能，同时为并发、锁、超时和阶段交接建立可验证的确定性状态。

### 回复 2 

判断已经比较明确：不修改 `implement` 或 `code-review` 外部技能。`implement` 确实会主动触发 `code-review`，但我们可以在票据 worker 的阶段契约中明确覆盖它的最后一步，让实现 worker 在提交后退出，再由全新 review worker 显式加载原版 `code-review`；这不需要 fork，也不会影响后续同步。

我会落地三组最小改动：修正主 session 捕获并加入真实进程状态与项目级互斥；增加带 Unity 独占锁、终态轮询、两击放弃和紧凑 JSON 的验证脚本；把技能流程改成 Implement、Review、条件式 Fix、Deliver 四阶段，并用假 OMP/假 Unity 串一次并发与超时烟测。

### 回复 3 

编排层现在已经切开，外部两个技能没有改动。验证阶段也不再让模型自己轮询 Unity，而是单次调用脚本等待终态；脚本会先抢项目级 Unity 锁、对比编译前后 Console，并在两次失败后明确返回 `inconclusive`，不会继续绕路重试。

下一步是纯烟测：先做 PowerShell 语法检查，再用假 OMP 制造“主 session + 嵌套 reviewer session”和并发二次启动，用假 Unity 制造成功、锁竞争与超时，确认状态机按预期收敛。

### 回复 4 

已在我们自己的 `tickets-implementor` 编排层落地改进，没有修改外部 `implement` 或 `code-review` 技能。

**核心判断**
- `implement` 的确明确要求完成后调用 `code-review`。
- 不需要侵入修改外部技能。实现阶段提示词现在明确声明：只执行 `implement` 的实现、验证和提交部分，最终 review 由上层编排接管。
- Review 由新的独立 worker 显式加载原版 `code-review`。它内部仍按原设计并行运行 Standards / Spec 两个 sub-agent。
- 这种拆法的稳定边界是阶段 artifact，而不是依赖模型口头判断是否进入下一阶段。

**已落地**
- 重写 `C:\Users\jinji\.agents\skills\tickets-implementor\SKILL.md`
- 修改 `scripts/launch-worker.ps1`
- 修改 `scripts/probe-worker.ps1`
- 新增 `scripts/verify-unity.ps1`
- 新增 `scripts/deliver-ticket.ps1`

新流程：

```text
Implement 独立 session
→ Review 独立 session
→ 仅有 P0/P1 时启动 Fix 独立 session
→ 确定性 Deliver 脚本关票
```

**Unity 稳定性**
`verify-unity.ps1` 将原本需要模型多轮编排的流程收敛成一次终态调用：

```text
获取 Unity 独占锁
→ 读取编译前 Console baseline
→ 请求 recompile
→ 脚本内部轮询 recompile_status
→ 读取编译后 Console
→ 比较新增 Error / Exception / Assert
→ 返回一行紧凑 JSON
```

处理了以下风险：

- 使用 `.opencode/tickets/unity-verify.lock`，同一项目的多个 AI 不会同时驱动 Unity 验证。
- 锁持有进程消失后可自动回收陈旧锁。
- 等锁超时返回 `blocked`，不会擅自并发执行。
- 同一动作最多尝试两次。
- 两次仍超时或状态不明返回 `inconclusive`，落实项目的“两击放弃”规则。
- 编译前后比较 Console 指纹，已有旧错误不会直接被误判为本票新增错误。
- `passed`、`failed`、`blocked`、`inconclusive` 都有不同退出码和结构化结果。

**进程与竞态**
`launch-worker.ps1` 现在：

- 使用项目级 `active-worker.lock`，阻止两个 watcher 同时修改共享 worktree。
- 按 `implement`、`review`、`fix` 分别保存 prompt、log 和 session。
- OMP session 只选择第一个顶层 JSONL。
- 不再递归寻找最新 JSONL，因此 reviewer session 不会覆盖主 session。
- 将主 session 写入 `session-<phase>.txt` 后不再漂移。
- `worker-state.json` 持续记录：
  - phase
  - runner PID
  - engine host PID
  - 主 session
  - heartbeat
  - 最后主 session 活动时间
  - exit code
- OMP 硬上限随阶段调整：
  - Implement：75 分钟
  - Review：20 分钟
  - Fix：40 分钟
  - Smoke：10 分钟

`probe-worker.ps1` 现在以真实 PID、主 JSONL 活性和阶段 artifact 判断状态，不再只看外层 PowerShell 与 `worker.log`。

**Review 拆分**
阶段间通过三个结构化 artifact 交接：

- `implementation.json`
- `review.json`
- `fix.json`

完整双轴 review 默认只跑一轮。只有 `P0/P1` 才启动 Fix，不再默认跑第二轮双轴 review。确实需要复审时，只复审有争议的单轴 finding。

`deliver-ticket.ps1` 是唯一允许关票的入口，会检查：

- 实现阶段 Unity 验证为 `passed`
- 实现 commit 位于当前 HEAD 历史
- Review artifact 合法
- 所有 P0/P1 都被 Fix 声明处理
- Fix 后 Unity 验证为 `passed`

模型 worker 已被禁止直接执行 `gh issue close`，避免提前关票或重复关票。

**虚构流程烟测**
使用假 OMP 和假 Unity 在临时目录跑了以下场景：

1. 假 OMP 创建顶层 `main.jsonl`，随后创建更新时间更晚的嵌套 `subagents/reviewer.jsonl`。
   - 结果：`session-implement.txt` 始终指向 `main.jsonl`。

2. 一个 30 秒 worker 运行时启动第二个 ticket worker。
   - 结果：第二次启动被项目锁拒绝，并报告现有 ticket、phase 和 PID。

3. Worker 正常退出。
   - 结果：`exitCode=0`，runner 已消失，项目锁自动释放。

4. 阶段 artifact 写入后探活。
   - 结果：返回 `FINAL PHASE_COMPLETE`，不会因为 issue 尚未关闭而误判 DEAD。

5. 假 Unity 正常编译。
   - 结果：返回 `status=passed`。

6. 两个 Unity 验证同时启动。
   - 结果：第一个正常完成，第二个返回 `status=blocked`，没有并发操作 Editor。

7. Unity 编译状态永不进入终态。
   - 结果：恰好尝试两次，然后返回：

```json
{
  "status": "inconclusive",
  "reason": "recompile status timeout; two-attempt rule reached",
  "attempts": 2
}
```

8. Deliver artifact 使用不存在的假 commit。
   - 结果：门禁拒绝关票，证明是 fail-closed。

所有四个 PowerShell 脚本均通过 PowerShell AST 语法解析。没有运行真实 ticket、没有关闭真实 issue，也没有修改当前 Unity 项目工作树中的现有改动。

### 回复 5 

我先核对当前 watcher 实际加载到的技能文本、`#173` 的阶段产物与 `worker-state.json`，再直接检查 PID 98560 的进程树、主 OMP JSONL 最近事件和日志尾部。这样可以区分“技能还是旧版”“worker 正常慢跑”“主 session 漂移”或“进程已经假活”。

### 回复 6 

已经抓到一个明确问题：当前磁盘上的技能是新版，但 `#173` 实际收到的是旧版 worker 提示词。进程命令行仍要求“一生一票、implement 后内嵌 code-review、worker 自己关票”，所以 watcher 会话显然缓存了改造前的技能正文；它只是调用到了磁盘上的新版 `launch-worker.ps1`，因此出现了“新版 runner + 旧版任务契约”的混合状态。

worker 进程树目前仍真实存活，OMP PID 是 `93876`，并且还启动了 Unity MCP 子进程。我继续看主 JSONL 的最近事件和工具日志，确认它是在正常探查、卡在某个工具，还是模型已经停止产出。

### 回复 7 

worker 不是完全没动，它在约 27 分钟里进行了非常长的架构推演，主上下文已经到约 `221K` tokens，直到 11:38 才第一次写代码。这正好复现了你之前担心的“大量深思考、迟迟不形成变更”。

更严重的是第一次编辑已经出现异常：`AudioBindingCatalog.cs` 被 OMP 的 edit 工具写进了字面量 `PUT >32:`，当前代码已损坏。worker 随后重新读取到了这段损坏内容，但还没有看到后续修复事件。我要再取一次实时状态和 git diff，判断它是否正在自修；在确认前不杀进程，避免打断它刚开始的修复回合。

### 回复 8 （最终回复）

结论已经足够明确：这不是单纯“慢”，而是旧编排正在重演原来的失控模式。watcher 会话的首条用户内容就是完整旧版技能，它没有动态加载新版 `SKILL.md`；因此当前 worker 会自行 review、关票，也不会写新版阶段 artifact。

worker 仍在产出，不是死锁，但状态不健康：约 33 分钟、主上下文约 `224K` tokens、31 分钟后才首次编辑；同一个文件连续两次结构化编辑失败。现在 `AudioBindingCatalog.cs` 虽然去掉了 `PUT >32:`，却留下了重复的 `AudioBinding` 声明和多余闭合括号，当前仍不可编译。继续放任很可能让它在膨胀上下文里反复修补，并最终走旧版内嵌 review。

## 报错信息

### [bash] exit=1 status=completed
`$env:PATH = "C:\Users\jinji\AppData\Local\Temp\opencode;$env:PATH"; & "C:\Users\jinji\.agents\skills\tickets-implementor\scripts\launch-worker.ps1" -ProjectRoot "C:\Users\jinji\AppData\Local\Temp\opencode\ticket-skill-smoke" -Ticket 1003 -Phase review -Platform omp -Message "must be rejected"`

```
[31;1mException: [0mC:\Users\jinji\.agents\skills\tickets-implementor\scripts\launch-worker.ps1:62[0m
[31;1m[0m[36;1mLine |[0m
[31;1m[0m[36;1m[36;1m  62 | [0m         [36;1mthrow "项目已有 ticket worker：ticket=$($owner.ticket) phase=$($ow[0m …[0m
[31;1m[0m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m         ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[0m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1m项目已有 ticket worker：ticket=1002 phase=smoke pid=101092[0m
```

### [bash] exit=1 status=completed
`Start-Sleep -Seconds 1; & "C:\Users\jinji\.agents\skills\tickets-implementor\scripts\verify-unity.ps1" -ProjectRoot "C:\Users\jinji\AppData\Local\Temp\opencode\ticket-skill-smoke" -UnityCommand "C:\Users\jinji\AppData\Local\Temp\opencode\fake-unity.ps1" -LockWaitMinutes 0 -PollSeconds 1 -RecompileTi
…[截断，共 314 字]`

```
{"status":"blocked","reason":"unity verification lock timeout","projectRoot":"C:\\Users\\jinji\\AppData\\Local\\Temp\\opencode\\ticket-skill-smoke","attempts":0,"elapsedSeconds":0.0,"newErrorCount":0,"newErrors":[]}
```

### [bash] exit=1 status=completed
`$env:FAKE_UNITY_NEVER_COMPLETE = "1"; & "C:\Users\jinji\.agents\skills\tickets-implementor\scripts\verify-unity.ps1" -ProjectRoot "C:\Users\jinji\AppData\Local\Temp\opencode\ticket-skill-smoke" -UnityCommand "C:\Users\jinji\AppData\Local\Temp\opencode\fake-unity.ps1" -PollSeconds 1 -RecompileTimeout
…[截断，共 324 字]`

```
{"status":"inconclusive","reason":"recompile status timeout; two-attempt rule reached","projectRoot":"C:\\Users\\jinji\\AppData\\Local\\Temp\\opencode\\ticket-skill-smoke","attempts":2,"elapsedSeconds":1.1,"newErrorCount":0,"newErrors":[]}
```

### [bash] exit=1 status=completed
`& "C:\Users\jinji\.agents\skills\tickets-implementor\scripts\deliver-ticket.ps1" -ProjectRoot "C:\Users\jinji\AppData\Local\Temp\opencode\ticket-skill-smoke" -Ticket 999 -Mirror`

```
[31;1mException: [0mC:\Users\jinji\.agents\skills\tickets-implementor\scripts\deliver-ticket.ps1:34[0m
[31;1m[0m[36;1mLine |[0m
[31;1m[0m[36;1m[36;1m  34 | [0m … DE -ne 0) { [36;1mthrow "实现 commit 不在当前 HEAD 历史中: $($implementation.commitS[0m …[0m
[31;1m[0m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m               ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[0m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1m实现 commit 不在当前 HEAD 历史中: fake-head[0m
```
