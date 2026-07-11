# PerfLog / PerfTrace 事件与约定

落盘：`Assets/Notes/Logs/PerfLog/perflog-{sessionId}-seed{seed}.json`  
与 CoreLog / BattleLog 共享 `sessionId` / `seed`。  
共通键：`beatId`（与 CoreLog 事件、`DiagBeatClock` 对齐）。  
`schemaVersion`：**1**。

## 设计原则

- 只记意图与突变，**不逐帧**采样。
- 主键 **uid**；排查先按 uid 过滤。
- 每次改位/改显隐带稳定 **site**（调用点短名）。
- Beat 边界 **BoardSnap**（Open 常 full，Close 常 diff）+ Close 时轻量 **Anomaly**。

## Event 字段

| 字段 | 含义 |
|------|------|
| `index` | 会话内序号 |
| `beatId` | 共通 Beat；0 表示无 |
| `tMs` | 相对会话起点毫秒 |
| `kind` | 见下表 |
| `uid` | 卡牌；Beat/BoardSnap 可为 -1 |
| `site` | 调用点 |
| `payload` | 扁平 string 字典 |

## kind

| kind | 何时 | 关键 payload |
|------|------|----------------|
| `BeatOpen` / `BeatClose` | 与流程同拍 | `beatKind`, `nodeIndex`；Close 含 `anomalyCount` |
| `Spawn` / `Despawn` | 创建/回收视图 | `defId`, `slot`, `parent` / `reason` |
| `MotionPlan` | Hop 等已知 from→to | `fromSlot`,`toSlot`,`fromX/Y`,`toX/Y`,`reason`,`expectMs` |
| `MotionBegin` | tween 启动 | `motionId`, from/to, `reason` |
| `MotionEnd` | complete / kill | `motionId`, `endHow`, `x/y`, `killedBySite` |
| `SnapSet` | 瞬间写 position | `x/y`, `slot`, `killedTween`, `reason` |
| `VisChange` | DisplayMode / active 等 | `active`, `alpha`, `mode`, `renderOn` |
| `ParentChange` | SetParent | `parent`, `worldStays` |
| `BoardSnap` | Beat 边界 | `phase`, `full`=`0\|1`, `cards` 紧凑串 |
| `Anomaly` | 自动 | `code`, `detail`, `refIndex` |

### BoardSnap `cards` 格式

- full：`uid,slot=…,xy=x,y,active=0|1,mode=…,tween=0|1;…`
- diff：`+…` 新增 / `~uid slot=a→b xy=…→…` 变更 / `-uid` 消失  
坐标为厘米级量化（×100 取整）。

## beatKind

| 值 | 场景 |
|----|------|
| `OpeningDeal` | 开局发牌表现 |
| `PostKillDrain` | 击杀后补牌/hop Drain |
| `SyncBoard` | 独立 Sync（非 Drain 内） |
| `CombatHit` | 一次进攻或反击表现段 |
| `StartNode` / `Reward` / `Room` | 预留 |

## Anomaly code

| code | 含义 |
|------|------|
| `SnapDuringMotion` | 仍有未 End 的 motion 时 SnapSet |
| `MotionPlanWithoutBegin` | 有 Plan 无 Begin |
| `VisOffWhileCombatant` | CombatHit Beat 内战斗员 active=0 |
| `SlotWorldMismatch` | 登记 slot 锚点与世界坐标偏离 |
| `OrphanAtWrongAnchor` | 空槽锚点上贴了别的 uid |
| `MissingMotionEnd` | BeatClose 时 motion 未收尾 |

## 常用 site

| site | 来源 |
|------|------|
| `Ground.Place.Snap` / `Ground.Relocate.Snap` | GroundField 硬贴锚点 |
| `DeckTween.Move` / `DeckTween.Hop` / `DeckTween.Kill` | CardDeckTween |
| `FinalStateGuard.SoftSnap` / `HardSnap` | 战斗终态守卫 |
| `Card.DisplayMode` / `Card.Spawn` / `Card.Release` | CardManager |
| `Ground.HopPlan` | hop MotionPlan |
| `DiagBeat` / `BoardSnap.Capture` / `Anomaly.Detect` | 记录器自身 |

## 与 CoreLog / Battle 互指

- 同 `sessionId` + `seed` 打开三文件。
- 同 `beatId`：CoreLog 看意图与逻辑占格；PerfLog 看画面。
- Battle：`refBattleOpIndex` 仍指向 BattleLog Op；节奏用 `beatId`（CombatHit Beat 与战斗表现段对齐）。

## 插桩入口

- Cards：`CardPresentationProbe` → `PerfTraceSink`
- Flow：`PerfTraceRecorder.OpenBeat` / `CloseBeat` / `RecordBoardSnap`
- 导出：`BattleTraceRecorder.ExportBothNow` / Play 退出三件套
