# Presentation Tests Map

程序集：`NineGrid.Presentation.Tests`  
路径：`Assets/Scripts/NineGrid.Presentation/Tests/`

## 分层

| 目录 | 保护什么 |
|------|----------|
| `BehaviorBaseline/` | Busy / BatchAck / EventLogCommit / 场地双向索引等行为基线 |
| `Explore/` `Attack/` `Pickup/` `UseItem/` `Fusion/` `Drain/` `Shuffle/` `Zone/` | 垂直切片（意图 → Command → Director） |
| `Lifecycle/` | 卡实体生命周期 / Handoff |
| `FieldGeometry/` | Geometry / FieldBattle System（含无 Instance 护栏） |
| `FlowShell/` | 流程壳 Controller |
| `BattleSession/` | 局内会话 |
| `Output/` | 描述 / 伤害等输出 |
| `Cards/` | 卡面 Commit、牌库闸、飞行排序、致死表现回归等 |
| `Flow/` | Flow 侧遗留/切片 |
| `Fixtures/` | EditMode 夹具 |
| `HostContractStructuralTests.cs` | 结构护栏：禁四大旧宿主名、禁 `CombatHitSink`、禁回流 `new PresentationDirector`、System 不暴露具体 View |
| `IntentIntakeStructuralTests.cs` | 结构护栏（#52）：输入路径须经 IntentIntake；门禁/收口决策禁用壁钟；ADR-0004 accepted |

## 关闭门槛（普通实施票）

1. 受影响 EditMode 绿  
2. `unity command recompile` 后 Console 无新增 Error / Exception / Assert  

全量真实场景 PlayMode 终验是 Spec 级门禁（历史上由 #42 承接），不属于每张实施票的默认门槛。

## 跑测（Unity CLI）

```bash
unity command recompile --project-path "<repo>"
unity command run_tests --mode editor --filter <NameOrNamespace> --project-path "<repo>" --format json
```
