---
status: accepted
---

# 卡牌表现：底盘 + 五套卡面模板 + 装配槽表

## 决策

局内卡牌实体收敛为**一套卡牌底盘**（变换塔、SortingGroup、命中代理、效果挂点；由 `老Standard Card` 就地演化并保留场景 GUID）加**五套卡面模板**（玩家 / 怪物 / 道具 / 遗物 / **机关**），按 `CardPresentationKind` 显式表在 Spawn 时挂入 L4。Kind 映射为：Avatar→玩家；Monster→怪物；HelpCard/Item/PlayerCard→道具；Relic→遗物；**Trap→机关**（[ADR-0017](0017-trap-card-kind-and-dual-bucket.md)）。玩家技能不进卡面/Spawn。

表现数据走**胖投影**：卡面纯消费；数值与牌面朝向的可见提交（Commit）绑定核心战斗表演编排的阻塞串行事件流，禁止队列外直刷卡面。卡级 sorting 复用原底盘黑盒；卡面仅组内相对 order，通层重复 order 编辑器告警。

视觉装配为**同一系统、一张槽表**（ScriptableObject 注册表）：槽以代码代号为键、编辑器悬停中文注释；直接暴露主图标/卡面背景/卡背相关槽，其余收纳；描述用 `[SlotCode]` 引用图标语义槽。图标缺省回退源模板，数值缺省 0。无 hover 描述权威；卡面基础描述静态，详细描述（右键面板）另开后续。

## 修订（2026-08-01）

原决策写「四套卡面」。机关卡落地后正式为**五套**（+`TrapFacePrefab` / `机关卡标准模版.prefab`），枚举与选壳见 ADR-0017；本 ADR 正文已同步。

## 为什么

「一张 Standard Card 顶所有」无法承载多类对象差异化的卡面维度（行动计数、描述、卡背、世界 TMP 等），而运动/排序/命中仍是跨 Kind 公共黑盒——拆成多套完整运行时 Prefab 会倍增复制底盘并与现有塔/飞行抬序冲突。胖投影 + 编排 Commit 针对长期「数值抢先跳变」：可见值提交点收口到串行表演队列，而非 `ApplyToManagedCard`/`Set*` 旁路。装配槽表把 Catalog 的 icon/face 硬字段升级为可扩展语义槽，并让描述插入与主图标/卡背同一真相源。

## 考虑过的替代

- **多套完整运行时 Prefab**：否决——与「不跟现有底盘冲突」及塔/SG/HitProxy 复用目标相反。
- **DisplayMode 推导牌面朝向**：否决——朝向将参与攻击/效果门闩，权威必须在 Core，表现只 Commit 镜像。
- **保留上帝 `StandardCardView` Setter 当正式通路**：否决——无法严卡数值 timing，与编排事件流绑定冲突。
- **全局共用卡背源**：否决——每卡可自定卡背，模板仅作兜底。

## 后果

- `ManagedCard`/宿主仍可暂名 `StandardCardView`，职责挖成底盘宿主 + 分发投影给 Kind Binder。
- `RegisterPrefab(defId)` 降为特例；主路径为底盘 + Kind→卡面表。
- 首波回填既有主图标/名字/数值即可玩；描述、行动计数等槽口预留走默认，详细描述与翻牌规则门闩后续专题。
- 编辑器 Cards 管线必须按「底盘 + L4 卡面」预览终态，并修复已失效的 `Standard Card.prefab` 路径。

## 相关

- [ADR-0005](0005-card-face-beat-commit.md) — 真正落地「投影提交只由阻塞串行队列 / 事件流触发」：删除直读 Core 与公开底盘数值 Setter 旁路
- [ADR-0017](0017-trap-card-kind-and-dual-bucket.md) — 机关 Kind 与第五套卡面
