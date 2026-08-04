using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 局内会话执行缝：busy / CTS / Opening / 结算门 / Channel 持有；
    /// 盘面与批次投影经 <see cref="BoardPresentationPlayer"/> / <see cref="CoreBatchProjectionCoordinator"/>。
    /// 由 <see cref="NineGrid.Presentation.Systems.BattleSessionSystem"/> 拥有。
    /// </summary>
    internal sealed partial class BattleSessionExecutor
    {
        private const int BoardSelectLockWaitMs = 3000;

        private IBattleSessionView _view;
        private bool _isBusy;
        private bool _settlementRaised;
        private bool _battleEndRaised;
        private bool _drainInFlight;
        private CancellationTokenSource _presentationCts;
        private int _nodeEventLogStart;
        private UseItemPresentationResult _pendingUseItemPresent;

        private QueuedBoardPresentChannel _explorePresentChannel;
        private CombatAttackPresentChannel _attackHitPresentChannel;
        private CombatCounterPresentChannel _attackCounterPresentChannel;
        private QueuedBoardPresentChannel _attackBoardPresentChannel;
        private UseItemPresentChannel _useItemPresentChannel;
        private QueuedBoardPresentChannel _useItemBoardPresentChannel;

        private Func<bool> _ensurePresentationRuntime;
        private Action<IntentClearReason> _shutdownPresentationRuntime;

        private IUnRegister _exploreRejectedUnRegister;
        private IUnRegister _attackRejectedUnRegister;
        private bool _recoveringRewardUi;

        public BattleSessionExecutor()
        {
            BoardPlayer = new BoardPresentationPlayer(this);
            Coordinator = new CoreBatchProjectionCoordinator(this);
        }

        public BoardPresentationPlayer BoardPlayer { get; }

        public CoreBatchProjectionCoordinator Coordinator { get; }

        public bool IsBusy => _isBusy;

        public bool IsBound => _view != null;

        public event Action OnNodeSettlementReady;

        public CardManagerSingleton Cards =>
            _view?.CardManager ?? CardEntityLifecycleHook.CardsOrNull();

        public CardDeckManagerSingleton Deck =>
            _view?.DeckManager ?? CardEntityLifecycleHook.DeckOrNull();

        public GroundFieldView Field =>
            _view?.FieldManager ?? GroundFieldGeometryHook.FieldOrNull();

        public CardHandManagerSingleton Hand =>
            _view?.HandManager ?? CardEntityLifecycleHook.HandOrNull();

        public RelicManagerSingleton Relic => _view?.RelicManager;

        public SelectorManagerSingleton Selector =>
            _view?.SelectorManager ?? UnityEngine.Object.FindFirstObjectByType<SelectorManagerSingleton>();

        public FieldBattleView BattleHost => _view?.BattleManager;

        public bool DrainInFlight
        {
            get => _drainInFlight;
            set => _drainInFlight = value;
        }

        public int NodeEventLogStart
        {
            get => _nodeEventLogStart;
            set => _nodeEventLogStart = value;
        }

        public UseItemPresentationResult PendingUseItemPresent
        {
            get => _pendingUseItemPresent;
            set => _pendingUseItemPresent = value;
        }

        public QueuedBoardPresentChannel ExplorePresentChannel => _explorePresentChannel;

        public CombatAttackPresentChannel AttackHitPresentChannel => _attackHitPresentChannel;

        public CombatCounterPresentChannel AttackCounterPresentChannel => _attackCounterPresentChannel;

        public QueuedBoardPresentChannel AttackBoardPresentChannel => _attackBoardPresentChannel;

        public UseItemPresentChannel UseItemPresentChannel => _useItemPresentChannel;

        public QueuedBoardPresentChannel UseItemBoardPresentChannel => _useItemBoardPresentChannel;

        public void Bind(IBattleSessionView view)
        {
            if (ReferenceEquals(_view, view) && view != null)
            {
                return;
            }

            UnbindInternal(cancelWork: false);
            _view = view;
            if (view != null)
            {
                _ensurePresentationRuntime = view.EnsurePresentationRuntimeInstalled;
                _shutdownPresentationRuntime = view.ShutdownPresentationRuntime;
            }
        }

        public void Unbind()
        {
            UnbindInternal(cancelWork: true);
        }

        public void UnbindIfView(IBattleSessionView view)
        {
            if (ReferenceEquals(_view, view))
            {
                Unbind();
            }
        }

        private void UnbindInternal(bool cancelWork)
        {
            if (cancelWork)
            {
                TeardownPresentationRuntime(IntentClearReason.LayerChange);
            }

            UnregisterPresentationIntentHandlers();
            _view = null;
            _ensurePresentationRuntime = null;
            _shutdownPresentationRuntime = null;
        }

        public void BindPresentChannels(
            QueuedBoardPresentChannel explore,
            CombatAttackPresentChannel attackHit,
            CombatCounterPresentChannel attackCounter,
            QueuedBoardPresentChannel attackBoard,
            UseItemPresentChannel useItem,
            QueuedBoardPresentChannel useItemBoard)
        {
            _explorePresentChannel = explore;
            _attackHitPresentChannel = attackHit;
            _attackCounterPresentChannel = attackCounter;
            _attackBoardPresentChannel = attackBoard;
            _useItemPresentChannel = useItem;
            _useItemBoardPresentChannel = useItemBoard;
        }

        public void ClearPresentChannels()
        {
            _explorePresentChannel = null;
            _attackHitPresentChannel = null;
            _attackCounterPresentChannel = null;
            _attackBoardPresentChannel = null;
            _useItemPresentChannel = null;
            _useItemBoardPresentChannel = null;
            _pendingUseItemPresent = default;
            BoardPlayer.ClearShuffleSink();
        }

        public CancellationToken EnsurePresentationToken()
        {
            if (_presentationCts == null)
            {
                _presentationCts = new CancellationTokenSource();
            }

            return _presentationCts.Token;
        }

        public CancellationToken RenewPresentationToken()
        {
            CancelPresentationWork();
            _presentationCts = new CancellationTokenSource();
            return _presentationCts.Token;
        }

        /// <summary>
        /// 软取消局内表现工作：取消 CTS、清 hold/sync、HardClear 意图缓冲；
        /// <b>不</b>关停 <see cref="IPresentationRuntimeSystem"/>（Opening Renew 依赖此语义）。
        /// </summary>
        public void CancelPresentationWork()
        {
            CancelPresentationWorkCore(teardownRuntime: false, IntentClearReason.LayerChange);
        }

        /// <summary>
        /// 硬关停：软取消后经 SceneRoot 回调 Stop 导演并清 mInstalled。
        /// </summary>
        public void TeardownPresentationRuntime(IntentClearReason reason = IntentClearReason.LayerChange)
        {
            CancelPresentationWorkCore(teardownRuntime: true, reason);
        }

        private void CancelPresentationWorkCore(bool teardownRuntime, IntentClearReason reason)
        {
            if (_presentationCts != null)
            {
                _presentationCts.Cancel();
                _presentationCts.Dispose();
                _presentationCts = null;
            }

            BoardPlayer.ClearShuffleSink();
            PresentationInputGates.ForceEndExternalHold(
                teardownRuntime ? "TeardownPresentationRuntime" : "CancelPresentationWork");

            if (teardownRuntime)
            {
                ShutdownPresentationRuntime(reason);
            }
            else
            {
                var runtime = TryGetPresentationRuntime();
                if (runtime != null && runtime.IsStarted)
                {
                    runtime.HardClearIntents(IntentClearReason.LayerChange);
                }
            }

            try
            {
                NineGridArchitecture.Current.GetSystem<IPresentationSyncSystem>().Clear();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BattleSession] Clear PresentationSync: " + ex.Message);
            }

            _drainInFlight = false;
        }

        public void RegisterPresentationIntentHandlers()
        {
            UnregisterPresentationIntentHandlers();
            var architecture = NineGridArchitecture.Current;
            if (architecture == null)
            {
                return;
            }

            _exploreRejectedUnRegister = architecture.RegisterEvent<ExploreIntentRejectedEvent>(
                OnExploreIntentRejected);
            _attackRejectedUnRegister = architecture.RegisterEvent<AttackIntentRejectedEvent>(
                OnAttackIntentRejected);
        }

        public void UnregisterPresentationIntentHandlers()
        {
            if (_exploreRejectedUnRegister != null)
            {
                _exploreRejectedUnRegister.UnRegister();
                _exploreRejectedUnRegister = null;
            }

            if (_attackRejectedUnRegister != null)
            {
                _attackRejectedUnRegister.UnRegister();
                _attackRejectedUnRegister = null;
            }
        }

        public InitialGameSnapshot BootstrapRun(
            InitialGameOptions options = null,
            bool preserveRunInventory = false)
        {
            RunInventorySnapshot inventory = null;
            if (preserveRunInventory)
            {
                inventory = CaptureRunInventory(NineGridArchitecture.Current);
            }

            TeardownPresentationRuntime(IntentClearReason.LayerChange);
            ResetPresentationSurface();
            CoreCardPresentationMapper.EnsureContentCatalogLoaded();

            var arch = NineGridArchitecture.Current;
            var snapshot = options != null
                ? InitialGameFactory.Create(arch, options)
                : InitialGameFactory.Create(arch);

            if (inventory != null)
            {
                RestoreRunInventory(arch, inventory);
            }

            RefreshPersistentInBattleUi(animate: false);
            PlayerInfoHudPresenter.TryGetInstance()?.SyncFromCore(animate: false);
            _settlementRaised = false;
            _battleEndRaised = false;
            _nodeEventLogStart = 0;
            try
            {
                BattleTraceRecorder.Clear();
                DiagTraceShared.EnsureSessionIdentity(snapshot.Seed);
                BattleTraceRecorder.BeginSessionIfNeeded(snapshot.Seed);
                FlowTraceRecorder.BeginSessionIfNeeded(snapshot.Seed);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BattleSession] BattleTrace BootstrapRun: " + ex.Message);
            }

            Debug.Log(
                $"[BattleSession] BootstrapRun 完成 avatar=#{snapshot.AvatarUid} @{snapshot.AvatarSlot}"
                + (preserveRunInventory ? " preserveRunInventory=true" : string.Empty));
            return snapshot;
        }

        private sealed class RunInventorySnapshot
        {
            public string[] RelicDefIds;
            public int ItemDeckCapacity;
            public int ItemSlotsCapacity;
            public string[] ItemSourcePoolDefIds;
            public string[] FixedItemCardDefIds;
            public string[] ItemSlotDefIds;
            public int Coins;
            public int InteractionCount;
            public string ProfessionId;
            public int Floor;
            public int NodeIndex;
            public ulong Seed;
        }

        private static RunInventorySnapshot CaptureRunInventory(IArchitecture arch)
        {
            if (arch == null)
            {
                return null;
            }

            var player = arch.GetModel<PlayerModel>();
            var run = arch.GetModel<RunModel>();
            var deck = arch.GetModel<DeckModel>();
            var registry = arch.GetModel<CardRegistry>();
            var relics = player.RelicDefIds;
            var sourcePool = player.ItemSourcePoolDefIds;
            var fixedCards = player.FixedItemCardDefIds;
            var itemSlots = deck.ItemSlotUids;
            var itemSlotDefIds = new string[itemSlots.Count];
            for (var i = 0; i < itemSlots.Count; i++)
            {
                CardInstance card;
                itemSlotDefIds[i] = registry.TryGet(itemSlots[i], out card) && card != null
                    ? card.DefId
                    : string.Empty;
            }

            var snapshot = new RunInventorySnapshot
            {
                RelicDefIds = new string[relics.Count],
                ItemDeckCapacity = player.ItemDeckCapacity,
                ItemSlotsCapacity = player.ItemSlotsCapacity,
                ItemSourcePoolDefIds = new string[sourcePool.Count],
                FixedItemCardDefIds = new string[fixedCards.Count],
                ItemSlotDefIds = itemSlotDefIds,
                Coins = player.Coins.Value,
                InteractionCount = player.InteractionCount.Value,
                ProfessionId = player.ProfessionId.Value ?? string.Empty,
                Floor = run.Floor.Value,
                NodeIndex = run.NodeIndex.Value,
                Seed = run.Seed.Value,
            };
            for (var i = 0; i < relics.Count; i++)
            {
                snapshot.RelicDefIds[i] = relics[i];
            }

            for (var i = 0; i < sourcePool.Count; i++)
            {
                snapshot.ItemSourcePoolDefIds[i] = sourcePool[i];
            }

            for (var i = 0; i < fixedCards.Count; i++)
            {
                snapshot.FixedItemCardDefIds[i] = fixedCards[i];
            }

            return snapshot;
        }

        private static void RestoreRunInventory(IArchitecture arch, RunInventorySnapshot inventory)
        {
            if (arch == null || inventory == null)
            {
                return;
            }

            var player = arch.GetModel<PlayerModel>();
            var run = arch.GetModel<RunModel>();
            var content = arch.GetSystem<IContentSystem>();
            var registry = arch.GetModel<CardRegistry>();
            var deck = arch.GetModel<DeckModel>();

            if (!string.IsNullOrEmpty(inventory.ProfessionId))
            {
                player.SetProfession(inventory.ProfessionId);
            }

            player.AddCoins(inventory.Coins - player.Coins.Value);
            player.AddInteractionCount(inventory.InteractionCount - player.InteractionCount.Value);
            player.SetItemDeckCapacity(inventory.ItemDeckCapacity);
            if (inventory.ItemSlotsCapacity > 0)
            {
                player.SetItemSlotsCapacity(inventory.ItemSlotsCapacity);
            }

            player.ReplaceItemSourcePool(inventory.ItemSourcePoolDefIds);
            player.ReplaceFixedItemCards(inventory.FixedItemCardDefIds);

            if (inventory.RelicDefIds != null)
            {
                var alreadyActive = new System.Collections.Generic.HashSet<string>();
                var existing = player.RelicDefIds;
                for (var i = 0; i < existing.Count; i++)
                {
                    alreadyActive.Add(existing[i]);
                }

                for (var i = 0; i < inventory.RelicDefIds.Length; i++)
                {
                    var defId = inventory.RelicDefIds[i];
                    if (string.IsNullOrEmpty(defId))
                    {
                        continue;
                    }

                    player.AddRelic(defId);
                    if (!alreadyActive.Contains(defId))
                    {
                        content?.ActivateRelic(defId);
                    }
                }
            }

            if (inventory.ItemSlotDefIds != null && content != null)
            {
                for (var i = 0; i < inventory.ItemSlotDefIds.Length; i++)
                {
                    var defId = inventory.ItemSlotDefIds[i];
                    if (string.IsNullOrEmpty(defId))
                    {
                        continue;
                    }

                    var draft = content.CreateDraft(defId);
                    if (draft == null)
                    {
                        continue;
                    }

                    deck.AddToItemSlots(draft.Create(registry));
                }
            }

            // Create 会 Reset RunModel；跨关恢复时保留进度，避免内容节点回绕。
            run.Floor.Value = inventory.Floor;
            run.NodeIndex.Value = inventory.NodeIndex;
            run.Seed.Value = inventory.Seed;
        }

        public void ClearPresentationSurface()
        {
            TeardownPresentationRuntime(IntentClearReason.LayerChange);
            ResolveBattlePresentation()?.CancelBattleWork();
            PresentationInputGates.ForceEndExternalHold("ClearPresentationSurface");
            _drainInFlight = false;
            ResetPresentationSurface();
            _settlementRaised = false;
            _battleEndRaised = false;
            _isBusy = false;
        }

        public void ClearCardPresentationSurface()
        {
            TeardownPresentationRuntime(IntentClearReason.LayerChange);
            ResolveBattlePresentation()?.CancelBattleWork();
            PresentationInputGates.ForceEndExternalHold("ClearCardPresentationSurface");
            _drainInFlight = false;
            ResetCardPresentationSurface();
            _settlementRaised = false;
            _battleEndRaised = false;
            _isBusy = false;
        }

        public void RefreshPersistentInBattleUi(bool animate = false)
        {
            EnsureRelicHudHookWired();
            if (RelicHudHook.SyncFromCore != null)
            {
                RelicHudHook.RequestSync();
            }
            else
            {
                Relic?.SyncFromCore();
            }

            // PlayerInfo 战中改由结算指令在表演锚点驱动；开局/作弊白名单仍可 SyncFromCore。
            _ = animate;
        }

        public UniTask StartBattleNodeAsync(
            NodeDeckOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return StartBattleNodeInternalAsync(options, cancellationToken);
        }

        public void NotifyPresentationBoardMayBeClear()
        {
            TryEnterNodeSettlement();
        }

        public bool TryEnterNodeSettlement()
        {
            if (_settlementRaised)
            {
                return false;
            }

            var arch = NineGridArchitecture.Current;
            var phase = arch.GetSystem<IPhaseSystem>().CurrentPhase;
            var pending = arch.GetModel<PendingChoiceModel>();
            // ADR-0021：清关直接 RoomChoice/Navigation；局内宝箱仍 InteractionLoop 当场 Bounce，
            // 不得在此误判。RewardItemChoice 仅保留给仍挂着 Reward Pending 的孤儿路径。
            if (!NodeSettlementReadiness.IsReady(phase, pending))
            {
                return false;
            }

            Debug.Log(
                $"[BattleSession] 节点结算就绪 phase={phase} pending={pending.Kind.Value}");
            RaiseSettlementReady();
            return true;
        }

        public void RaiseBattleEnded(bool victory)
        {
            if (_battleEndRaised)
            {
                return;
            }

            _battleEndRaised = true;

            var runtime = TryGetPresentationRuntime();
            if (runtime != null && runtime.IsStarted)
            {
                runtime.HardClearIntents(
                    victory ? IntentClearReason.PhaseChange : IntentClearReason.Defeat);
            }

            // HardClear 清导演租约；同步清 PresentationInputGates 外租约，避免战败留场锁输入。
            PresentationInputGates.ForceEndExternalHold("RaiseBattleEnded");

            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            arch?.SendEvent(new BattleSessionEndedEvent(victory));
        }

        /// <summary>
        /// Core 已 Defeat 时收口战败：Present 早退/取消/空盘面批不得漏 Raise。
        /// </summary>
        public void EnsureBattleEndedIfAvatarDefeated(
            PostKillBoardPresentationResult result,
            CancellationToken cancellationToken = default)
        {
            if (!result.AvatarDefeated)
            {
                return;
            }

            ResolveBattlePresentation()?.TryBeginAvatarDefeatPresentation(cancellationToken);
            RaiseBattleEnded(victory: false);
        }

        public bool TryResolveHandDealOrigin(string sourceDefId, out Transform origin)
        {
            origin = null;
            if (string.IsNullOrEmpty(sourceDefId))
            {
                return false;
            }

            if (sourceDefId.StartsWith("relic.", StringComparison.Ordinal))
            {
                return Relic != null && Relic.TryGetDealOrigin(sourceDefId, out origin);
            }

            return false;
        }

        public UniTask DrainPostKillBoardAsync(
            PostKillBoardPresentationResult result,
            CancellationToken cancellationToken)
        {
            return BoardPlayer.DrainPostKillBoardAsync(result, cancellationToken);
        }

        public void PresentShuffleIntoDeckFromEventLog(int startIndex)
        {
            BoardPlayer.PresentShuffleIntoDeckFromEventLog(startIndex);
        }

        public UniTask FlushPendingShuffleIntoPresentationAsync(CancellationToken ct)
        {
            return BoardPlayer.FlushPendingShuffleIntoPresentationAsync(ct);
        }

        public CombatHitPresentationResult ApplyCombatHit(int attackerUid, int targetUid)
        {
            return Coordinator.ApplyCombatHitFromCore(attackerUid, targetUid);
        }

        public PostKillBoardPresentationResult ResolvePostKillBoard()
        {
            return Coordinator.ResolvePostKillBoardFromCore();
        }

        public void OnExploreBatchProjected(
            int startIndex,
            int boardSlot,
            PostKillBoardPresentationResult result)
        {
            Coordinator.OnExploreBatchProjected(startIndex, boardSlot, result);
        }

        public void OnAttackHitBatchProjected(
            int startIndex,
            int boardSlot,
            int resolvedCombatUid,
            PostKillBoardPresentationResult result)
        {
            Coordinator.OnAttackHitBatchProjected(startIndex, boardSlot, resolvedCombatUid, result);
        }

        public void OnAttackBoardBatchProjected(
            int startIndex,
            int boardSlot,
            PostKillBoardPresentationResult result)
        {
            Coordinator.OnAttackBoardBatchProjected(startIndex, boardSlot, result);
        }

        public void OnAttackCounterBatchProjected(
            int startIndex,
            int attackerBoardSlot,
            int attackerUid,
            PostKillBoardPresentationResult result)
        {
            Coordinator.OnAttackCounterBatchProjected(startIndex, attackerBoardSlot, attackerUid, result);
        }

        public void OnUseItemBatchProjected(
            int startIndex,
            int boardSlot,
            PostKillBoardPresentationResult result)
        {
            Coordinator.OnUseItemBatchProjected(startIndex, boardSlot, result);
        }

        public void OnUseItemBoardBatchProjected(
            int startIndex,
            int boardSlot,
            PostKillBoardPresentationResult result)
        {
            Coordinator.OnUseItemBoardBatchProjected(startIndex, boardSlot, result);
        }

        public void OnUseItemResolvedWithoutKill()
        {
            Coordinator.OnUseItemResolvedWithoutKill();
        }

        public UniTask PlayDirectorAttackHitPresentAsync(
            int boardSlot,
            int resolvedCombatUid,
            PostKillBoardPresentationResult result,
            CancellationToken token)
        {
            var battle = ResolveBattlePresentation();
            if (battle == null)
            {
                Debug.LogWarning("[BattleSession] PlayDirectorAttackHitPresent：无 FieldBattlePresentationSystem。");
                return UniTask.CompletedTask;
            }

            return battle.PlayDirectorAttackHitPresentAsync(boardSlot, resolvedCombatUid, result, token);
        }

        public UniTask PlayDirectorCounterPresentAsync(
            int attackerSlot,
            int attackerUid,
            PostKillBoardPresentationResult result,
            CancellationToken token)
        {
            var battle = ResolveBattlePresentation();
            if (battle == null)
            {
                Debug.LogWarning("[BattleSession] PlayDirectorCounterPresent：无 FieldBattlePresentationSystem。");
                return UniTask.CompletedTask;
            }

            return battle.PlayDirectorCounterPresentAsync(attackerSlot, attackerUid, result, token);
        }

        public static void AssertOccupancySyncForbidden(string reason, string detail = null)
        {
            OccupancyForceSyncGuard.RecordForbiddenSync(reason, detail);
            var message =
                "[BattleSession] #10 占格强制对账断言触发（正常路径永不应发生）。reason="
                + (reason ?? string.Empty)
                + " detail="
                + (detail ?? string.Empty);
            Debug.LogError(message);
            Debug.Assert(false, message);
        }

        public static IFieldBattlePresentationSystem ResolveBattlePresentation()
        {
            return NineGridArchitecture.Interface?.GetSystem<IFieldBattlePresentationSystem>();
        }

        public static bool IsPresentationMainlineBusy()
        {
            var runtime = TryGetPresentationRuntime();
            return runtime != null && runtime.IsStarted && runtime.MainlineBusy.Value;
        }

        public static IPresentationRuntimeSystem TryGetPresentationRuntime()
        {
            var architecture = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            return architecture != null
                ? architecture.GetSystem<IPresentationRuntimeSystem>()
                : null;
        }

        private void RaiseSettlementReady()
        {
            _settlementRaised = true;
            // ADR-0026：清关进选房前卸掉抽牌堆残留视图（场上清残留已由 Core/Present 处理；
            // 卡组视图若等到下一关 StartBattle 才 Reset，选房阶段会看见上局牌）。
            ClearResidualBattleDeckViews();
            RunUnusedHelpCardSettlementPresentationAsync(
                _nodeEventLogStart,
                EnsurePresentationToken()).Forget();
            OnNodeSettlementReady?.Invoke();
            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            arch?.SendEvent(new BattleSessionSettlementReadyEvent());
        }

        /// <summary>
        /// 清关/跳关选房前：卸掉卡组槽内战斗残留视图并 Release。
        /// 道具卡格在手牌，不受影响；下一关 SetupNodeDeck 会重建抽牌堆。
        /// </summary>
        private void ClearResidualBattleDeckViews()
        {
            var deck = Deck;
            var cards = Cards;
            if (deck == null)
            {
                return;
            }

            // 先收集回库途中（尚未入槽）的视图，Detach 只会清槽内牌。
            var orphanInFlight = new List<ManagedCard>();
            if (cards != null)
            {
                foreach (var card in cards.EnumerateCards())
                {
                    if (card != null
                        && card.Uid > 0
                        && deck.IsReturnInFlight(card.Uid))
                    {
                        orphanInFlight.Add(card);
                    }
                }
            }

            var detached = deck.DetachAllInGameCards();
            var seen = new HashSet<int>();
            void ReleaseOne(ManagedCard card, string reason)
            {
                if (card == null || card.Uid <= 0 || !seen.Add(card.Uid) || cards == null)
                {
                    return;
                }

                if (card.Transform != null)
                {
                    CardDeckTween.KillMotion(card.Transform, reason, card.Uid);
                }

                cards.Release(card, reason);
            }

            for (var i = 0; i < detached.Count; i++)
            {
                ReleaseOne(detached[i], "Settlement.ClearResidualDeck");
            }

            for (var i = 0; i < orphanInFlight.Count; i++)
            {
                var card = orphanInFlight[i];
                if (card == null || card.Uid <= 0 || seen.Contains(card.Uid))
                {
                    continue;
                }

                if (cards != null && cards.TryGet(card.Uid, out var live) && live != null)
                {
                    ReleaseOne(live, "Settlement.ClearResidualDeckInFlight");
                }
            }

            // 漏网：仍标 CardDeckMode 但不在手牌的孤儿（含 AddAnchor 途中）。
            if (cards == null)
            {
                return;
            }

            var hand = Hand;
            var leftovers = new List<ManagedCard>();
            foreach (var card in cards.EnumerateCards())
            {
                if (card == null
                    || card.Uid <= 0
                    || card.DisplayMode != CardDisplayMode.CardDeckMode
                    || seen.Contains(card.Uid))
                {
                    continue;
                }

                if (hand != null && hand.ContainsUid(card.Uid))
                {
                    continue;
                }

                leftovers.Add(card);
            }

            for (var i = 0; i < leftovers.Count; i++)
            {
                ReleaseOne(leftovers[i], "Settlement.ClearResidualDeckOrphan");
            }
        }

        private async UniTaskVoid RunUnusedHelpCardSettlementPresentationAsync(
            int startIndex,
            CancellationToken cancellationToken)
        {
            try
            {
                await PresentUnusedHelpCardSettlementFromEventLogAsync(startIndex, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BattleSession] 残留帮助卡结算演出异常: " + ex.Message);
            }
        }

        private bool EnsurePresentationRuntimeInstalled()
        {
            if (_ensurePresentationRuntime != null)
            {
                if (_ensurePresentationRuntime())
                {
                    return true;
                }

                Debug.LogWarning(
                    "[BattleSession] EnsurePresentationRuntimeInstalled 后导演仍未启动。");
                return false;
            }

            Debug.LogWarning(
                "[BattleSession] PresentationSceneRoot 未绑定 Runtime 生命周期，无法安装导演。");
            return false;
        }

        private void ShutdownPresentationRuntime(IntentClearReason reason)
        {
            if (_shutdownPresentationRuntime != null)
            {
                _shutdownPresentationRuntime(reason);
                return;
            }

            ClearPresentChannels();
            var runtime = TryGetPresentationRuntime();
            if (runtime != null && runtime.IsStarted)
            {
                runtime.Stop(reason);
            }
        }

        private void ResetPresentationSurface()
        {
            ResetCardPresentationSurface();
            ClearPersistentInBattleHud();
        }

        private void ResetCardPresentationSurface()
        {
            // Bounce 退出 DelayedCall 可能跨节点；清场前必须先终止选择会话，避免陈旧句柄误触新视图。
            Selector?.HideChoice();
            ResolveBattlePresentation()?.CancelBattleWork();
            PresentationInputGates.Reset("ResetCardPresentationSurface");
            Hand?.ClearHand();
            Deck?.ResetToStandby();
            Field?.ClearField(force: true);
            Cards?.ReleaseAll("Presentation.ResetCardSurface");
            _isBusy = false;
        }

        private void ClearPersistentInBattleHud()
        {
            EnsureRelicHudHookWired();
            if (RelicHudHook.Clear != null)
            {
                RelicHudHook.RequestClear();
            }
            else
            {
                Relic?.Clear();
            }

            PlayerInfoHudPresenter.TryGetInstance()?.ClearSnapshot();
        }

        private static void EnsureRelicHudHookWired()
        {
            if (RelicHudHook.SyncFromCore == null || RelicHudHook.Clear == null)
            {
                RelicHudHook.RequestWire();
            }
        }

        private static void EnsureRewardChoiceHookWired()
        {
            if (RewardChoiceCoreHook.SelectReward == null || RewardChoiceCoreHook.SkipHelpChoice == null)
            {
                RewardChoiceCoreHook.RequestWire();
            }
        }

        private void OnExploreIntentRejected(ExploreIntentRejectedEvent e)
        {
            TryRecoverOrphanMidBattleRewardUi("ExploreRejected");
        }

        private void OnAttackIntentRejected(AttackIntentRejectedEvent e)
        {
            TryRecoverOrphanMidBattleRewardUi("AttackRejected");
        }

        private void RequestSyncBoardFromCore()
        {
            AssertOccupancySyncForbidden("requestSyncBoardFromCore", "soft");
        }

        // Choice / Opening / UseItem / UnusedHelp 见 partial 文件。
    }
}
