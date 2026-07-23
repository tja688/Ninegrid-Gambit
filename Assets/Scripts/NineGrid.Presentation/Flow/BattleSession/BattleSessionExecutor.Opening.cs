using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
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
                if (!phase.CanExecute(GameCommandKind.StartNode))
                {
                    Debug.LogWarning(
                        $"[BattleSession] StartNode 非法 phase={phase.CurrentPhase}，先 BootstrapRun。");
                    BootstrapRun();
                    if (!EnsurePresentationRuntimeInstalled())
                    {
                        Debug.LogError(
                            "[BattleSession] BootstrapRun 后表现意图运行时未启动，中止入场。");
                        return;
                    }
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
                        phaseBefore = GamePhase.None.ToString(),
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
                        phaseBefore: GamePhase.None.ToString(),
                        phaseAfter: phase.CurrentPhase.ToString(),
                        accepted: true,
                        refBattleOpIndex: BattleTraceRecorder.LastOpIndex);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[BattleSession] BattleTrace StartNode: " + ex.Message);
                }

                var plan = CaptureOpeningPresentationPlan(arch);
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

                // 开局即空怪：IsNodeCleared 但尚未 OfferReward，先走 PostKill→CompleteNodeIfCleared。
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
                if (slot == GroundSlotTopology.AvatarReservedSlot)
                {
                    continue;
                }

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

            // 视觉卡组顺序：开局环板上牌优先（ClockwiseRing 末位是 slot4，便于开局就位），
            // 再拼抽牌堆剩余。
            var ring = GroundSlotTopology.ClockwiseRing;
            for (var i = 0; i < ring.Count; i++)
            {
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

                var view = Cards.SpawnView(
                    placement.Uid,
                    placement.DefId,
                    initialMode: CardDisplayMode.CardDeckMode,
                    kind: CoreCardPresentationMapper.ResolvePresentationKind(placement.Uid, placement.DefId));
                if (view != null)
                {
                    plan.DeckCards.Add(view);
                    CoreCardPresentationMapper.ApplyToManagedCard(view);
                }
            }

            // 环序未覆盖的板上牌（兜底）
            for (var p = 0; p < plan.BoardPlacements.Count; p++)
            {
                var placement = plan.BoardPlacements[p];
                if (Cards.TryGet(placement.Uid, out _))
                {
                    continue;
                }

                var view = Cards.SpawnView(
                    placement.Uid,
                    placement.DefId,
                    initialMode: CardDisplayMode.CardDeckMode,
                    kind: CoreCardPresentationMapper.ResolvePresentationKind(placement.Uid, placement.DefId));
                if (view != null)
                {
                    plan.DeckCards.Add(view);
                    CoreCardPresentationMapper.ApplyToManagedCard(view);
                }
            }

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

            CaptureOpeningDeckAdds(arch, plan);
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
            if (deck.ItemSlotUids.Count == 0 || entries == null)
            {
                return;
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
                spawnMeta[entry.CardUid] = (sourceDefId, entry.Sequence);
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
                if (spawnMeta.TryGetValue(uid, out var meta))
                {
                    sourceDefId = meta.sourceDefId;
                    eventSequence = meta.sequence;
                }

                var slotIndex = ResolveHandDealSourceSlotIndex(sourceDefId, player);
                plan.HandDeals.Add(new HandDealFromSource(
                    uid,
                    coreCard.DefId,
                    sourceDefId,
                    slotIndex,
                    eventSequence));
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
            if (plan.DeckCards.Count > 0)
            {
                Deck.ResetToStandby();
                Deck.InjectDeck(plan.DeckCards);
                await Deck.BeginEntryAsync(cancellationToken);
            }

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

            // 按内核盘面 uid→slot 就位：走卡组管理器完整发牌缓动（与 DealOpeningRing 同轨迹）。
            var ring = GroundSlotTopology.ClockwiseRing;
            var dealInterval = Deck.LayoutSettings != null
                ? Deck.LayoutSettings.dealInterval
                : 0.06f;
            var flightHandles = new List<DealFlightHandle>();

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

                    Cards.TryGet(placement.Uid, out var ensureCard);
                    var flightContext = new DealFlightContext(
                        Field.IsFieldBusy,
                        Field.ActiveDealFlightCount + flightHandles.Count + 1,
                        pendingRotateSteps: 0);
                    var (ok, handle) = await Deck.DealCardByUidWithFlightAsync(
                        placement.Uid,
                        placement.GroundSlot,
                        ensureCard: ensureCard,
                        skipBusyGuard: true,
                        flightContext: flightContext,
                        cancellationToken: cancellationToken);
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

                if (plan.DeckAdds.Count > 0)
                {
                                        for (var d = 0; d < plan.DeckAdds.Count; d++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var deckDeal = plan.DeckAdds[d];
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

                        var deckOk = await Deck.AddCardAtFromOriginAsync(
                            Deck.DeckCount,
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
            }
            finally
            {
                PresentationInputGates.SetOpening(false);
                // #10：开局不再靠 Sync 自愈占格镜像；几何登记由发牌表演维护，合法性由 Flow idle 裁决。
                // 开局表演收束：编排主线全场 Commit 投影（非旁路 Set*）。
                CoreCardPresentationMapper.CommitAllSpawnedCards();
                PresentationOutputProjector.UpdateAvatarDebugText();
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
