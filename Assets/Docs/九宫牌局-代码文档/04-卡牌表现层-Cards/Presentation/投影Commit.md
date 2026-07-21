# Presentation / 投影 Commit

> 覆盖 `Assets/Scripts/Cards/Presentation/` 与宿主 Commit 出口。  
> 对齐 ADR-0002 / #15：胖投影 → 编排 Commit → Kind Binder；禁止旁路 Set* 直刷卡面。

---

## 1. 三层状态

| 层 | 权威 | 说明 |
|----|------|------|
| 1 Core | `CardInstance` / Stat | 逻辑真相 |
| 2 已提交投影 | `ManagedCard.CommittedPresentation` | 可见值真相；对账对齐此层 |
| 3 卡面消费 | `CardFacePresentationBinder` | 只读已提交投影，按 Kind 路由 |

禁止 1 直写 3。

---

## 2. 类型

| 类型 | 作用 |
|------|------|
| `CardPresentationSnapshot` | 胖投影：Kind / 名 / 主图标与卡背 / 攻甲血 / ActionCount / FaceUp / 描述占位 |
| `ICardFaceBinder` | 卡面消费接口：`ApplyPresentation` + `ApplyFaceOrientation` |
| `CardFacePresentationBinder` | L4 Kind Binder；挂面时由 `CardManager` 装配 |

### FaceUp（朝向预留）

- 语义：Core 牌面朝向镜像，**非** DisplayMode 推导。
- 本波：Binder 恒正面（front 显 / back 隐），入口不抛错；规则门闩 / 揭牌演出属后续专题。

### Kind 首波消费

| Kind | 消费 |
|------|------|
| Avatar | 主图标、名字、攻击、护甲；**不绑血量** |
| Monster | 主图标、名字、攻 / 甲 / 血；行动计数缺省 0 |
| HelpCard / Item / PlayerCard / Relic | 主图标、名字为主 |

未接线图标回退模板默认；数值缺省 0。

---

## 3. 宿主出口

`StandardCardView`（卡牌底盘宿主）：

- `AttachFaceBinder` — Spawn 挂面后装配
- `ApplyPresentation(snapshot)` — 正式分发出口
- `ApplyFaceOrientation(bool)` — 与数值同一出口家族
- 旧 `Set*` — 仅无 Binder 的 RegisterPrefab 特例过渡 / 测试兼容

`ManagedCard`：

- `CommitPresentation(snapshot)` — 写入已提交 + 宿主 Apply
- `ReapplyCommittedPresentation()` — 对账重放，不读 Core

---

## 4. Flow 桥（`CoreCardPresentationMapper`）

| API | 行为 |
|-----|------|
| `ApplyToManagedCard` | 读 Core/Content → 构建投影 → `CommitPresentation` |
| `CommitAllSpawnedCards` | 编排主线全场 Commit |
| `SyncAllSpawnedCards` | 全场对账已提交投影（不直刷最新 Core） |
| `ApplyVisualsByDefId` | 无 Core uid 展示卡：按 DefId 构建投影并 Commit |

---

## 5. 测试

| 测试 | Seam |
|------|------|
| `CardPresentationCommitTests` | 构建投影 → Commit → 观察图标/名字/数值；未接线默认；FaceUp 不抛错；对账重放 |
