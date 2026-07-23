---
status: proposed
---

# 输入交互走"单一意图收口 + 两轴门禁"

## 决策

彻底重构表演编排的输入处理与门禁：所有玩家点击只经**唯一意图收口（IntentIntake）**进入编排，门禁按**两个正交轴**裁决——**时序互斥**（`MainlineBusy`，唯一互斥真相；凡须阻塞输入的表演都持有主线）与**输入所有权**（当前哪个表面拥有输入：受保护场地 / 场地覆层 / 棋盘选择模式）。四个"棋盘动作"（Explore/Attack/Pickup/UseItem）统一走**最新覆盖缓冲**：忙时至多缓冲一条、后者覆盖前者，主线转 idle 时对 Core **重校合法性**再放行、非法即丢；模式切换（BoardSelect begin）与模态选择（房间/奖励）忙时一律 `Reject` 不缓冲。互斥/缓冲权威留在 `PresentationDirector`（时间线唯一所有者），`IntentIntake` 为薄前置、`PresentationInputStateSystem` 降为"输入所有权轴"的只读提供者。忙时额外点击发一个**冲动轻点脉冲**给（当前 no-op 的）加速通道，为未来"多点即加速"预留缝。`OccupancyDesync` 硬拒门禁降级为"永不应触发"的诊断断言。门禁/收口决策只依赖"事件顺序 + 注入 deltaTime"，禁用壁钟（`Time.realtimeSinceStartup` / `DateTime.Now`）。

## 为什么

现状是**两层会打架的门禁**：advisory `PresentationInputStateSystem.Evaluate*`（返回 Allow/Buffer/Reject/Route）与 authoritative `PresentationDirector.TrySubmitIntent`（真正的 1 深缓冲互斥）。各输入路径处置不一致——Attack 查门禁且忙时 `Reject`（被丢），Explore 完全不查门禁直发 Command 走缓冲，同构点击在负载下行为相反；`EvaluateExplore` 的 Buffer 分支在该路径上是死代码。忙时缓冲"最早保留"且 **flush 不重校合法性**，被缓冲意图在棋盘已变后仍按旧目标建脚本，是"快速连点导致莫名其妙 bug/非预期表现"的主因。busy 真相分成 `MainlineBusy`/`BattleBusy`/`FieldBusy`/`ExternalHold` 四份并行、各动作各读子集，desync 即怪态。单一收口 + 单一互斥真相 + 两轴显式建模 + flush 重校，从结构上消除这一整类 bug；两轴也给"活性遮盖"（覆层在、场地照推进但不收被占输入）一个干净语义位。

## 考虑过的替代

- **仅补齐缺失门禁、保留分层（keep_split）**：给 Explore 补上 gate 调用即可。被否——两层真相仍在、flush 重校缺口仍在，治标。
- **完整 FIFO 队列（queue_n）**：忙时按序排多条。被否——最易连出非预期"连动"，与"消除怪态"目标相悖。
- **严格丢弃（strict_drop）**：忙时一律忽略点击。稳健但手感偏滞，且无法承接未来"多点即加速 + 无缝续接"。被否为默认，仅作回退。
- **垂直切片渐进落地**：对齐 ADR-0001 的偏好。本轮改为 **big-bang 一次切换**——本次是表现层输入/门禁的较收敛替换（不触 Core 规则），且已决定先写全面 EditMode 护栏 + 结构护栏再切，回归风险可控。

## 后果

- big-bang 切换回归面较大：以 **先写护栏后切** 兜底——EditMode 覆盖 intake 状态机全处置、latest-wins 覆盖、flush 重校丢非法、两轴所有权、impatience-tap 计数、同帧突发序列；再加结构护栏"任何输入路径不得绕过 IntentIntake"。
- `BattleBusy`/`FieldBusy` 不再作独立门禁输入：须阻塞输入的交战/场地表演改为持有主线（Step 或 ExternalHold 租约），否则会漏门禁。
- `OccupancyDesyncLatched` 退出运行时门禁，降为断言；真出 desync 视为 bug 修根因。
- 玩家可见反馈（缓冲/非法/非当前所有者的差异化提示）**不在本轮**，保持现状。
- 禁用壁钟为硬约束：未来 debounce/加速需注入时钟 `Func<float>`，以保门禁边界测试可确定、不 flaky。
