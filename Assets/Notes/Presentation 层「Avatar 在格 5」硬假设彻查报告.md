# Presentation 层「Avatar 在格 5」硬假设彻查报告

Repo: `C:\Users\jinji\Documents\GitHub\Ninegrid Gambit`（只读调查，未做任何写操作）

---

## 1. `AvatarReservedSlot` 全仓分布与语义

**结论：单一常量定义点，无副本；但同一常量被当作 5 种不同语义使用。**

唯一定义点：

```11:15:Assets/Scripts/NineGrid.Presentation/Cards/GroundSlotTopology.cs
        public const int MinSlot = 1;
        public const int MaxSlot = 9;
        public const int AvatarReservedSlot = 5;

        public static readonly IReadOnlyList<int> ClockwiseRing = new[] { 1, 2, 3, 6, 9, 8, 7, 4 };
```

同文件的谓词包装：

```52:55:Assets/Scripts/NineGrid.Presentation/Cards/GroundSlotTopology.cs
        public static bool IsAvatarReserved(int slot)
        {
            return slot == AvatarReservedSlot;
        }
```

唯一的「转发式别名」（不是独立副本，但制造了第二个名字）：

```77:97:Assets/Scripts/NineGrid.Presentation/Cards/CardSlotAnchorUtility.cs
        /// <summary>
        /// 禁止发牌的目标 Ground 槽位（1-based 编号，如 slot5 = 5）。
        /// </summary>
        public const int ForbiddenGroundDealSlotNumber = GroundSlotTopology.AvatarReservedSlot;
        ...
        public static bool IsPlaceableGroundSlot(int slotNumber)
        {
            return GroundSlotTopology.IsValidSlot(slotNumber)
                   && !GroundSlotTopology.IsAvatarReserved(slotNumber);
        }
```

另有 4 处**并行的字面量 5**，不引用该常量（换位后会各自漂移）：

| 文件:行号 | 字面量 | 语义 |
|---|---|---|
| `Flow/ShopBoard/ShopBoardSlotResolver.cs:12` | `public const int AvatarSlot = 5;` | 商店盘 Avatar 站位保留 |
| `Flow/TavernBoard/TavernBoardSlotResolver.cs:16` | `public const int AvatarSlot = 5;` | 酒馆盘同上 |
| `Flow/RewardBoard/RewardBoardSlotResolver.cs:12` | `public const int AvatarSlot = 5;` | 奖励盘同上 |
| `Flow/AttributeBoard/AttributeBoardSlotResolver.cs:11` | `public const int AvatarSlot = 5;` | 属性盘同上 |
| `Flow/InRoomBoard/InRoomOfferSlotPlanner.cs:13` | `DefaultAvatarSlot = 5` | 落格规划默认避让位 |
| `Flow/RoomIcons/RoomIconHoverPreviewPresenter.cs:27` | `DefaultAvatarSlot = 5` | 悬停预览默认避让位 |
| `Flow/Tutorial/TutorialBattleDirector.cs:653` | `return slot.IsBoardSlot ? slot.Index : 5;` | 兜底回落 |
| `Flow/RoomIcons/RoomIconBoardPresenter.cs:163` | `SlotId.Board(5)` | 进房硬切复位目标 |

**逐处语义分类**（共 30 处引用 `AvatarReservedSlot`）：

**语义 A —「Avatar 在哪」（查玩家位置，最危险）** — 15 处：

- `Cards/Battle/FieldBattlePresentationExecutor.cs:155, 263, 632, 920, 975, 989`
- `Cards/CardAttackBasicAdapter.cs:516, 744, 760, 779`
- `Cards/Battle/FieldBattlePresentationExecutor.cs:662, 667`（开局落位断言）
- `Cards/CardHandManagerSingleton.cs:1591`（BoardSelect 停泊锚点）
- `Cards/Ground/GroundMotionExecutor.cs:544`（Avatar 揭示落格）
- `Flow/Presentation/BattleVfxCues.cs:221`（胜负大字定位到「棋盘中心」）

**语义 B —「哪格不发牌 / 不可放置」** — 4 处：

- `Cards/CardSlotAnchorUtility.cs:80, 96`
- `Cards/Ground/GroundOccupancyIndex.cs:72`
- `Cards/Ground/GroundMotionExecutor.cs:423`

**语义 C —「朝向 / 冲撞方向基准」** — 8 处：

- `Cards/CardAttackBasicAdapter.cs:67, 74, 88, 95, 241, 323, 524, 538`
- `Cards/Battle/FieldBattlePresentationExecutor.cs:670, 907`
- `Cards/GroundFieldView.cs:516`、`Cards/Ground/GroundMotionExecutor.cs:1053, 1064`

**语义 D —「诊断/统计时跳过该格」** — 4 处：

- `Flow/BattleSession/BattleSessionExecutor.Opening.cs:396`
- `Flow/Diagnostics/FieldTraceHelper.cs:703, 1072`
- `Cards/Ground/GroundMotionExecutor.cs:2220`

**语义 E —「哪格不参与旋转」** — **0 处**。外圈旋转从不显式排除格 5，它只是不在 `ClockwiseRing` 数组里（见第 4 节）。

---

## 2. 交战表现：`FieldBattlePresentationExecutor` / `CardAttackBasicAdapter`

**结论：硬编码耦合。玩家位置、冲撞方向、攻击者锚点全部写死格 5，三条路径互相独立地假设同一件事。**

### 2.1 取玩家位置：全部 `TryGetCardAt(5)`，从不查 `board.AvatarSlot`

```155:160:Assets/Scripts/NineGrid.Presentation/Cards/Battle/FieldBattlePresentationExecutor.cs
            if (!geometry.TryGetCardAt(GroundSlotTopology.AvatarReservedSlot, out var avatar)
                || avatar == null)
            {
                Debug.LogWarning("[FieldBattle] 导演命中 Present：Avatar 不可用。");
                return;
            }
```

同一模式在 `:263`（反击）、`:920`（反击参与者校验）、`:989`（Avatar 死亡表演）重复。`ResolveAvatarUid` 与 `ResolveAvatarDefId` 也是：

```629:636:Assets/Scripts/NineGrid.Presentation/Cards/Battle/FieldBattlePresentationExecutor.cs
        private static int ResolveAvatarUid(IGroundFieldGeometrySystem geometry)
        {
            return geometry != null
                && geometry.TryGetCardAt(GroundSlotTopology.AvatarReservedSlot, out var avatar)
                && avatar != null
                    ? avatar.Uid
                    : 0;
        }
```

```971:983:Assets/Scripts/NineGrid.Presentation/Cards/Battle/FieldBattlePresentationExecutor.cs
        private string ResolveAvatarDefId()
        {
            var geometry = ResolveGeometry();
            if (geometry != null
                && geometry.TryGetCardAt(GroundSlotTopology.AvatarReservedSlot, out var avatar)
                && avatar != null
                && !string.IsNullOrWhiteSpace(avatar.DefId))
            {
                return avatar.DefId;
            }

            return BattleParticipantIds.Wildcard;
        }
```

后果直接：Avatar 在格 2 而怪物在格 5 时，`ResolveAvatarDefId` 会返回**那只怪物的 DefId**，交战 Profile 路由（`BattlePresentationRouter.ResolveBindParams`）随之全错。

Adapter 侧同样：

```732:749:Assets/Scripts/NineGrid.Presentation/Cards/CardAttackBasicAdapter.cs
        /// <summary>
        /// 优先取场地格5入场的 Avatar 卡；否则用 Inspector defaultAttacker / AttackerProxy 兜底。
        /// </summary>
        private bool TryResolveAttacker(
            GroundFieldView field,
            out ManagedCard attackerCard,
            out Transform attacker)
        {
            attackerCard = null;
            attacker = null;

            if (field != null
                && field.TryGetCardAt(GroundSlotTopology.AvatarReservedSlot, out attackerCard)
                && attackerCard?.Transform != null)
            {
                attacker = attackerCard.Transform;
                return true;
            }
```

注意这里**没有任何 Kind 校验**——格 5 上无论是谁都被当作「攻击者 Avatar」返回。

### 2.2 冲撞方向：以格 5 为几何基准算 rig 方向

```63:76:Assets/Scripts/NineGrid.Presentation/Cards/CardAttackBasicAdapter.cs
        public bool TryResolveDirectionForVictimSlot(int victimSlot, out CardBoardDirection direction)
        {
            direction = CardBoardDirection.None;
            if (!GroundSlotTopology.IsValidSlot(victimSlot)
                || !GroundSlotTopology.AreOrthogonal(victimSlot, GroundSlotTopology.AvatarReservedSlot))
            {
                return false;
            }

            direction = CardBoardDirectionUtility.ComputeSelfDirection(
                victimSlot,
                GroundSlotTopology.AvatarReservedSlot);
            return direction != CardBoardDirection.None && _rigByDirection.ContainsKey(direction);
        }
```

这是**双重耦合**：既用格 5 判「是否可交战（正交邻接）」，又用格 5 算 rig 方向。Avatar 在格 1 时，攻击格 2 会被判为「非正交于格 5」而直接 `return false`，交战表现整体不播。

反击方向同理：

```82:95:Assets/Scripts/NineGrid.Presentation/Cards/CardAttackBasicAdapter.cs
        public bool TryResolveCounterAttackDirectionForAttackerSlot(int attackerSlot, out CardBoardDirection direction)
        {
            direction = CardBoardDirection.None;
            if (!GroundSlotTopology.IsValidSlot(attackerSlot)
                || !GroundSlotTopology.AreAdjacentEight(
                    attackerSlot,
                    GroundSlotTopology.AvatarReservedSlot))
            {
                return false;
            }

            var avatarToMonster = CardBoardDirectionUtility.ComputeSelfDirection(
                attackerSlot,
                GroundSlotTopology.AvatarReservedSlot);
```

`ComputeSelfDirection` 本身是纯 row/col 差，完全可复用任意两格：

```13:29:Assets/Scripts/NineGrid.Presentation/Cards/Effects/CardBoardDirectionUtility.cs
        public static CardBoardDirection ComputeSelfDirection(int observerSlot, int focusSlot)
        {
            ...
            var rowDelta = GroundSlotTopology.GetRow(observerSlot) - GroundSlotTopology.GetRow(focusSlot);
            var colDelta = GroundSlotTopology.GetColumn(observerSlot) - GroundSlotTopology.GetColumn(focusSlot);

            return FromRowColDelta(rowDelta, colDelta);
        }
```

即：**几何工具已解耦，是调用点把参数写死成 5**。

### 2.3 位移基准：用固定锚点，不查当前格

`PrepareAttackerAtAvatarAnchor` 强制把攻击者 Transform 传送到**格 5 锚点**：

```771:800:Assets/Scripts/NineGrid.Presentation/Cards/CardAttackBasicAdapter.cs
        private static void PrepareAttackerAtAvatarAnchor(
            GroundFieldView field,
            Transform attacker,
            Transform victim)
        {
            PrepareAttackerAtSlotAnchor(
                field,
                attacker,
                GroundSlotTopology.AvatarReservedSlot,
                victim);
        }

        private static void PrepareAttackerAtSlotAnchor(
            GroundFieldView field,
            Transform attacker,
            int attackerSlot,
            Transform victim)
        {
            ...
            var anchor = field.GetGroundAnchor(attackerSlot);
            ...
            attacker.position = anchor.position;
```

`PrepareAttackerAtSlotAnchor` **已参数化**（怪物反击 `:540`、效果打击 `:404` 都传真实格），只有玩家进攻路径（`:244`、`:326`）绕道 `PrepareAttackerAtAvatarAnchor` 硬传 5。这是最容易改也最容易漏的一处：Avatar 在格 2 时它会被**瞬移到格 5** 再冲刺。

终态守卫的还原目标同样写死：

```238:242:Assets/Scripts/NineGrid.Presentation/Cards/CardAttackBasicAdapter.cs
            var attackerSnapshot = BattleFinalStateGuard.Capture(
                attackerCard,
                field,
                GroundSlotTopology.AvatarReservedSlot);
```

`BattleFinalStateGuard.Capture` 的 `preferredSlot` 会**覆盖**真实查格：

```51:66:Assets/Scripts/NineGrid.Presentation/Cards/Battle/BattleFinalStateGuard.cs
        public static ParticipantSnapshot Capture(
            ManagedCard card,
            GroundFieldView field,
            int? preferredSlot = null)
        {
            ...
            int? slot = preferredSlot;
            if (!slot.HasValue && field != null && field.TryGetSlotOf(card.Uid, out var found))
            {
                slot = found;
            }
```

所以交战结束后 `RestorePairAsync(restoreAttackerToSlot: true)` 会把 Avatar **拉回格 5**——换位后这会静默撤销位移表现。同样出现在 `:323`（嘲讽重定向）、`:538`（反击受击者）。

### 2.4 已解耦的部分

Rig 内部几何是纯相对的，只要 `bind.UseRelativeAttackerMotion` 开启就按真实世界坐标重算，不依赖格号：

```806:817:Assets/Scripts/NineGrid.Presentation/Cards/CardAttackBasicDirectionRig.cs
            var motionTarget = ResolveEffectMotionTarget(attacker);
            var homeWorld = motionTarget.position;
            var towardVictim = victim.position - homeWorld;
            var flatToward = new Vector3(towardVictim.x, towardVictim.y, 0f);
```

但玩家主动进攻默认**不开**相对模式（只有 counter 才默认开）：

```175:182:Assets/Scripts/NineGrid.Presentation/Cards/Battle/BattleEncounterProfileSO.cs
        public void ApplyIntentDefaults(BattleIntent targetIntent)
        {
            intent = targetIntent;
            var counter = BattleIntentUtility.IsCounter(targetIntent);
            var lethal = BattleIntentUtility.IsLethal(targetIntent);
            useRelativeAttackerMotion = counter;
            useRelativeVictimKnockback = counter;
```

即玩家进攻走 Absolute 分支 `RebindAnimations(attackerAnimations, ...)`（`:244`），播的是**烘焙轴向**，只在「攻击者恰在格 5、受击者恰在正交邻格」时看起来正确。

---

## 3. Avatar 表现实体的格位绑定

**结论：已解耦（Transform 不挂格 5 锚点，按当前格重定位），且已有可复用的 hop 表现。**

### 3.1 Transform 不 SetParent 到锚点，只写 position

Avatar 揭示时只 `SnapHome` 到锚点位置，不做父子挂接：

```544:566:Assets/Scripts/NineGrid.Presentation/Cards/Ground/GroundMotionExecutor.cs
            var slot = GroundSlotTopology.AvatarReservedSlot;
            if (!IsEmpty(slot))
            {
                Debug.LogWarning($"[GroundMotionExecutor] Avatar 格位已被占用: slot={slot}");
                return;
            }

            if (!TryGetAnchor(slot, out var anchor) || anchor == null)
            {
                Debug.LogWarning($"[GroundMotionExecutor] Avatar 锚点缺失: slot={slot}");
                return;
            }

            if (!_index.TryRegister(slot, avatar.Uid))
            ...
            SlotFrameConvergence.SnapHome(avatar, anchor.position, "Ground.AvatarReveal", avatar.Uid);
```

方法签名注释已明说不走 `IsPlaceable`：

```513:519:Assets/Scripts/NineGrid.Presentation/Cards/Ground/GroundMotionExecutor.cs
        /// <summary>
        /// Avatar 专用入场：登记到格5并播缩放出现。不走 <see cref="IsPlaceable"/>，不锁 busy，便于与开局外圈发牌并行。
        /// </summary>
        public UniTask RequestRevealAvatarAsync(ManagedCard avatar, CancellationToken cancellationToken = default)
```

### 3.2 朝向控制器已按当前格查询（ADR-0019 的成果）

`AvatarBoardFacingController` 明确优先 Core 当前占格，注释直接写「跳格后不再锁死格 5」：

```56:83:Assets/Scripts/NineGrid.Presentation/Controllers/AvatarBoardFacingController.cs
        private bool TryResolveAvatar(out ManagedCard avatar)
        {
            avatar = null;
            var geometry = this.GetSystem<IGroundFieldGeometrySystem>();
            var board = this.GetModel<BoardModel>()
                        ?? NineGridArchitecture.Interface?.GetModel<BoardModel>();

            // 优先：Core 当前 Avatar 占格（跳格后不再锁死格 5）。
            if (board != null && board.AvatarSlot.Value.IsBoardSlot)
            {
                var slot = board.AvatarSlot.Value.Index;
                if (geometry != null && geometry.IsBound
                    && geometry.TryGetCardAt(slot, out avatar)
                    && avatar != null
                    && avatar.CoreKind == CardPresentationKind.Avatar)
                {
                    return true;
                }
```

并且它**有 CoreKind 校验**（`CardPresentationKind.Avatar`），这与第 2 节交战代码的无校验形成鲜明对比。镜像基准用卡面实时世界坐标，不用盘面中线：

```48:52:Assets/Scripts/NineGrid.Presentation/Controllers/AvatarBoardFacingController.cs
            // 以卡面图标当前世界位置为基准（跳格途中也会跟着动），不用盘面固定中线。
            var facing = AvatarBoardFacingState.ResolveFromPointerX(
                pointer.x,
                avatar.Transform.position.x);
```

### 3.3 已有可复用的换格移动表现：`HopAvatarToSlotAsync`

```1745:1769:Assets/Scripts/NineGrid.Presentation/Cards/Ground/GroundMotionExecutor.cs
        /// <summary>
        /// Avatar 单格 hop：允许落点含格5；短租 FieldMotion 主线以便跳间缓冲改目标。
        /// </summary>
        public async UniTask HopAvatarToSlotAsync(
            int fromSlot,
            int toSlot,
            CancellationToken cancellationToken = default)
        {
            if (!IsValidSlot(fromSlot) || !IsValidSlot(toSlot) || fromSlot == toSlot)
            {
                return;
            }
            ...
            if (!IsEmpty(toSlot))
            {
                Debug.LogWarning($"[GroundMotionExecutor] Avatar hop 目标已占用 to={toSlot}");
                return;
            }
```

它**绕过了 `IsPlaceable`**（只查 `IsEmpty`），所以格 5 可作落点。但要求目标格为空 —— 换位（目标格有怪物）走不通这条路。

`AvatarWalkRunner` 已完全按 `board.AvatarSlot` 驱动，逐跳 `MoveAvatarCommand` + hop：

```104:108:Assets/Scripts/NineGrid.Presentation/Flow/Presentation/AvatarWalkRunner.cs
                    var from = board.AvatarSlot.Value;
                    if (!from.IsBoardSlot)
                    {
                        break;
                    }
```

```142:150:Assets/Scripts/NineGrid.Presentation/Flow/Presentation/AvatarWalkRunner.cs
                    mHopping = true;
                    try
                    {
                        var geometry = mArch.GetSystem<IGroundFieldGeometrySystem>();
                        if (geometry != null)
                        {
                            await geometry.HopAvatarToSlotAsync(from.Index, next.Index, ct);
```

另有**现成的双向换位表现** `ApplyCrossSwapMovesInternalAsync`，识别 A↔B 交叉对并批量置换 + 并行飞行：

```1143:1153:Assets/Scripts/NineGrid.Presentation/Cards/Ground/GroundMotionExecutor.cs
            if (moves.Count == 2
                && TryGetCrossSwapPair(moves, out var swapA, out var swapB))
            {
                await ApplyCrossSwapMovesInternalAsync(
                    swapA,
                    swapB,
                    cancellationToken,
                    skipBusyGuard,
                    commitment);
                return;
            }
```

```2063:2078:Assets/Scripts/NineGrid.Presentation/Cards/Ground/GroundMotionExecutor.cs
                var batch = new List<(int uid, int toSlot)>
                {
                    (moveA.Uid, moveA.ToSlot),
                    (moveB.Uid, moveB.ToSlot),
                };

                if (!_index.TryCommitPermutation(batch, "Cross.Swap"))
                {
                    Debug.LogError("[GroundMotionExecutor] 换位占格批量置换失败，跳过动画。");
                    return;
                }

                // ADR-0023：换位飞行中不认领。
                ReleaseGroundCardClaim(cardA);
                ReleaseGroundCardClaim(cardB);
```

`TryCommitPermutation` 只校验 uid 有效、`IsValidSlot`、批内无重复 —— **不查 `IsPlaceable`**，所以它在几何层面已能把怪物置换进格 5。

---

## 4. 外圈旋转表现

**结论：已解耦（uid 驱动，非格号白名单）；旋转序列硬编码环序但从不显式跳过格 5——格 5 只是不在数组里。若 Avatar 站在环上，旋转会正常带上它。**

环序是唯一的硬编码点：

```15:15:Assets/Scripts/NineGrid.Presentation/Cards/GroundSlotTopology.cs
        public static readonly IReadOnlyList<int> ClockwiseRing = new[] { 1, 2, 3, 6, 9, 8, 7, 4 };
```

旋转实现遍历环序读**占格 uid**，不看是谁：

```1486:1511:Assets/Scripts/NineGrid.Presentation/Cards/Ground/GroundMotionExecutor.cs
                var ring = GroundSlotTopology.ClockwiseRing;
                var uids = new int[ring.Count];
                var planSb = new StringBuilder(ring.Count * 16);
                for (var i = 0; i < ring.Count; i++)
                {
                    uids[i] = _index.GetUidAt(ring[i]);
                    if (uids[i] == 0)
                    {
                        continue;
                    }

                    plannedAnim++;
                    var toIndex = clockwise
                        ? (i + 1) % ring.Count
                        : (i + ring.Count - 1) % ring.Count;
```

```1523:1537:Assets/Scripts/NineGrid.Presentation/Cards/Ground/GroundMotionExecutor.cs
                var batch = new List<(int uid, int toSlot)>(plannedAnim);
                for (var i = 0; i < ring.Count; i++)
                {
                    if (uids[i] == 0)
                    {
                        continue;
                    }

                    var toIndex = clockwise
                        ? (i + 1) % ring.Count
                        : (i + ring.Count - 1) % ring.Count;
                    batch.Add((uids[i], ring[toIndex]));
                }

                if (!_index.TryCommitPermutation(batch, "Ring.Shift"))
```

Avatar 若已登记在环上某格，会被自然带走。同理 `CardSlotAnchorUtility.GetOpeningRingSlotIndices` 只是转发同一数组（`:104`）。

**但有一个隐性 bug 会在此路径上咬人**：`IsOuterRing` 对格 5 返回 `true`。

```47:50:Assets/Scripts/NineGrid.Presentation/Cards/GroundSlotTopology.cs
        public static bool IsOuterRing(int slot)
        {
            return ClockwiseRingIndex[slot] >= 0;
        }
```

`ClockwiseRingIndex` 是 `new int[10]`，默认全 0；静态构造只写入环上 8 格（`:31-34`），格 5 与索引 0 保持默认值 `0`，因此 `IsOuterRing(5) == true`，且 `GetClockwiseRingIndex(5) == 0`（与格 1 撞索引）。

这在**当前**格 5 恒为 Avatar 时无害（旋转不会产生涉及格 5 的 move）。换位后，格 5 上有怪物且发生涉及格 5 的移动时：

```1206:1229:Assets/Scripts/NineGrid.Presentation/Cards/Ground/GroundMotionExecutor.cs
                if (!GroundSlotTopology.IsOuterRing(move.FromSlot)
                    || !GroundSlotTopology.IsOuterRing(move.ToSlot))
                {
                    return false;
                }
                ...
                var fromIdx = GroundSlotTopology.GetClockwiseRingIndex(move.FromSlot);
                var toIdx = GroundSlotTopology.GetClockwiseRingIndex(move.ToSlot);
```

`TryClassifyOuterRingRotation` 会把格 5 误当环上格，且用错误索引 0 判方向。`DealFlightCoordinator` 的两处也同样受影响：

```291:301:Assets/Scripts/NineGrid.Presentation/Cards/Ground/DealFlightCoordinator.cs
                if (!GroundSlotTopology.IsOuterRing(probe.TrackedSlot))
                {
                    continue;
                }

                var prev = probe.TrackedSlot;
                probe.TrackedSlot = GroundSlotTopology.GetClockwiseRingTargetSlot(probe.TrackedSlot, clockwise);
```

`GetClockwiseRingTargetSlot(5)` 会返回 `ClockwiseRing[1] = 2`，即把格 5 的飞牌探针错误推进到格 2。

---

## 5. 命中与认领（ADR-0023）

**结论：认领框架已解耦（按 `CoreKind` 而非格号判定），但「无认领者格位」的兜底分支隐式假设那格就是 Avatar 格。**

### 5.1 认领判定按 Kind，不按格号 —— 已解耦

```70:106:Assets/Scripts/NineGrid.Presentation/Cards/GroundCardHitProxy.cs
        public void SyncClaimForSlot(int slot)
        {
            if (!isActiveAndEnabled)
            {
                ReleaseClaim();
                return;
            }

            _driver ??= GetComponent<CardVisualDriver>();
            var card = _driver?.BoundCard;
            if (card == null
                || card.CoreKind == CardPresentationKind.Avatar
                || card.CoreKind == CardPresentationKind.Unknown)
            {
                ReleaseClaim();
                return;
            }
            ...
            field.TryClaimSlot(slot, claimant);
```

`SlotClaimRegistry` 全文无任何格 5 特判，纯 owner 语义，且支持同 owner 迁格自动卸旧格：

```61:77:Assets/Scripts/NineGrid.Presentation/Cards/Ground/SlotClaimRegistry.cs
            // 同 owner 迁格：先卸旧格。
            for (var s = GroundSlotTopology.MinSlot; s <= GroundSlotTopology.MaxSlot; s++)
            {
                if (s == slot)
                {
                    continue;
                }

                var other = _bySlot[s];
                if (other != null && ReferenceEquals(other.Owner, claimant.Owner))
                {
                    _bySlot[s] = null;
                }
            }

            _bySlot[slot] = claimant;
```

**怪物换到格 5 会正常认领、正常可点击、正常出简要解释与威胁光圈**——这一层不需要改。命中框恒开也不看格号：

```150:159:Assets/Scripts/NineGrid.Presentation/Cards/Ground/GroundMotionExecutor.cs
        private void RefreshSlotHitCollider(int slot)
        {
            // ADR-0023：九框恒开；禁止用启停 collider 表达规则。
            _view?.RefreshSlotHit(slot, hitEnabled: true);
        }
```

### 5.2 「无认领者 ⇒ 走 Avatar 范围光圈」是隐式假设

```156:165:Assets/Scripts/NineGrid.Presentation/Cards/GroundFieldHitSurface.cs
            var field = GroundFieldGeometryHook.FieldOrNull();
            SlotClaimant claimant = null;
            var hasClaim = field != null && field.TryGetSlotClaimant(_resolvedSlot, out claimant);
            if (!hasClaim)
            {
                // Avatar 永不认领（ADR-0023）：无认领者格位的悬停在此接玩家攻击范围光圈。
                ClearHoverState();
                ApplyAvatarRangeGlow();
                return;
            }
```

这条分支同时服务「Avatar 格」与「真空格」两种情况，靠下游 `ShowAvatarIfSlotMatches` 二次判定收敛。**下游判定本身已按当前格解耦**（见第 7 节），所以行为是正确的：怪物换到格 5 后有认领者 → 走认领分支；Avatar 换到格 2 后格 2 无认领者 → 光圈按格 2 点亮。属于「表达方式脆弱但结果正确」。

### 5.3 无「格 5 永远无认领者」的硬编码

全仓未发现把格 5 当特殊跳过的认领代码。`GroundFieldView` 的认领门面全是纯转发（`:75-99`），无格号过滤。

---

## 6. 输入合法性 `BoardIntentLegality`

**结论：已完全解耦。14 处命中全部从 `board.AvatarSlot.Value` 出发。**

跳格起点：

```45:56:Assets/Scripts/NineGrid.Presentation/Flow/Presentation/BoardIntentLegality.cs
            var board = arch.GetModel<BoardModel>();
            var from = board.AvatarSlot.Value;
            if (!from.IsBoardSlot)
            {
                rejectReason = "avatarNotOnBoard";
                return false;
            }

            if (from.Index == groundSlot)
            {
                rejectReason = "alreadyAtDestination";
                return false;
            }
```

可交战集合：

```157:184:Assets/Scripts/NineGrid.Presentation/Flow/Presentation/BoardIntentLegality.cs
            var slot = SlotId.Board(groundSlot);
            var board = arch.GetModel<BoardModel>();
            if (!slot.IsBoardSlot || slot == board.AvatarSlot.Value)
            {
                rejectReason = "notBoardCardSlot";
                return false;
            }
            ...
            if (!arch.GetSystem<IBoardSystem>().AreAdjacent(board.AvatarSlot.Value, slot))
            {
                rejectReason = "notAdjacent avatarSlot=" + board.AvatarSlot.Value;
                return false;
            }
```

可拾取集合同构（`:406-433`）、翻牌（`:225-260`）、Explore（`:107-127`）皆同。邻接判定委托 `IBoardSystem.AreAdjacent(avatarSlot, slot)`，无格号硬编码。

**注意由此产生的一处不一致**：合法性层允许「Avatar 在格 2 攻击格 1」，但第 2.2 节的表现层 `TryResolveDirectionForVictimSlot` 会因「格 1 不正交于格 5」返回 false。换位落地后，Core 判合法、Present 静默不播，就是这个错位。

---

## 7. 范围高亮 / 预览 / 非战斗盘面

### 7.1 `BoardRangeGlowFx` — 已解耦

```471:481:Assets/Scripts/NineGrid.Presentation/Cards/Vfx/BoardRangeGlowFx.cs
        private static int ResolveAvatarSlotOrMinusOne()
        {
            var arch = NineGridArchitecture.Current;
            var board = arch?.GetModel<BoardModel>();
            if (board == null || !board.AvatarSlot.Value.IsBoardSlot)
            {
                return -1;
            }

            return board.AvatarSlot.Value.Index;
        }
```

范围以该值为原点动态展开，且每帧重算 + 失效校验：

```186:214:Assets/Scripts/NineGrid.Presentation/Cards/Vfx/BoardRangeGlowFx.cs
        public void ShowAvatarIfSlotMatches(Behaviour requester, int hoveredSlot)
        {
            var avatarSlot = ResolveAvatarSlotOrMinusOne();
            if (requester == null
                || avatarSlot < GroundSlotTopology.MinSlot
                || hoveredSlot != avatarSlot)
            {
                HideIfOwnedBy(requester);
                return;
            }
            ...
            var reachable = GroundSlotTopology.GetNeighbors(avatarSlot, GroundSlotRelation.Orthogonal);
```

```334:337:Assets/Scripts/NineGrid.Presentation/Cards/Vfx/BoardRangeGlowFx.cs
                if (_avatarMode)
                {
                    stillValid = ResolveAvatarSlotOrMinusOne() == _originSlot;
                }
```

威胁范围里的「Avatar 强调权重」也按当前格：

```159:170:Assets/Scripts/NineGrid.Presentation/Cards/Vfx/BoardRangeGlowFx.cs
            var avatarSlot = ResolveAvatarSlotOrMinusOne();
            var shown = false;
            for (var i = 0; i < threatened.Count; i++)
            {
                var slot = threatened[i];
                if (!TryPrepareCellRenderer(field, slot))
                {
                    continue;
                }

                _targetWeight[slot] = slot == avatarSlot ? AvatarEmphasisWeight : 1f;
```

### 7.2 `BattleInfoPreviewPresenter` — 与九宫格无关（误报）

该文件 8 处 `avatarSlot` 全部是**UI 立绘占位 Transform**，非盘面格位：

```73:75:Assets/Scripts/NineGrid.Presentation/Flow/BattleInfoPreview/BattleInfoPreviewPresenter.cs
        private Vector3 _avatarSlotBaseLocalPos;
        private Vector3 _avatarSlotBaseLocalScale = Vector3.one;
        private bool _avatarSlotBaseCaptured;
```

```270:279:Assets/Scripts/NineGrid.Presentation/Flow/BattleInfoPreview/BattleInfoPreviewPresenter.cs
        private void RestoreAvatarSlotBaseTransform()
        {
            if (!_avatarSlotBaseCaptured || playerBodySlot == null)
            {
                return;
            }

            playerBodySlot.localPosition = _avatarSlotBaseLocalPos;
            playerBodySlot.localScale = _avatarSlotBaseLocalScale;
        }
```

`playerBodySlot` 来自 `playerGroup.Find("玩家本体占位")`（`:778`）。**无耦合，无需改动。**

### 7.3 各非战斗盘面 Presenter — 常量兜底 + 运行时查询双轨

四个 `SlotResolver` 都硬编码 `AvatarSlot = 5`（见第 1 节表格），但 Presenter 只把它当**兜底**：

```271:280:Assets/Scripts/NineGrid.Presentation/Flow/ShopBoard/ShopBoardPresenter.cs
        private int ResolveAvatarSlot()
        {
            var board = mArch?.GetModel<BoardModel>() ?? NineGridArchitecture.Current?.GetModel<BoardModel>();
            if (board != null && board.AvatarSlot.Value.IsBoardSlot)
            {
                return board.AvatarSlot.Value.Index;
            }

            return ShopBoardSlotResolver.AvatarSlot;
        }
```

`TavernBoardPresenter.cs:311-320` 完全同构。落格规划把它作参数传入，无内部假设（`:309-318`）。Avatar 追踪循环也全按当前格轮询：

```1019:1030:Assets/Scripts/NineGrid.Presentation/Flow/ShopBoard/ShopBoardPresenter.cs
                    if (board == null || !board.AvatarSlot.Value.IsBoardSlot)
                    {
                        await DelayAsync(0.05f, ct);
                        continue;
                    }

                    var slot = board.AvatarSlot.Value.Index;
                    if (slot != mLastAvatarSlot)
                    {
                        mLastAvatarSlot = slot;
                        HandleAvatarSlot(slot, ct);
                    }
```

`RewardBoardPresenter.cs:620-631`、`AttributeBoardPresenter.cs:486-497`、`RoomIconBoardPresenter.cs:210-221` 全部同构。`RoomIconHoverPreviewPresenter` 同样双轨：

```293:300:Assets/Scripts/NineGrid.Presentation/Flow/RoomIcons/RoomIconHoverPreviewPresenter.cs
            var avatarSlot = DefaultAvatarSlot;
            if (board != null && board.AvatarSlot.Value.IsBoardSlot)
            {
                avatarSlot = board.AvatarSlot.Value.Index;
            }

            var excluded = new HashSet<int>(RoomIconOccupancy.Current.BySlot.Keys);
            excluded.Add(avatarSlot);
```

**结论：非战斗盘面已解耦（隐式假设仅存于兜底常量）。** 这些盘面本就不参与战斗换位，风险最低。

唯一真正的硬编码是进房复位（这是 ADR-0019 明文设计，非缺陷）：

```163:181:Assets/Scripts/NineGrid.Presentation/Flow/RoomIcons/RoomIconBoardPresenter.cs
            var target = SlotId.Board(5);
            if (board.AvatarSlot.Value == target)
            {
                SnapAvatarView(arch, 5);
                return;
            }

            var pipeline = arch.GetSystem<IActionPipelineSystem>();
            if (pipeline != null)
            {
                pipeline.Enqueue(new MoveAvatarAction(target));
                pipeline.RunToCompletion();
            }
            else
            {
                board.SetAvatar(avatar, target);
            }

            SnapAvatarView(arch, 5);
```

---

## 8. 测试断言

**结论：无测试断言「Avatar 必须在格 5」。全部是 setup 阶段的固定放置，非行为断言。**

`Assets/Scripts/**/Tests/` 下共 30+ 处 `SlotId.Board(5)`，全部形如：

```39:39:Assets/Scripts/NineGrid.Presentation/Tests/AvatarVitalityInvariantTests.cs
            board.SetAvatar(avatar, SlotId.Board(5));
```

```323:323:Assets/Scripts/NineGrid.Presentation/Tests/RelicMountResilienceTests.cs
            mArch.GetModel<BoardModel>().SetAvatar(avatar, SlotId.Board(5));
```

`CreateAvatarOnBoard(SlotId.Board(5))` 出现在 `TutorialPhase2FlowTests.cs:47`、`TutorialPhase4FlowTests.cs:46,75,110`、`TutorialDummyRhythmDeathRequiresTests.cs:55,98,127,162,197`、`TrapArmorTotemStackingTests.cs:71,86,100,114,134`、`BearTrapAdjacentDealRegressionTests.cs:37,59,85`、`BrutalityCardMultiplierRegressionTests.cs:42,67,91`、`FlameIntenseBurningLoopRegressionTests.cs:42,67`、`TrapFlameItemStatBonusRegressionTests.cs:42,79` 等。

唯一一处把格 5 写成**约束前提**的是测试注释：

```467:467:Assets/Scripts/NineGrid.Presentation/Tests/CardFaceReconciliationRegressionTests.cs
            // Avatar 固定 Board(5)：随机换位避开，避免撞 BoardModel 占位断言。
```

这说明该测试的随机换位逻辑**主动规避了格 5**，换位功能上线后此测试的覆盖假设需要复核。

`SlotClaimRegistryTests.cs` 存在，但未出现在 `AvatarSlot` 搜索结果中——认领测试不涉及 Avatar 格位假设。

---

## Presentation 层耦合风险清单（按严重度排序）

### P0 — 换位落地必须先改

**1. `GroundOccupancyIndex.IsPlaceable` 硬拒非 Avatar 卡进格 5**

```69:74:Assets/Scripts/NineGrid.Presentation/Cards/Ground/GroundOccupancyIndex.cs
        public bool IsPlaceable(int slot)
        {
            return IsValidSlot(slot)
                   && !GroundSlotTopology.IsAvatarReserved(slot)
                   && _uidBySlot[slot] == 0;
        }
```

这是最底层的墙。`RequestPlaceCard`（`GroundMotionExecutor.cs:335`）、`RequestRelocateOccupancy`（`:423`）、`PlaceForExplore`（`:803`）、`CardDeckManagerSingleton.IsValidGroundSlot`（`:1826`）全部经此。怪物无法通过常规路径落到格 5。唯一绕开它的是 `TryCommitPermutation`（换位/旋转批量置换）与 `HopAvatarToSlotAsync`。

**2. `CardAttackBasicAdapter.TryResolveAttacker` 无 Kind 校验地把格 5 占用者当玩家**

`CardAttackBasicAdapter.cs:743-749`。怪物换到格 5 后会被当成攻击者 Avatar，同时真正的 Avatar（在外圈）从表现视角消失。

**3. `TryResolveDirectionForVictimSlot` / `TryResolveCounterAttackDirectionForAttackerSlot` 以格 5 判邻接与算方向**

`CardAttackBasicAdapter.cs:63-76`、`:82-95`。Avatar 不在格 5 时，玩家进攻表现直接 `return false` 静默不播，与 `BoardIntentLegality` 判定的合法集合脱节。

**4. `PrepareAttackerAtAvatarAnchor` 把攻击者瞬移到格 5 锚点**

`CardAttackBasicAdapter.cs:771-781`，被 `:244`（普攻）与 `:326`（嘲讽重定向）调用。修改成本最低（`PrepareAttackerAtSlotAnchor` 已参数化），漏改代价最大（视觉直接跳格）。

**5. `BattleFinalStateGuard.Capture(..., AvatarReservedSlot)` 把 Avatar 还原目标写死格 5**

`CardAttackBasicAdapter.cs:241`、`:323`、`:538`。`preferredSlot` 会覆盖真实查格（`BattleFinalStateGuard.cs:61-65`），交战结束后 `restoreAttackerToSlot: true` 会把 Avatar 拉回格 5。

**6. `ResolveAvatarDefId` / `ResolveAvatarUid` 取格 5 占用者的身份**

`FieldBattlePresentationExecutor.cs:971-983`、`:629-636`。直接污染 `BattlePresentationRouter` 的 Profile 路由（拿到怪物 DefId 当玩家 DefId）。

**7. Avatar 可用性校验全部 `TryGetCardAt(5)`**

`FieldBattlePresentationExecutor.cs:155, 263, 920, 989`。Avatar 在外圈时全部返回「Avatar 不可用」并 `return`，命中/反击/死亡表演整批丢失。

**8. `IsOuterRing(5) == true` 与 `GetClockwiseRingIndex(5) == 0` 的默认值 bug**

`GroundSlotTopology.cs:47-50` 配合 `:21` 的 `new int[10]`。当前无害（格 5 恒为 Avatar，不产生涉格 5 的环 move），换位后会让 `TryClassifyOuterRingRotation`（`GroundMotionExecutor.cs:1206-1229`）与 `DealFlightCoordinator`（`:291-301`、`:652-670`）用错误索引处理格 5。属于「换位一开就会踩」的定时炸弹。

### P1 — 换位后表现会错但不阻塞

**9. 交战邻接资格判定散落三处，全部锚定格 5**

`FieldBattlePresentationExecutor.cs:670`（决斗惩罚）、`:907`（反击资格）、`GroundFieldView.cs:512-517` + `GroundMotionExecutor.cs:1051-1053`（`IsAvatarOrthogonalBattleSlot`）。`GroundCardHitProxy.cs:236-237` 的诊断快照也依赖它。

**10. 玩家进攻默认走 Absolute 烘焙轴向，不开相对几何**

`BattleEncounterProfileSO.cs:180-181`（`useRelativeAttackerMotion = counter`）。非中心攻击方向会播错烘焙轴，只有反击路径有 `WithForcedRelativeMotion` 兜底（`CardAttackBasicAdapter.cs:524-528`）。

**11. `HopAvatarToSlotAsync` 要求目标格为空**

`GroundMotionExecutor.cs:1765-1769`。换位（目标格有怪物）无法复用这条现成路径，需走 `ApplyCrossSwapMovesInternalAsync` 或新增路径。

**12. 开局强制断言 Avatar 占格 5 并兜底落位**

`BattleSessionExecutor.Opening.cs:661-671`。若换位状态跨越了开战边界，会强行把 Avatar 塞回格 5。ADR-0019 的「开战前须回格 5」正落在此处。

**13. `BattleVfxCues` 胜负大字定位到格 5 锚点**

`Flow/Presentation/BattleVfxCues.cs:220-221`。注释写「棋盘中心（Avatar 保留格锚点）」，语义上是「棋盘中心」而非「玩家位置」——若意图是中心则无需改，但常量选择使其与 Avatar 概念绑定。

**14. `CardHandManagerSingleton` BoardSelect 停泊锚点写死格 5**

`Cards/CardHandManagerSingleton.cs:1590-1597`。Avatar 在外圈时，选牌停泊卡会飞到中心空格（或飞到占据格 5 的怪物身上）。

### P2 — 诊断/统计失真，不影响玩法

**15. 诊断遍历时跳过格 5，会漏掉换到格 5 的怪物**

`FieldTraceHelper.cs:701-712`（`CountBoardOccupants`）、`:1070-1090`（Core/Pres 占格对账）、`GroundMotionExecutor.cs:2215-2232`（`HasLivingNonAvatarCard`）、`BattleSessionExecutor.Opening.cs:394-399`（开局计划捕获）。第 15 项中 `HasLivingNonAvatarCard` 最值得注意——它用于判断「场上是否还有活怪」，若怪物换到格 5 会被漏计，可能导致战斗结束判定异常。

**16. 四个非战斗盘 `SlotResolver` 的 `AvatarSlot = 5` 兜底常量**

`ShopBoardSlotResolver.cs:12`、`TavernBoardSlotResolver.cs:16`、`RewardBoardSlotResolver.cs:12`、`AttributeBoardSlotResolver.cs:11`、`InRoomOfferSlotPlanner.cs:13`、`RoomIconHoverPreviewPresenter.cs:27`、`TutorialBattleDirector.cs:653`。仅在 `board.AvatarSlot` 无效时生效，且这些盘面不参与战斗换位。

**17. `RoomIconBoardPresenter.HardCutAfterEnter` 硬切格 5**

`Flow/RoomIcons/RoomIconBoardPresenter.cs:163-181`。ADR-0019 明文设计，非缺陷；但若换位状态需跨房间保留，此处会无条件清除。

**18. `CardFaceReconciliationRegressionTests` 的随机换位主动规避格 5**

`Tests/CardFaceReconciliationRegressionTests.cs:467`。测试覆盖假设需随换位功能复核。

---

## 已解耦、无需改动的模块

| 模块 | 依据 |
|---|---|
| `BoardIntentLegality`（14 处） | 全部 `board.AvatarSlot.Value` + `IBoardSystem.AreAdjacent` |
| `BoardRangeGlowFx`（8 处） | `ResolveAvatarSlotOrMinusOne()` 每帧重算 + 失效校验 |
| `AvatarBoardFacingController` | 优先 Core 当前占格 + `CoreKind` 校验 + 卡面实时世界坐标 |
| `AvatarWalkRunner` / `AvatarWalkSystem` | 逐跳 `board.AvatarSlot` → `MoveAvatarCommand` → hop |
| 认领框架（`SlotClaimRegistry` / `GroundCardHitProxy` / `GroundFieldHitSurface`） | 按 `CoreKind == Avatar` 判定，无格号特判；九框恒开 |
| 外圈旋转（`RotateOuterRingInternalAsync`） | uid 驱动，环上任何占用者都会被带走 |
| `CardBoardDirectionUtility.ComputeSelfDirection` | 纯 row/col 差，任意两格通用 |
| `CardAttackBasicDirectionRig` 相对几何分支 | 按真实世界坐标重算，不看格号 |
| `ApplyCrossSwapMovesInternalAsync` / `TryCommitPermutation` | 已支持任意两格互换，不查 `IsPlaceable` |
| 非战斗盘 Presenter 的 Avatar 追踪循环 | 全部轮询 `board.AvatarSlot.Value.Index` |
| `BattleInfoPreviewPresenter`（8 处） | UI 立绘占位 Transform，与九宫格无关 |
| `RunSceneTransitionService` | `board.AvatarSlot.Value` + 按格取锚点 |
| `BoardIntentGateDiagnostics` | 只读打点 `board.AvatarSlot.Value` |
| Core 层（`BoardModel` / `BoardSystem` / `PhaseSystem`） | `AvatarReservedSlot` 在 `NineGrid.Core` 下 0 命中；占格权威是 `BoardModel.AvatarSlot` |

---

**一句话概括**：ADR-0019 那次「Avatar 跳格」改造只打通了**非战斗链路**（输入合法性、朝向、盘面追踪、hop 表现、认领框架），战斗表现链路完全没动。换位落地的工作量集中在两个文件——`CardAttackBasicAdapter.cs`（12 处）与 `FieldBattlePresentationExecutor.cs`（8 处）——再加 `GroundOccupancyIndex.IsPlaceable` 这一道底层墙和 `GroundSlotTopology.IsOuterRing` 那个默认值 bug。