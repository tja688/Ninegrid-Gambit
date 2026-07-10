# EditMode 复测（战斗日志）

抓到 Core 疑点后再用；日志分析本身不必先跑测。

## 现有测试入口

| 类 | 路径 | 用途 |
|----|------|------|
| `CombatHitDeathRegressionTests` | `NineGrid.Core.Tests/` | 分段 `ApplyCombatHit` 反击致死样板 + `Attack` 对照骨架 |
| `CoreOperationContractTests` | 同上 | R1 Fill→Rotate、ClickEmpty E1、击杀旋转 |
| `CoreCommandShellTests` | 同上 | Command 壳层 |

Harness 惯例：

```csharp
NineGridArchitecture.ResetForTests();
InitialGameFactory.Create(arch, new InitialGameOptions { Seed = /* 日志 seed */ });
mPhase.StartNode(...);
// ApplyCombatHit / Attack / SliceEvents 断言
```

## 从日志落成回归（最短路径）

1. 读末条可疑 Op：`seed`、双方 `defId`/before hp·atk·armor、`DamageDealt.amount`、`reason`
2. 在 `CombatHitDeathRegressionTests`（或新测）里：同 seed → `StartNode` 造怪 → 必要时 `SetBase` 对齐 before 数值
3. 复现序列：
   - 分段路径：`ApplyCombatHit(avatar,monster)` → `ApplyCombatHit(monster,avatar)`
   - 对照路径：`Attack(slot)`（与分段差异只记录/弱断言，见样板注释）
4. 断言：`HpChanged`/`DamageDealt` remainingHp、`GamePhase.Defeat`、或期望 Amount
5. Unity MCP：`run_tests` → `EditMode` → 指定测试全名

## 结果怎么用

| EditMode | 含义 | 下一步 |
|----------|------|--------|
| 红，且违背 Docs | Core 回归钉住 | 用户要求改再动 Core；否则只汇报 |
| 绿，与 Docs 一致 | Core OK | 查表现：`PendingTraceReason`、命中回调是否双次结算、PostKill |
| 绿，但与「玩家体感」不符 | 多半 Docs/表现预期差 | 分析模式写清差距，勿擅自改公式 |

## 不要做

- 不为「看一眼日志」先跑全量测试
- 不把表现层 MonoBehaviour 拉进 EditMode Core 测
- 不改 `DealDamage`/反击语义来让某一局 Trace「好看」
