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
| `Output/` | 描述 / 伤害等输出 |
| `Cards/` | 卡面 Commit、牌库闸、飞行排序、致死表现回归等 |
| `Fixtures/` | EditMode 夹具 |
| `HostContractStructuralTests.cs` | 结构护栏：禁 `*Hook.cs`、禁 `*ManagerSingleton`、禁回流巨型宿主名 |

## 关闭门槛（普通票）

1. 受影响 EditMode 绿
2. `unity command recompile` 后 Console 无新增 Error

PlayMode 全链终验不属于单张实施票门槛（见 Issue #28 US18）。
