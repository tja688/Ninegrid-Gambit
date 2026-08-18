using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Flow.BoardBriefTip;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using NineGrid.Flow.Tutorial;
using NineGrid.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow
{
    internal sealed partial class BattleSessionExecutor
    {
        private static void LogDeckOptionsProbe(NodeDeckOptions options)
        {
            var playerKinds = new System.Text.StringBuilder();
            for (var i = 0; i < options.PlayerCards.Count; i++)
            {
                if (i > 0) playerKinds.Append(",");
                playerKinds.Append(options.PlayerCards[i].DefId);
                playerKinds.Append(":");
                playerKinds.Append(options.PlayerCards[i].Kind);
            }

            var enemyKinds = new System.Text.StringBuilder();
            for (var i = 0; i < options.EnemyCards.Count; i++)
            {
                if (i > 0) enemyKinds.Append(",");
                enemyKinds.Append(options.EnemyCards[i].DefId);
                enemyKinds.Append(":");
                enemyKinds.Append(options.EnemyCards[i].Kind);
            }

            Debug.Log(
                $"[DeckProbe] OptionsBeforeStartNode "
                + $"playerOpening={options.PlayerOpeningCount} enemyOpening={options.EnemyOpeningCount} "
                + $"playerCards=[{playerKinds}] (n={options.PlayerCards.Count}) "
                + $"enemyCards=[{enemyKinds}] (n={options.EnemyCards.Count})");
        }

        /// <summary>
        /// [DeckProbe] 记录 StartNode 后板面卡牌组成。
        /// </summary>
        private static void LogBoardDeckProbe(IArchitecture arch)
        {
            var registry = arch.GetModel<CardRegistry>();
            var board = arch.GetModel<BoardModel>();
            var deck = arch.GetModel<DeckModel>();

            var boardSlots = new System.Text.StringBuilder();
            var kindTally = new System.Collections.Generic.Dictionary<CardKind, int>();
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                var uid = board.GetCardUid(slot);
                if (uid <= 0 || !registry.TryGet(uid, out var card))
                {
                    if (i > SlotId.MinBoardIndex) boardSlots.Append(",");
                    boardSlots.Append($"s{i}:empty");
                    continue;
                }

                if (i > SlotId.MinBoardIndex) boardSlots.Append(",");
                boardSlots.Append($"s{i}:{card.DefId}:{card.Kind}");
                kindTally.TryGetValue(card.Kind, out var c);
                kindTally[card.Kind] = c + 1;
            }

            var tallyStr = new System.Text.StringBuilder();
            foreach (var kv in kindTally)
            {
                if (tallyStr.Length > 0) tallyStr.Append(" ");
                tallyStr.Append($"{kv.Key}={kv.Value}");
            }

            UnityEngine.Debug.Log(
                $"[DeckProbe] BoardAfterStartNode "
                + $"drawPile={deck.DrawPileUids.Count} "
                + $"playerPoolRemain={deck.PlayerCardPoolUids.Count} "
                + $"enemyPoolRemain={deck.EnemyCardPoolUids.Count} "
                + $"itemSlots={deck.ItemSlotUids.Count} "
                + $"board=[{boardSlots}] "
                + $"tally=[{tallyStr}]");
        }

        /// <summary>
        /// ADR-0039 诊断：StartNode 后 Core 血/相位与 HUD 快照对齐探针（仅 Editor / Development）。
        /// </summary>
        private static void LogAvatarDefeatProbeAfterStartNode(IArchitecture arch)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var phase = arch.GetSystem<IPhaseSystem>();
            var board = arch.GetModel<BoardModel>();
            var registry = arch.GetModel<CardRegistry>();
            var statSystem = arch.GetSystem<IStatSystem>();
            var avatarUid = board.AvatarUid.Value;
            var baseHp = -1;
            var effectiveHp = -1;
            var zoneNote = "unregistered";
            if (avatarUid > 0 && registry.TryGet(avatarUid, out var avatar) && avatar != null)
            {
                baseHp = (int)Math.Round(avatar.Stats.GetBase(StatId.Hp));
                effectiveHp = statSystem != null
                    ? statSystem.GetEffectiveInt(avatar, StatId.Hp)
                    : baseHp;
                zoneNote = avatar.Zone.Value.ToString();
            }

            var hud = PlayerInfoHudPresenter.TryGetInstance();
            var hudNote = hud != null ? "present" : "missing";
            Debug.Log(
                $"[AvatarDefeatProbe] AfterStartNode "
                + $"phase={phase?.CurrentPhase} "
                + $"avatarUid={avatarUid} zone={zoneNote} baseHp={baseHp} effectiveHp={effectiveHp} "
                + $"phaseIsDefeat={phase?.CurrentPhase == GamePhase.Defeat} "
                + $"hud={hudNote}");
            if (avatarUid > 0 && zoneNote != nameof(ZoneId.Avatar))
            {
                // ADR-0039 补遗：在册 Avatar 的 zone 不是 Avatar 属非法状态；
                // StartNode 已尝试归位自愈，此处仍异常说明有新的置死写入点，需立即归因。
                Debug.LogError(
                    $"[AvatarDefeatProbe] Avatar zone 非法：zone={zoneNote} baseHp={baseHp}（ADR-0039）");
            }
#endif
        }

        /// <summary>
        /// 建跑并复位表现侧卡视图/卡组/场地。
        private async UniTask StartBattleNodeInternalAsync(
            NodeDeckOptions options,
            CancellationToken cancellationToken)
        {
            if (_isBusy)
            {
                // 失败重开/清场竞态可能留下粘连忙碌；强制复位后继续，避免空场干等结算。
                Debug.LogWarning("[BattleSession] StartBattleNode：检测到粘连忙碌，强制复位后继续。");
                CancelPresentationWork();
                _drainInFlight = false;
                _isBusy = false;
                PresentationInputGates.ForceEndExternalHold("StartBattleNode.staleBusy");
            }

                        if (Cards == null || Deck == null || Field == null)
            {
                Debug.LogError("[BattleSession] 缺少 Card/Deck/Field 管理器，中止入场。");
                return;
            }

            _isBusy = true;
            _settlementRaised = false;

            try
            {
                // 进战硬清简要解释（悬停 + Notice），兜底清掉房内「金币不足」等粘连文案。
                BoardBriefTipPresenter.InstanceOrNull()?.HardClear();

                // 跨关前等前关 Drain/tween 收束，避免与 Opening 发牌交错。
                await WaitPresentationIdleAsync(cancellationToken);
                CancelPresentationWork();
                // StartNode 前清零卡牌占格，保留遗物/技能/PlayerInfo 持久 HUD。
                ResetCardPresentationSurface();
                if (!EnsurePresentationRuntimeInstalled())
                {
                    Debug.LogError("[BattleSession] 表现意图运行时未启动，中止入场。");
                    return;
                }

                var arch = NineGridArchitecture.Current;
                var phase = arch.GetSystem<IPhaseSystem>();
                var phaseBeforeStartNode = phase.CurrentPhase;
                if (!phase.CanExecute(GameCommandKind.StartNode))
                {
                    // 粘连 Present 锁 / 卡在房间相位时先解锁；仍非法才 Bootstrap，并保留中途遗物。
                    arch.GetSystem<IPresentationSyncSystem>()?.Clear();
                    PresentationInputGates.ForceEndExternalHold("StartBattleNode.unlock");
                    if (!phase.CanExecute(GameCommandKind.StartNode))
                    {
                        Debug.LogWarning(
                            $"[BattleSession] StartNode 非法 phase={phase.CurrentPhase}，先 BootstrapRun(preserve)。");
                        BootstrapRun(preserveRunInventory: true);
                        if (!EnsurePresentationRuntimeInstalled())
                        {
                            Debug.LogError(
                                "[BattleSession] BootstrapRun 后表现意图运行时未启动，中止入场。");
                            return;
                        }
                    }

                    phaseBeforeStartNode = phase.CurrentPhase;
                }

                options ??= NodeDeckOptions.CreateDefaultBattle();
                LogDeckOptionsProbe(options);

                // 记录本节点 EventLog 起点：结算演出据此定位 unusedHelpCards 金币事件。
                _nodeEventLogStart = arch.GetSystem<IActionPipelineSystem>().EventLog.Entries.Count;
                var result = phase.StartNode(options);
                if (!result.Accepted)
                {
                    Debug.LogError($"[BattleSession] StartNode 被拒: {result.Reason}");
                    return;
                }

                LogBoardDeckProbe(arch);

                // StartNode 后立即对齐持久 HUD，避免 Opening 期间遗物/技能/数值栏断口。
                RefreshPersistentInBattleUi(animate: false);
                PlayerInfoHudPresenter.TryGetInstance()?.SyncFromCore(animate: false);
                LogAvatarDefeatProbeAfterStartNode(arch);

                try
                {
                    var board = arch.GetModel<BoardModel>();
                    BattleTraceRecorder.BeginSessionIfNeeded(arch.GetModel<RunModel>().Seed.Value);
                    FlowTraceRecorder.BeginSessionIfNeeded(arch.GetModel<RunModel>().Seed.Value);
                    BattleTraceRecorder.RecordOp(new BattleTraceOp
                    {
                        opKind = "StartNode",
                        reason = "StartNode",
                        apiPath = "PhaseSystem.StartNode",
                        phaseBefore = phaseBeforeStartNode.ToString(),
                        phaseAfter = phase.CurrentPhase.ToString(),
                        attacker = null,
                        target = BattleTraceRecorder.TryCaptureCard(board.AvatarUid.Value),
                        eventStartIndex = 0,
                        eventEndIndex = arch.GetSystem<IActionPipelineSystem>().EventLog.Entries.Count,
                        events = new List<BattleTraceEventRow>(),
                        presentation = new BattleTracePresentation { accepted = true },
                        verdictHints = new BattleTraceVerdictHints(),
                    });
                    FieldTraceHelper.CountBoardOccupants(out var boardOcc, out var presOcc);
                    var deckCount = arch.GetModel<DeckModel>().DrawPileUids.Count;
                    FlowTraceRecorder.Record(
                        FlowTraceCategory.CoreGate,
                        FlowTraceNames.StartNode,
                        FieldTraceHelper.BuildStartNodePayload(
                            board.AvatarUid.Value,
                            deckCount,
                            boardOcc,
                            presOcc),
                        phaseBefore: phaseBeforeStartNode.ToString(),
                        phaseAfter: phase.CurrentPhase.ToString(),
                        accepted: true,
                        refBattleOpIndex: BattleTraceRecorder.LastOpIndex);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[BattleSession] BattleTrace StartNode: " + ex.Message);
                }

                var plan = CaptureOpeningPresentationPlan(arch);
                // Reset 已清手牌视图；在 Avatar/环发牌前立刻贴回持续持有，避免空窗。
                ApplyOpeningHandRestores(plan);
                var presentationCt = RenewPresentationToken();
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    presentationCt);
                var openingNode = 0;
                int.TryParse(FieldTraceHelper.ResolveNodeIndex(), out openingNode);
                PerfTraceRecorder.OpenBeat(DiagBeatKinds.OpeningDeal, openingNode);
                try
                {
                    await PresentOpeningAsync(plan, linkedCts.Token);
                    FieldTraceHelper.RecordOccupancySnapshot("startNodeAfter", FlowTraceBatchTags.StartNode);
                }
                finally
                {
                    PerfTraceRecorder.CloseBeat();
                }

                Cards?.AuditRegistryIntegrity("Opening.Settled");
                RegistryTraceRecorder.ResetVisualBaselineForOpening();
                if (phase.CurrentPhase == GamePhase.InteractionLoop)
                {
                    Cards?.AuditRegistryIntegrity("InteractionLoop.Idle");
                    RegistryTraceRecorder.BeginIdleWatch(linkedCts.Token);
                }

                RefreshPersistentInBattleUi(animate: false);
                PlayerInfoHudPresenter.TryGetInstance()?.SyncFromCore(animate: false);
                LogAvatarDefeatProbeAfterStartNode(arch);
                CheckAndNotifyLeaveTrapSettled();

                // 开局若已置离开机关清关标志（罕见），补走 PostKill→CompleteNodeIfCleared。
                if (phase.CurrentPhase == GamePhase.InteractionLoop
                    && arch.GetSystem<IDeckSystem>().IsNodeCleared())
                {
                    Coordinator.ResolvePostKillBoardFromCore();
                }

                TryEnterNodeSettlement();
            }
            finally
            {
                _isBusy = false;
            }
        }

        private void CheckAndNotifyLeaveTrapSettled()
        {
            if (Field == null)
            {
                return;
            }

            for (var slot = 1; slot <= 9; slot++)
            {
                if (slot == GroundSlotTopology.AvatarReservedSlot)
                {
                    continue;
                }

                if (Field.TryGetCardAt(slot, out var card) && card != null && !string.IsNullOrEmpty(card.DefId))
                {
                    if (card.DefId.StartsWith("trap.leave", StringComparison.OrdinalIgnoreCase))
                    {
                        TutorialCoach.NotifyLeaveTrapSettled(slot, card);
                        break;
                    }
                }
            }
        }

        private sealed class BoardPlacement
        {
            public BoardPlacement(int uid, string defId, int groundSlot)
            {
                Uid = uid;
                DefId = defId;
                GroundSlot = groundSlot;
            }

            public int Uid { get; }
            public string DefId { get; }
            public int GroundSlot { get; }
        }

        private sealed class OpeningPresentationPlan
        {
            public int AvatarUid;
            public string AvatarDefId;
            public readonly List<ManagedCard> DeckCards = new();
            public readonly List<BoardPlacement> BoardPlacements = new();
            public readonly List<DeckDealFromSource> DeckAdds = new();
            /// <summary>跨节点持续持有：就地贴回，无飞入。</summary>
            public readonly List<HandDealFromSource> HandRestores = new();
            /// <summary>本关遗物/技能开局授予：从源锚点飞入手牌。</summary>
            public readonly List<HandDealFromSource> HandDeals = new();
            public readonly HashSet<int> PendingDeckAddUids = new();
        }

        private sealed class DeckDealFromSource
        {
            public DeckDealFromSource(
                int uid,
                string defId,
                string sourceDefId,
                int sourceSlotIndex,
                long eventSequence)
            {
                Uid = uid;
                DefId = defId;
                SourceDefId = sourceDefId ?? string.Empty;
                SourceSlotIndex = sourceSlotIndex;
                EventSequence = eventSequence;
            }

            public int Uid { get; }
            public string DefId { get; }
            public string SourceDefId { get; }
            public int SourceSlotIndex { get; }
            public long EventSequence { get; }
        }

        private sealed class HandDealFromSource
        {
            public HandDealFromSource(
                int uid,
                string defId,
                string sourceDefId,
                int sourceSlotIndex,
                long eventSequence)
            {
                Uid = uid;
                DefId = defId;
                SourceDefId = sourceDefId ?? string.Empty;
                SourceSlotIndex = sourceSlotIndex;
                EventSequence = eventSequence;
            }

            public int Uid { get; }
            public string DefId { get; }
            public string SourceDefId { get; }
            public int SourceSlotIndex { get; }
            public long EventSequence { get; }
        }

        private OpeningPresentationPlan CaptureOpeningPresentationPlan(IArchitecture arch)
        {
            var registry = arch.GetModel<CardRegistry>();
            var board = arch.GetModel<BoardModel>();
            var deck = arch.GetModel<DeckModel>();
            var plan = new OpeningPresentationPlan();

            plan.AvatarUid = board.AvatarUid.Value;
            if (plan.AvatarUid > 0 && registry.TryGet(plan.AvatarUid, out var avatar))
            {
                plan.AvatarDefId = avatar.DefId;
            }

            var boardPlacements = new List<BoardPlacement>();
            for (var slot = SlotId.MinBoardIndex; slot <= SlotId.MaxBoardIndex; slot++)
            {
                var slotId = SlotId.Board(slot);
                var uid = board.GetCardUid(slotId);
                if (uid <= 0 || uid == plan.AvatarUid)
                {
                    continue;
                }

                if (!registry.TryGet(uid, out var card))
                {
                    continue;
                }

                boardPlacements.Add(new BoardPlacement(uid, card.DefId, slot));
            }

            plan.BoardPlacements.AddRange(boardPlacements);

            // 遗物/技能开局授予须先入 PendingDeckAddUids，再构建抽牌堆 Inject 列表；
            // 否则同一 uid 既 InjectDeck 又 DeckAdds，卡组槽位重复入列，战中补牌会
            // 卡组↔场地来回抢跑（#180 药水袋 / 飞刀袋等同路径）。
            CaptureOpeningDeckAdds(arch, plan);

            // 视觉卡组只注入 Core 抽牌堆（不含已在盘面上的开局 8 张；盘面环飞走发牌原点，不占 slot0）。
            for (var i = 0; i < deck.DrawPileUids.Count; i++)
            {
                var uid = deck.DrawPileUids[i];
                if (plan.PendingDeckAddUids.Contains(uid))
                {
                    continue;
                }

                if (!registry.TryGet(uid, out var card))
                {
                    continue;
                }

                var view = Cards.SpawnView(
                    uid,
                    card.DefId,
                    initialMode: CardDisplayMode.CardDeckMode,
                    kind: CoreCardPresentationMapper.ResolvePresentationKind(uid, card.DefId));
                if (view != null)
                {
                    plan.DeckCards.Add(view);
                    CoreCardPresentationMapper.ApplyToManagedCard(view);
                }
            }

            CaptureOpeningHandDeals(arch, plan);

            return plan;
        }

        private void CaptureOpeningDeckAdds(IArchitecture arch, OpeningPresentationPlan plan)
        {
            var deck = arch.GetModel<DeckModel>();
            var registry = arch.GetModel<CardRegistry>();
            var player = arch.GetModel<PlayerModel>();
            var entries = arch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            if (entries == null)
            {
                return;
            }

            var boardUids = new HashSet<int>();
            for (var p = 0; p < plan.BoardPlacements.Count; p++)
            {
                boardUids.Add(plan.BoardPlacements[p].Uid);
            }

            var spawnMeta = new Dictionary<int, (string sourceDefId, long sequence)>();
            for (var i = _nodeEventLogStart; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Type != CoreEventType.CardSpawned || entry.CardUid <= 0)
                {
                    continue;
                }

                if (spawnMeta.ContainsKey(entry.CardUid))
                {
                    continue;
                }

                var sourceDefId = entry.Cause ?? string.Empty;
                if (!IsOpeningGrantSource(sourceDefId))
                {
                    continue;
                }

                spawnMeta[entry.CardUid] = (sourceDefId, entry.Sequence);
            }

            for (var i = 0; i < deck.DrawPileUids.Count; i++)
            {
                var uid = deck.DrawPileUids[i];
                if (boardUids.Contains(uid) || !spawnMeta.TryGetValue(uid, out var meta))
                {
                    continue;
                }

                if (!registry.TryGet(uid, out var coreCard))
                {
                    continue;
                }

                plan.PendingDeckAddUids.Add(uid);
                var slotIndex = ResolveHandDealSourceSlotIndex(meta.sourceDefId, player);
                plan.DeckAdds.Add(new DeckDealFromSource(
                    uid,
                    coreCard.DefId,
                    meta.sourceDefId,
                    slotIndex,
                    meta.sequence));
            }

            plan.DeckAdds.Sort((a, b) =>
            {
                var cmp = a.SourceSlotIndex.CompareTo(b.SourceSlotIndex);
                return cmp != 0 ? cmp : a.EventSequence.CompareTo(b.EventSequence);
            });
        }

        private static bool IsOpeningGrantSource(string sourceDefId)
        {
            return !string.IsNullOrEmpty(sourceDefId)
                && (sourceDefId.StartsWith("relic.", StringComparison.Ordinal)
                    || sourceDefId.StartsWith("skill.", StringComparison.Ordinal));
        }

        private void CaptureOpeningHandDeals(IArchitecture arch, OpeningPresentationPlan plan)
        {
            var deck = arch.GetModel<DeckModel>();
            var registry = arch.GetModel<CardRegistry>();
            var player = arch.GetModel<PlayerModel>();
            var entries = arch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            if (deck.ItemSlotUids.Count == 0)
            {
                return;
            }

            var spawnMeta = new Dictionary<int, (string sourceDefId, long sequence)>();
            if (entries != null)
            {
                for (var i = _nodeEventLogStart; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (entry.Type != CoreEventType.CardSpawned || entry.CardUid <= 0)
                    {
                        continue;
                    }

                    if (spawnMeta.ContainsKey(entry.CardUid))
                    {
                        continue;
                    }

                    var sourceDefId = entry.Cause ?? string.Empty;
                    spawnMeta[entry.CardUid] = (sourceDefId, entry.Sequence);
                }
            }

            for (var i = 0; i < deck.ItemSlotUids.Count; i++)
            {
                var uid = deck.ItemSlotUids[i];
                if (!registry.TryGet(uid, out var coreCard))
                {
                    continue;
                }

                var sourceDefId = string.Empty;
                long eventSequence = i;
                var isOpeningGrant = false;
                if (spawnMeta.TryGetValue(uid, out var meta))
                {
                    sourceDefId = meta.sourceDefId;
                    eventSequence = meta.sequence;
                    isOpeningGrant = IsOpeningGrantSource(sourceDefId);
                }

                var slotIndex = ResolveHandDealSourceSlotIndex(sourceDefId, player);
                var entry = new HandDealFromSource(
                    uid,
                    coreCard.DefId,
                    sourceDefId,
                    slotIndex,
                    eventSequence);
                // 仅本关遗物/技能授予走飞入；其余为跑图持续持有，就地贴回。
                if (isOpeningGrant)
                {
                    plan.HandDeals.Add(entry);
                }
                else
                {
                    plan.HandRestores.Add(entry);
                }
            }

            plan.HandDeals.Sort((a, b) =>
            {
                var cmp = a.SourceSlotIndex.CompareTo(b.SourceSlotIndex);
                return cmp != 0 ? cmp : a.EventSequence.CompareTo(b.EventSequence);
            });
        }

        private static int ResolveHandDealSourceSlotIndex(string sourceDefId, PlayerModel player)
        {
            if (string.IsNullOrEmpty(sourceDefId) || player == null)
            {
                return 99999;
            }

            if (sourceDefId.StartsWith("relic.", StringComparison.Ordinal))
            {
                var relics = player.RelicDefIds;
                for (var i = 0; i < relics.Count; i++)
                {
                    if (relics[i] == sourceDefId)
                    {
                        return i;
                    }
                }

                return 9000;
            }

            return 99999;
        }
        private async UniTask PresentOpeningAsync(
            OpeningPresentationPlan plan,
            CancellationToken cancellationToken)
        {
            PresentationInputGates.SetOpening(true);
            try
            {
            Deck.ResetToStandby();
            if (plan.DeckCards.Count > 0)
            {
                Deck.InjectDeck(plan.DeckCards);
            }

            await Deck.BeginEntryAsync(cancellationToken);

            if (plan.AvatarUid > 0)
            {
                var avatarDefId = string.IsNullOrEmpty(plan.AvatarDefId)
                    ? CardManagerSingleton.StandardDefId
                    : plan.AvatarDefId;
                var avatar = Cards.SpawnView(
                    plan.AvatarUid,
                    avatarDefId,
                    initialMode: CardDisplayMode.GroundCardMode,
                    kind: CardPresentationKind.Avatar);

                if (avatar != null)
                {
                    CoreCardPresentationMapper.ApplyToManagedCard(avatar);
                    await Field.RequestRevealAvatarAsync(avatar, cancellationToken);

                    // 揭示被场地自身 busy 拒掉时：强制 skipBusyGuard 落格 5，避免孤儿视图卡在牌堆坐标。
                    if (!Field.TryGetSlotOf(avatar.Uid, out var avatarSlot)
                        || avatarSlot != GroundSlotTopology.AvatarReservedSlot)
                    {
                        Debug.LogWarning(
                            $"[BattleSession] Opening Avatar 未占格5，skipBusyGuard 兜底落位 uid={avatar.Uid}。");
                        Field.RequestPlaceCardAtAnchor(
                            GroundSlotTopology.AvatarReservedSlot,
                            avatar,
                            skipBusyGuard: true,
                            snapToAnchor: true);
                    }
                }
            }

            // 按内核盘面 uid→slot 就位：从发牌原点飞向格位（不占抽牌堆视觉 slot0）。
            var ring = GroundSlotTopology.ClockwiseRing;
            var dealInterval = Deck.LayoutSettings != null
                ? Deck.LayoutSettings.dealInterval
                : 0.06f;
            var flightHandles = new List<DealFlightHandle>();
            var openingBoardDealUids = new List<int>();
            for (var i = 0; i < ring.Count; i++)
            {
                var groundSlot = ring[i];
                for (var p = 0; p < plan.BoardPlacements.Count; p++)
                {
                    if (plan.BoardPlacements[p].GroundSlot == groundSlot)
                    {
                        openingBoardDealUids.Add(plan.BoardPlacements[p].Uid);
                        break;
                    }
                }
            }

            Deck.SetOpeningBoardDealPendingUids(openingBoardDealUids);
            try
            {
                for (var i = 0; i < ring.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var groundSlot = ring[i];
                    BoardPlacement placement = null;
                    for (var p = 0; p < plan.BoardPlacements.Count; p++)
                    {
                        if (plan.BoardPlacements[p].GroundSlot == groundSlot)
                        {
                            placement = plan.BoardPlacements[p];
                            break;
                        }
                    }

                    if (placement == null)
                    {
                        continue;
                    }

                    if (!Cards.TryGet(placement.Uid, out var boardCard) || boardCard == null)
                    {
                        boardCard = Cards.SpawnView(
                            placement.Uid,
                            placement.DefId,
                            initialMode: CardDisplayMode.GroundCardMode,
                            kind: CoreCardPresentationMapper.ResolvePresentationKind(
                                placement.Uid,
                                placement.DefId));
                        if (boardCard != null)
                        {
                            CoreCardPresentationMapper.ApplyToManagedCard(boardCard);
                        }
                    }

                    if (boardCard == null)
                    {
                        Debug.LogWarning(
                            $"[BattleSession] 开局盘面 Spawn 失败 uid={placement.Uid} slot={placement.GroundSlot}。");
                        continue;
                    }

                    var flightContext = new DealFlightContext(
                        Field.IsFieldBusy,
                        Field.ActiveDealFlightCount + flightHandles.Count + 1,
                        pendingRotateSteps: 0);
                    var (ok, handle) = await Deck.LaunchOpeningBoardDealFromOriginAsync(
                        boardCard,
                        placement.GroundSlot,
                        skipBusyGuard: true,
                        flightContext: flightContext,
                        cancellationToken: cancellationToken);
                    if (ok && openingBoardDealUids.Count > 0 && openingBoardDealUids[0] == placement.Uid)
                    {
                        openingBoardDealUids.RemoveAt(0);
                        Deck.SetOpeningBoardDealPendingUids(openingBoardDealUids);
                    }

                    FieldTraceHelper.RecordOpeningDealProgress(
                        placement.Uid,
                        placement.GroundSlot,
                        ok,
                        i);
                    if (!ok)
                    {
                        Debug.LogWarning(
                            $"[BattleSession] 就位失败 uid={placement.Uid} slot={placement.GroundSlot}，继续其余格并由 Sync 兜底。");
                        continue;
                    }

                    if (handle != null)
                    {
                        flightHandles.Add(handle);
                    }

                    if (i < ring.Count - 1 && dealInterval > 0f)
                    {
                        await UniTask.Delay(
                            TimeSpan.FromSeconds(dealInterval),
                            cancellationToken: cancellationToken);
                    }
                }

                if (flightHandles.Count > 0)
                {
                    await GroundFieldView.WaitDealFlightsSettledAsync(
                        flightHandles,
                        cancellationToken);
                }
            }
            finally
            {
                Deck.ClearOpeningBoardDealPendingUids();
            }

                if (plan.DeckAdds.Count > 0)
                {
                    for (var d = 0; d < plan.DeckAdds.Count; d++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var deckDeal = plan.DeckAdds[d];
                        if (Deck != null && Deck.ContainsUid(deckDeal.Uid))
                        {
                            continue;
                        }

                        if (!TryResolveHandDealOrigin(deckDeal.SourceDefId, out var origin))
                        {
                            if (Deck == null || !Deck.TryGetDefaultDealOrigin(out origin))
                            {
                                Debug.LogWarning(
                                    $"[BattleSession] Opening DeckAdd uid={deckDeal.Uid} source={deckDeal.SourceDefId} 无锚点，跳过。");
                                continue;
                            }

                            Debug.LogWarning(
                                $"[BattleSession] Opening DeckAdd uid={deckDeal.Uid} source={deckDeal.SourceDefId} 锚点未找到，fallback 卡组左侧。");
                        }

                        Cards.TryGet(deckDeal.Uid, out var ensureCard);
                        if (ensureCard == null)
                        {
                            ensureCard = Cards.SpawnView(
                                deckDeal.Uid,
                                deckDeal.DefId,
                                initialMode: CardDisplayMode.CardDeckMode,
                                kind: CoreCardPresentationMapper.ResolvePresentationKind(
                                    deckDeal.Uid, deckDeal.DefId));
                            if (ensureCard != null)
                            {
                                CoreCardPresentationMapper.ApplyToManagedCard(ensureCard);
                            }
                        }

                        // RandomInsertIndex → 按 Core 抽牌堆真实位置落点（赠牌可能被 Core 置顶/洗中段，
                        // 固定 append 到末尾会造成顶牌失同步）。
                        var deckOk = await Deck.AddCardAtFromOriginAsync(
                            CardDeckManagerSingleton.RandomInsertIndex,
                            ensureCard,
                            origin,
                            cancellationToken);
                        FieldTraceHelper.RecordOpeningGrantProgress(
                            deckDeal.Uid,
                            deckDeal.SourceDefId,
                            "deck",
                            deckOk,
                            d);
                        if (!deckOk)
                        {
                            Debug.LogWarning(
                                $"[BattleSession] Opening DeckAdd 失败 uid={deckDeal.Uid} source={deckDeal.SourceDefId}。");
                        }

                        if (d < plan.DeckAdds.Count - 1 && dealInterval > 0f)
                        {
                            await UniTask.Delay(
                                TimeSpan.FromSeconds(dealInterval),
                                cancellationToken: cancellationToken);
                        }
                    }
                }

                if (plan.HandDeals.Count > 0)
                {
                    // 本关遗物/技能飞入；持续持有已在 Opening 开头 ApplyOpeningHandRestores。
                    for (var h = 0; h < plan.HandDeals.Count; h++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var handDeal = plan.HandDeals[h];
                        if (!TryResolveHandDealOrigin(handDeal.SourceDefId, out var origin))
                        {
                            if (Deck == null || !Deck.TryGetDefaultDealOrigin(out origin))
                            {
                                Debug.LogWarning(
                                    $"[BattleSession] Opening HandDeal uid={handDeal.Uid} source={handDeal.SourceDefId} 无锚点，跳过。");
                                continue;
                            }

                            Debug.LogWarning(
                                $"[BattleSession] Opening HandDeal uid={handDeal.Uid} source={handDeal.SourceDefId} 锚点未找到，fallback 卡组左侧。");
                        }

                        Cards.TryGet(handDeal.Uid, out var ensureCard);
                        if (ensureCard == null)
                        {
                            ensureCard = Cards.SpawnView(
                                handDeal.Uid,
                                handDeal.DefId,
                                initialMode: CardDisplayMode.GroundCardMode,
                                kind: CoreCardPresentationMapper.ResolvePresentationKind(
                                    handDeal.Uid, handDeal.DefId));
                            if (ensureCard != null)
                            {
                                CoreCardPresentationMapper.ApplyToManagedCard(ensureCard);
                            }
                        }

                        var handOk = await Deck.DealCardToHandAsync(
                            handDeal.Uid,
                            handDeal.DefId,
                            origin,
                            ensureCard: ensureCard,
                            skipBusyGuard: true,
                            cancellationToken: cancellationToken);
                        FieldTraceHelper.RecordOpeningGrantProgress(
                            handDeal.Uid,
                            handDeal.SourceDefId,
                            "hand",
                            handOk,
                            h);
                        if (!handOk)
                        {
                            Debug.LogWarning(
                                $"[BattleSession] Opening HandDeal 失败 uid={handDeal.Uid} source={handDeal.SourceDefId}。");
                        }

                        if (h < plan.HandDeals.Count - 1 && dealInterval > 0f)
                        {
                            await UniTask.Delay(
                                TimeSpan.FromSeconds(dealInterval),
                                cancellationToken: cancellationToken);
                        }
                    }
                }

                // 发牌收束：先刷视觉与数值，再重放批内翻面（刺客领袖等 OnDeal）。
                // 层主技能在发牌落地后才结算/表现——发牌飞行中不得翻面（ADR-0016 串行门控）。
                CoreCardPresentationMapper.CommitAllSpawnedCards();
                CardFaceGenerationBootstrap.ApplyFromEventLog(NineGridArchitecture.Current, _nodeEventLogStart);
                await FlipPlaybackCoordinator.WaitIdleAsync(cancellationToken);

                // 开局收束终对账：赠牌/洗牌后视觉卡组序对齐 Core 抽牌堆（slot 0 = 下一张要发）。
                await BoardPresentationPlayer.SyncDeckVisualOrderFromCoreAsync(Deck, cancellationToken);
            }
            finally
            {
                PresentationInputGates.SetOpening(false);
                // 幂等兜底：异常/取消路径仍对齐镜像（重放翻面已落地时无动画直 Snap）。
                CoreCardPresentationMapper.CommitAllSpawnedCards();
                CardFaceGenerationBootstrap.ApplyFromEventLog(NineGridArchitecture.Current, _nodeEventLogStart);
            }
        }

        private void ApplyOpeningHandRestores(OpeningPresentationPlan plan)
        {
            if (plan == null || plan.HandRestores.Count == 0 || Hand == null || Cards == null)
            {
                return;
            }

            for (var r = 0; r < plan.HandRestores.Count; r++)
            {
                var restore = plan.HandRestores[r];
                Cards.TryGet(restore.Uid, out var restoreCard);
                if (restoreCard == null)
                {
                    restoreCard = Cards.SpawnView(
                        restore.Uid,
                        restore.DefId,
                        initialMode: CardDisplayMode.HandCardMode,
                        kind: CoreCardPresentationMapper.ResolvePresentationKind(
                            restore.Uid, restore.DefId));
                    if (restoreCard != null)
                    {
                        CoreCardPresentationMapper.ApplyToManagedCard(restoreCard);
                    }
                }

                if (restoreCard == null)
                {
                    Debug.LogWarning(
                        $"[BattleSession] Opening HandRestore 无视图 uid={restore.Uid}。");
                    continue;
                }

                var restoreOk = Hand.TryPlaceInHandImmediate(restoreCard, skipBusyGuard: true);
                FieldTraceHelper.RecordOpeningGrantProgress(
                    restore.Uid,
                    "item_slots.preserve",
                    "hand",
                    restoreOk,
                    r);
                if (!restoreOk)
                {
                    Debug.LogWarning(
                        $"[BattleSession] Opening HandRestore 失败 uid={restore.Uid}。");
                }
            }
        }

        private async UniTask WaitPresentationIdleAsync(
            CancellationToken cancellationToken,
            float timeoutSeconds = 2f)
        {
                        var deadline = Time.realtimeSinceStartup + Mathf.Max(0.1f, timeoutSeconds);
            while (Time.realtimeSinceStartup < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 勿用 hand.IsBusy / field.IsBusy 聚合（含 DirectorMainlineBusy），避免静态镜像粘连假忙。
                var fieldBusy = Field != null && Field.IsFieldBusy;
                var hand = Hand;
                var handSelfBusy = hand != null && hand.IsSelfBusy;
                var deckBusy = Deck != null && Deck.IsBusy;
                var battleBusy = ResolveBattlePresentation()?.IsBusy == true;

                if (!_drainInFlight
                    && !fieldBusy
                    && !handSelfBusy
                    && !deckBusy
                    && !battleBusy
                    && !IsPresentationMainlineBusy())
                {
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            Debug.LogWarning(
                $"[BattleSession] WaitPresentationIdle 超时({timeoutSeconds:0.##}s)：drain={_drainInFlight} " +
                $"fieldBusy={Field != null && Field.IsFieldBusy} " +
                $"directorBusy={IsPresentationMainlineBusy()}，继续清场。");
        }

    }
}
