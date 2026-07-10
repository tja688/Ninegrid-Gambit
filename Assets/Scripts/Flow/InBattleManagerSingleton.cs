using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Flow.Diagnostics;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 游戏局内管理器单例：内核 ↔ 表现中转与局内总控编排（入场、通关结算钩子）。
    /// 非全部效果的唯一编排器；部分效果仍可由表现自取或由内核驱动对应脚本。
    /// </summary>
    public sealed class InBattleManagerSingleton : MonoBehaviour
    {
        private static InBattleManagerSingleton _instance;

        [Tooltip("运行时自动查找 CardManagerSingleton.Instance；也可手动拖入覆盖。")]
        [SerializeField] private CardManagerSingleton cardManager;

        [Tooltip("运行时自动查找 CardDeckManagerSingleton.Instance；也可手动拖入覆盖。")]
        [SerializeField] private CardDeckManagerSingleton deckManager;

        [Tooltip("运行时自动查找 GroundFieldManagerSingleton.Instance；也可手动拖入覆盖。")]
        [SerializeField] private GroundFieldManagerSingleton fieldManager;

        [Tooltip("运行时自动查找 RelicManagerSingleton.Instance；也可手动拖入覆盖。")]
        [SerializeField] private RelicManagerSingleton relicManager;

        [Tooltip("运行时自动查找 PlayerSkillManagerSingleton.Instance；也可手动拖入覆盖。")]
        [SerializeField] private PlayerSkillManagerSingleton skillManager;

        [Tooltip("面板路由；留空则运行时在同物体或场景中查找 UiPanelRouter。")]
        [SerializeField] private UiPanelRouter panelRouter;

        private const string StatBoostCardDefId = "help.stat_boost_card";
        private static readonly string[] StatBoostOptions = { "Attack", "Armor", "Hp" };

        private bool _isBusy;
        private bool _settlementRaised;
        private bool _fieldSignalSubscribed;
        private CancellationTokenSource _presentationCts;

        public static InBattleManagerSingleton Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<InBattleManagerSingleton>();
                }

                return _instance;
            }
        }

        public bool IsBusy => _isBusy;

        /// <summary>
        /// 内核确认节点可结算（IsNodeCleared 或已进入 RewardItemChoice）时触发；UI 层订阅。
        /// </summary>
        public event Action OnNodeSettlementReady;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            ResolveManagers();
            SubscribeFieldSignal();
            RegisterCombatHitSink();
        }

        private void OnDestroy()
        {
            CancelPresentationWork();
            UnsubscribeFieldSignal();
            UnregisterCombatHitSink();
#if UNITY_EDITOR
            // Play 退出时先于域重载导出，避免静态会话被清掉。
            BattleTraceRecorder.ExportOnPlayExit("OnDestroy");
#endif
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// DevTest：将 Avatar MaxHp/Hp 设为指定值并刷新表现。
        /// </summary>
        public bool TryCheatSetAvatarHp(int hp)
        {
            if (hp <= 0)
            {
                return false;
            }

            var arch = NineGridArchitecture.Current;
            var board = arch.GetModel<BoardModel>();
            var avatarUid = board.AvatarUid.Value;
            if (avatarUid <= 0
                || !arch.GetModel<CardRegistry>().TryGet(avatarUid, out var avatar))
            {
                Debug.LogWarning("[InBattleManager] Avatar 不存在，无法改血。");
                return false;
            }

            avatar.Stats.SetBase(StatId.MaxHp, hp);
            avatar.Stats.SetBase(StatId.Hp, hp);

            ResolveManagers();
            if (cardManager != null
                && cardManager.TryGet(avatarUid, out var view)
                && view != null)
            {
                CoreCardPresentationMapper.ApplyToManagedCard(view);
            }

            UpdateAvatarDebugText();
            Debug.Log($"[InBattleManager] Avatar#{avatarUid} MaxHp/Hp → {hp}");
            return true;
        }

        /// <summary>
        /// 建跑并复位表现侧卡视图/卡组/场地。
        /// </summary>
        public InitialGameSnapshot BootstrapRun(InitialGameOptions options = null)
        {
            ResolveManagers();
            CancelPresentationWork();
            ResetPresentationSurface();
            CoreCardPresentationMapper.EnsureContentCatalogLoaded();

            var arch = NineGridArchitecture.Current;
            var snapshot = options != null
                ? InitialGameFactory.Create(arch, options)
                : InitialGameFactory.Create(arch);

            SyncContentPanels();
            _settlementRaised = false;
            try
            {
                BattleTraceRecorder.Clear();
                BattleTraceRecorder.BeginSessionIfNeeded(snapshot.Seed);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[InBattleManager] BattleTrace BootstrapRun: " + ex.Message);
            }

            Debug.Log($"[InBattleManager] BootstrapRun 完成 avatar=#{snapshot.AvatarUid} @{snapshot.AvatarSlot}");
            return snapshot;
        }

        /// <summary>
        /// 公开清场：回主菜单等生命周期清理用。
        /// </summary>
        public void ClearPresentationSurface()
        {
            CancelPresentationWork();
            FieldBattleManagerSingleton.Instance?.CancelBattleWork();
            ResetPresentationSurface();
            _settlementRaised = false;
            _isBusy = false;
        }

        /// <summary>
        /// StartNode → 取真实 uid/defId → 正式牌组入场并按内核盘面就位。
        /// </summary>
        public UniTask StartBattleNodeAsync(
            NodeDeckOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return StartBattleNodeInternalAsync(options, cancellationToken);
        }

        /// <summary>
        /// 表现侧板面可能已无敌时回调；内部仍以内核 IsNodeCleared / 相位为准。
        /// </summary>
        public void NotifyPresentationBoardMayBeClear()
        {
            TryEnterNodeSettlement();
        }

        /// <summary>
        /// 若内核确认通关（或已落在奖励相位），触发结算推进事件（UI stub）。
        /// </summary>
        public bool TryEnterNodeSettlement()
        {
            if (_settlementRaised)
            {
                return false;
            }

            var arch = NineGridArchitecture.Current;
            var phase = arch.GetSystem<IPhaseSystem>().CurrentPhase;
            var cleared = arch.GetSystem<IDeckSystem>().IsNodeCleared();
            var inReward = phase == GamePhase.RewardItemChoice;

            if (!cleared && !inReward)
            {
                return false;
            }

            _settlementRaised = true;
            Debug.Log(
                $"[InBattleManager] 节点结算就绪 phase={phase} isNodeCleared={cleared}（UI 尚未接入，仅事件）");
            OnNodeSettlementReady?.Invoke();
            return true;
        }

        private async UniTask StartBattleNodeInternalAsync(
            NodeDeckOptions options,
            CancellationToken cancellationToken)
        {
            if (_isBusy)
            {
                Debug.LogWarning("[InBattleManager] 当前忙碌，无法 StartBattleNode。");
                return;
            }

            ResolveManagers();
            if (cardManager == null || deckManager == null || fieldManager == null)
            {
                Debug.LogError("[InBattleManager] 缺少 Card/Deck/Field 管理器，中止入场。");
                return;
            }

            _isBusy = true;
            _settlementRaised = false;
            CancelPresentationWork();

            try
            {
                var arch = NineGridArchitecture.Current;
                var phase = arch.GetSystem<IPhaseSystem>();
                if (!phase.CanExecute(GameCommandKind.StartNode))
                {
                    Debug.LogWarning(
                        $"[InBattleManager] StartNode 非法 phase={phase.CurrentPhase}，先 BootstrapRun。");
                    BootstrapRun();
                }

                options ??= NodeDeckOptions.CreateDefaultBattle();
                var result = phase.StartNode(options);
                if (!result.Accepted)
                {
                    Debug.LogError($"[InBattleManager] StartNode 被拒: {result.Reason}");
                    return;
                }

                try
                {
                    var board = arch.GetModel<BoardModel>();
                    BattleTraceRecorder.BeginSessionIfNeeded(arch.GetModel<RunModel>().Seed.Value);
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
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[InBattleManager] BattleTrace StartNode: " + ex.Message);
                }

                // StartNode 后、Spawn 前彻底清表现，避免与新 uid 冲突。
                ResetPresentationSurface();
                var plan = CaptureOpeningPresentationPlan(arch);
                var presentationCt = RenewPresentationToken();
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    presentationCt);
                await PresentOpeningAsync(plan, linkedCts.Token);
                SyncContentPanels();

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

            // 视觉卡组顺序：抽牌堆剩余在前，再按开局环序拼接已上板非 Avatar（便于 Entry 演出）。
            for (var i = 0; i < deck.DrawPileUids.Count; i++)
            {
                var uid = deck.DrawPileUids[i];
                if (!registry.TryGet(uid, out var card))
                {
                    continue;
                }

                var view = cardManager.SpawnView(uid, card.DefId, initialMode: CardDisplayMode.CardDeckMode);
                if (view != null)
                {
                    plan.DeckCards.Add(view);
                    CoreCardPresentationMapper.ApplyToManagedCard(view);
                }
            }

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

                var view = cardManager.SpawnView(
                    placement.Uid,
                    placement.DefId,
                    initialMode: CardDisplayMode.CardDeckMode);
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
                if (cardManager.TryGet(placement.Uid, out _))
                {
                    continue;
                }

                var view = cardManager.SpawnView(
                    placement.Uid,
                    placement.DefId,
                    initialMode: CardDisplayMode.CardDeckMode);
                if (view != null)
                {
                    plan.DeckCards.Add(view);
                    CoreCardPresentationMapper.ApplyToManagedCard(view);
                }
            }

            return plan;
        }

        private async UniTask PresentOpeningAsync(
            OpeningPresentationPlan plan,
            CancellationToken cancellationToken)
        {
            if (plan.DeckCards.Count > 0)
            {
                deckManager.ResetToStandby();
                deckManager.InjectDeck(plan.DeckCards);
                await deckManager.BeginEntryAsync(cancellationToken);
            }

            if (plan.AvatarUid > 0)
            {
                var avatar = cardManager.SpawnView(
                    plan.AvatarUid,
                    string.IsNullOrEmpty(plan.AvatarDefId)
                        ? CardManagerSingleton.StandardDefId
                        : plan.AvatarDefId,
                    initialMode: CardDisplayMode.GroundCardMode);

                if (avatar != null)
                {
                    CoreCardPresentationMapper.ApplyToManagedCard(avatar);
                    await fieldManager.RequestRevealAvatarAsync(avatar, cancellationToken);
                }
            }

            // 按内核盘面 uid→slot 就位：走卡组管理器完整发牌缓动（与 DealOpeningRing 同轨迹）。
            var ring = GroundSlotTopology.ClockwiseRing;
            var dealInterval = deckManager.LayoutSettings != null
                ? deckManager.LayoutSettings.dealInterval
                : 0.06f;
            var moveDuration = deckManager.LayoutSettings != null
                ? deckManager.LayoutSettings.moveDuration
                : 0.28f;
            var dealtAny = false;

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

                // 交错启动：不等单张 moveDuration，只隔 dealInterval（与 DealOpeningRingInternal 一致）。
                var ok = await deckManager.DealCardByUidAsync(
                    placement.Uid,
                    placement.GroundSlot,
                    ensureCard: null,
                    skipBusyGuard: false,
                    awaitMove: false,
                    cancellationToken: cancellationToken);
                if (!ok)
                {
                    Debug.LogError(
                        $"[InBattleManager] 就位失败 uid={placement.Uid} slot={placement.GroundSlot}，中止后续发牌。");
                    return;
                }

                dealtAny = true;
                if (i < ring.Count - 1 && dealInterval > 0f)
                {
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(dealInterval),
                        cancellationToken: cancellationToken);
                }
            }

            // 末张飞入播完后再刷数值，避免 SoftAlign/Sync 掐掉轨迹。
            if (dealtAny && moveDuration > 0f)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(moveDuration),
                    cancellationToken: cancellationToken);
            }

            CoreCardPresentationMapper.SyncAllSpawnedCards();
            UpdateAvatarDebugText();
        }

        private void UpdateAvatarDebugText()
        {
            ResolveManagers();
            if (panelRouter == null)
            {
                panelRouter = GetComponent<UiPanelRouter>();
                if (panelRouter == null)
                {
                    panelRouter = FindFirstObjectByType<UiPanelRouter>();
                }
            }

            CoreCardPresentationMapper.UpdateAvatarDebugText(panelRouter);
        }

        private void ResetPresentationSurface()
        {
            ResolveManagers();
            // 先取消交战/手牌异步，再强制清占格与手牌槽，最后统一 Release 视图。
            FieldBattleManagerSingleton.Instance?.CancelBattleWork();
            CardHandManagerSingleton.Instance?.ClearHand();
            deckManager?.ResetToStandby();
            fieldManager?.ClearField(force: true);
            cardManager?.ReleaseAll();
            relicManager?.Clear();
            skillManager?.Clear();
            DescriptionManagerSingleton.TryGetInstance()?.Clear();
            _isBusy = false;
        }

        private void CancelPresentationWork()
        {
            if (_presentationCts == null)
            {
                return;
            }

            _presentationCts.Cancel();
            _presentationCts.Dispose();
            _presentationCts = null;
        }

        private CancellationToken RenewPresentationToken()
        {
            CancelPresentationWork();
            _presentationCts = new CancellationTokenSource();
            return _presentationCts.Token;
        }

        private CancellationToken EnsurePresentationToken()
        {
            if (_presentationCts == null)
            {
                _presentationCts = new CancellationTokenSource();
            }

            return _presentationCts.Token;
        }

        private void SyncContentPanels()
        {
            ResolveManagers();
            relicManager?.SyncFromCore();
            skillManager?.SyncFromCore();
        }

        private void ResolveManagers()
        {
            if (cardManager == null)
            {
                cardManager = CardManagerSingleton.Instance;
            }

            if (deckManager == null)
            {
                deckManager = CardDeckManagerSingleton.Instance;
            }

            if (fieldManager == null)
            {
                fieldManager = GroundFieldManagerSingleton.Instance;
            }

            if (relicManager == null)
            {
                relicManager = RelicManagerSingleton.Instance;
            }

            if (skillManager == null)
            {
                skillManager = PlayerSkillManagerSingleton.Instance;
            }
        }

        private void SubscribeFieldSignal()
        {
            if (_fieldSignalSubscribed || fieldManager == null)
            {
                ResolveManagers();
            }

            if (_fieldSignalSubscribed || fieldManager == null)
            {
                return;
            }

            fieldManager.FieldMaybeClearSignal += OnFieldMaybeClearSignal;
            _fieldSignalSubscribed = true;
        }

        private void UnsubscribeFieldSignal()
        {
            if (!_fieldSignalSubscribed || fieldManager == null)
            {
                return;
            }

            fieldManager.FieldMaybeClearSignal -= OnFieldMaybeClearSignal;
            _fieldSignalSubscribed = false;
        }

        private void OnFieldMaybeClearSignal()
        {
            NotifyPresentationBoardMayBeClear();
        }

        private void RegisterCombatHitSink()
        {
            CombatHitSink.ApplyCombatHit = ApplyCombatHitFromCore;
            CombatHitSink.ResolvePostKillBoard = ResolvePostKillBoardFromCore;
            CombatHitSink.EstimateWillKill = EstimateWillKillFromCore;
            CombatHitSink.SyncCardPresentation = SyncManagedCardPresentation;
            CombatHitSink.SpawnDamageNumber = SpawnDamageNumberAt;
            CombatHitSink.SyncBoardFromCore = SyncBoardOccupancyFromCore;
            CombatHitSink.DrainPostKillBoard = DrainPostKillBoardAsync;
            CombatHitSink.ApplyPickupItem = ApplyPickupItemFromCore;
            CombatHitSink.ApplyClickEmpty = ApplyClickEmptyFromCore;
            CombatHitSink.ApplyUseItem = ApplyUseItemFromCore;
            CombatHitSink.NotifyBattleEnded = OnBattleEndedFromCombat;
            RegisterHandBridge();
        }

        private void UnregisterCombatHitSink()
        {
            if (CombatHitSink.ApplyCombatHit == ApplyCombatHitFromCore)
            {
                CombatHitSink.ApplyCombatHit = null;
            }

            if (CombatHitSink.ResolvePostKillBoard == ResolvePostKillBoardFromCore)
            {
                CombatHitSink.ResolvePostKillBoard = null;
            }

            if (CombatHitSink.EstimateWillKill == EstimateWillKillFromCore)
            {
                CombatHitSink.EstimateWillKill = null;
            }

            if (CombatHitSink.SyncCardPresentation == SyncManagedCardPresentation)
            {
                CombatHitSink.SyncCardPresentation = null;
            }

            if (CombatHitSink.SpawnDamageNumber == SpawnDamageNumberAt)
            {
                CombatHitSink.SpawnDamageNumber = null;
            }

            if (CombatHitSink.SyncBoardFromCore == SyncBoardOccupancyFromCore)
            {
                CombatHitSink.SyncBoardFromCore = null;
            }

            if (CombatHitSink.DrainPostKillBoard == DrainPostKillBoardAsync)
            {
                CombatHitSink.DrainPostKillBoard = null;
            }

            if (CombatHitSink.ApplyPickupItem == ApplyPickupItemFromCore)
            {
                CombatHitSink.ApplyPickupItem = null;
            }

            if (CombatHitSink.ApplyClickEmpty == ApplyClickEmptyFromCore)
            {
                CombatHitSink.ApplyClickEmpty = null;
            }

            if (CombatHitSink.ApplyUseItem == ApplyUseItemFromCore)
            {
                CombatHitSink.ApplyUseItem = null;
            }

            if (CombatHitSink.NotifyBattleEnded == OnBattleEndedFromCombat)
            {
                CombatHitSink.NotifyBattleEnded = null;
            }

            UnregisterHandBridge();
        }

        private void RegisterHandBridge()
        {
            var hand = CardHandManagerSingleton.Instance
                       ?? FindFirstObjectByType<CardHandManagerSingleton>();
            if (hand == null)
            {
                return;
            }

            hand.DragApplyValidator -= ValidateHandDragApplyAsync;
            hand.DragApplyValidator += ValidateHandDragApplyAsync;
        }

        private void UnregisterHandBridge()
        {
            var hand = CardHandManagerSingleton.Instance;
            if (hand == null)
            {
                return;
            }

            hand.DragApplyValidator -= ValidateHandDragApplyAsync;
        }

        private static CombatHitPresentationResult ApplyCombatHitFromCore(int attackerUid, int targetUid)
        {
            var reason = BattleTraceRecorder.ConsumePendingReason("CombatHit");
            var arch = NineGridArchitecture.Current;
            var pipeline = arch.GetSystem<IActionPipelineSystem>();
            var phaseSystem = arch.GetSystem<IPhaseSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            var phaseBefore = phaseSystem.CurrentPhase.ToString();
            BattleTraceCardSnap attackerSnap = null;
            BattleTraceCardSnap targetSnap = null;
            try
            {
                if (BattleTraceRecorder.Enabled)
                {
                    BattleTraceRecorder.BeginSessionIfNeeded();
                    attackerSnap = BattleTraceRecorder.TryCaptureCard(attackerUid);
                    targetSnap = BattleTraceRecorder.TryCaptureCard(targetUid);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[InBattleManager] BattleTrace pre-hit: " + ex.Message);
            }

            var result = phaseSystem.ApplyCombatHit(attackerUid, targetUid);
            var summary = new CombatHitPresentationResult { Accepted = result.Accepted };
            if (!result.Accepted)
            {
                Debug.LogWarning($"[InBattleManager] CombatHit 被拒: {result.Reason}");
                try
                {
                    if (BattleTraceRecorder.Enabled)
                    {
                        var endIndex = pipeline.EventLog.Entries.Count;
                        var events = BattleTraceRecorder.SliceEvents(startIndex, endIndex);
                        BattleTraceRecorder.RecordOp(new BattleTraceOp
                        {
                            opKind = "CombatHit",
                            reason = reason,
                            apiPath = "PhaseSystem.ApplyCombatHit",
                            phaseBefore = phaseBefore,
                            phaseAfter = phaseSystem.CurrentPhase.ToString(),
                            attacker = attackerSnap,
                            target = targetSnap,
                            eventStartIndex = startIndex,
                            eventEndIndex = endIndex,
                            events = events,
                            presentation = new BattleTracePresentation { accepted = false },
                            verdictHints = BattleTraceRecorder.BuildVerdictHints(events, false, false),
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[InBattleManager] BattleTrace reject: " + ex.Message);
                }

                return summary;
            }

            var entries = pipeline.EventLog.Entries;
            var popups = new List<CombatDamagePopup>(4);
            for (var i = startIndex; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type == CoreEventType.DamageDealt && e.Amount > 0 && e.TargetUid > 0)
                {
                    popups.Add(new CombatDamagePopup { TargetUid = e.TargetUid, Amount = e.Amount });
                    if (e.TargetUid == targetUid)
                    {
                        summary.DamageAmount = e.Amount;
                        summary.RemainingHp = e.RemainingHp;
                        summary.RemainingArmor = e.RemainingArmor;
                    }
                }

                if (e.Type == CoreEventType.CardKilled && e.CardUid == targetUid)
                {
                    summary.TargetKilled = true;
                }
            }

            summary.DamagePopups = popups.Count > 0 ? popups.ToArray() : Array.Empty<CombatDamagePopup>();

            if (!summary.TargetKilled
                && arch.GetModel<CardRegistry>().TryGet(targetUid, out var target)
                && (target.Zone.Value == ZoneId.Graveyard || target.Zone.Value == ZoneId.Removed
                    || arch.GetSystem<IStatSystem>().GetEffectiveInt(target, StatId.Hp) <= 0))
            {
                summary.TargetKilled = target.Kind != CardKind.Avatar;
            }

            var phase = phaseSystem.CurrentPhase;
            summary.AvatarDefeated = phase == GamePhase.Defeat;
            summary.NodeClearedOrRewardPhase =
                phase == GamePhase.RewardItemChoice
                || phase == GamePhase.ClearCheck
                || phase == GamePhase.NodeCompleted
                || arch.GetSystem<IDeckSystem>().IsNodeCleared();

            try
            {
                if (BattleTraceRecorder.Enabled)
                {
                    var endIndex = entries.Count;
                    var events = BattleTraceRecorder.SliceEvents(startIndex, endIndex);
                    BattleTraceRecorder.RecordOp(new BattleTraceOp
                    {
                        opKind = "CombatHit",
                        reason = reason,
                        apiPath = "PhaseSystem.ApplyCombatHit",
                        phaseBefore = phaseBefore,
                        phaseAfter = phase.ToString(),
                        attacker = attackerSnap,
                        target = targetSnap,
                        eventStartIndex = startIndex,
                        eventEndIndex = endIndex,
                        events = events,
                        presentation = new BattleTracePresentation
                        {
                            accepted = summary.Accepted,
                            damageAmount = summary.DamageAmount,
                            targetKilled = summary.TargetKilled,
                            avatarDefeated = summary.AvatarDefeated,
                            nodeClearedOrRewardPhase = summary.NodeClearedOrRewardPhase,
                        },
                        verdictHints = BattleTraceRecorder.BuildVerdictHints(
                            events, summary.TargetKilled, summary.AvatarDefeated),
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[InBattleManager] BattleTrace post-hit: " + ex.Message);
            }

            return summary;
        }

        private static PostKillBoardPresentationResult ResolvePostKillBoardFromCore()
        {
            var arch = NineGridArchitecture.Current;
            var pipeline = arch.GetSystem<IActionPipelineSystem>();
            var phaseSystem = arch.GetSystem<IPhaseSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            var phaseBefore = phaseSystem.CurrentPhase.ToString();
            var result = phaseSystem.ResolvePostKillBoard();
            var phase = phaseSystem.CurrentPhase;
            var summary = new PostKillBoardPresentationResult
            {
                Accepted = result.Accepted,
                AvatarDefeated = phase == GamePhase.Defeat,
                NodeClearedOrRewardPhase =
                    phase == GamePhase.RewardItemChoice
                    || phase == GamePhase.ClearCheck
                    || phase == GamePhase.NodeCompleted
                    || arch.GetSystem<IDeckSystem>().IsNodeCleared(),
            };

            if (!result.Accepted)
            {
                Debug.LogWarning($"[InBattleManager] ResolvePostKillBoard 被拒: {result.Reason}");
                summary.Moves = Array.Empty<PostKillCardMove>();
                summary.Deals = Array.Empty<PostKillCardDeal>();
                summary.DamagePopups = Array.Empty<CombatDamagePopup>();
                try
                {
                    if (BattleTraceRecorder.Enabled)
                    {
                        var endIndex = pipeline.EventLog.Entries.Count;
                        var events = BattleTraceRecorder.SliceEvents(startIndex, endIndex);
                        BattleTraceRecorder.RecordOp(new BattleTraceOp
                        {
                            opKind = "PostKillBoard",
                            reason = "PostKillBoard",
                            apiPath = "PhaseSystem.ResolvePostKillBoard",
                            phaseBefore = phaseBefore,
                            phaseAfter = phase.ToString(),
                            eventStartIndex = startIndex,
                            eventEndIndex = endIndex,
                            events = events,
                            presentation = new BattleTracePresentation { accepted = false },
                            verdictHints = BattleTraceRecorder.BuildVerdictHints(events, false, false),
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[InBattleManager] BattleTrace PostKill reject: " + ex.Message);
                }

                return summary;
            }

            FillBoardDeltaFromEventLog(pipeline, startIndex, out var moves, out var deals, out _);
            summary.Moves = moves;
            summary.Deals = deals;
            summary.DamagePopups = CollectDamagePopups(pipeline.EventLog.Entries, startIndex);

            try
            {
                if (BattleTraceRecorder.Enabled)
                {
                    var endIndex = pipeline.EventLog.Entries.Count;
                    var events = BattleTraceRecorder.SliceEvents(startIndex, endIndex);
                    BattleTraceRecorder.RecordOp(new BattleTraceOp
                    {
                        opKind = "PostKillBoard",
                        reason = "PostKillBoard",
                        apiPath = "PhaseSystem.ResolvePostKillBoard",
                        phaseBefore = phaseBefore,
                        phaseAfter = phase.ToString(),
                        eventStartIndex = startIndex,
                        eventEndIndex = endIndex,
                        events = events,
                        presentation = new BattleTracePresentation
                        {
                            accepted = summary.Accepted,
                            avatarDefeated = summary.AvatarDefeated,
                            nodeClearedOrRewardPhase = summary.NodeClearedOrRewardPhase,
                        },
                        verdictHints = BattleTraceRecorder.BuildVerdictHints(
                            events, false, summary.AvatarDefeated),
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[InBattleManager] BattleTrace PostKill: " + ex.Message);
            }

            return summary;
        }

        /// <summary>
        /// 表现缓冲缓释：R1 先补牌再旋转（Deals → Moves），末尾两阶段 Sync 安全网。
        /// </summary>
        private async UniTask DrainPostKillBoardAsync(
            PostKillBoardPresentationResult result,
            CancellationToken cancellationToken)
        {
            if (!result.Accepted)
            {
                return;
            }

            // 生命周期清场会 CancelPresentationWork；未传可取消 token 时挂到局内 CTS。
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                EnsurePresentationToken(),
                cancellationToken.CanBeCanceled ? cancellationToken : CancellationToken.None);
            var ct = linkedCts.Token;

            ResolveManagers();
            if (fieldManager == null || cardManager == null || deckManager == null)
            {
                Debug.LogError("[InBattleManager] DrainPostKillBoard 缺少 Card/Deck/Field 管理器。");
                return;
            }

            // R1：先补牌（CardDealt），再旋转 hop（CardMoved）。
            if (result.Deals != null && result.Deals.Length > 0)
            {
                await DrainDealsAsync(result.Deals, ct);
            }

            if (result.Moves != null && result.Moves.Length > 0)
            {
                await fieldManager.ApplyBoardMovesAndHopAsync(
                    result.Moves,
                    ct,
                    skipBusyGuard: true);
            }

            SoftAlignBoardAnchorsToCore();
            SyncBoardOccupancyFromCore();
            SpawnDamagePopups(result.DamagePopups);
        }

        private static CombatDamagePopup[] CollectDamagePopups(
            IReadOnlyList<CoreGameEvent> entries,
            int startIndex)
        {
            if (entries == null || startIndex >= entries.Count)
            {
                return Array.Empty<CombatDamagePopup>();
            }

            var popups = new List<CombatDamagePopup>(4);
            for (var i = Math.Max(0, startIndex); i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type == CoreEventType.DamageDealt && e.Amount > 0 && e.TargetUid > 0)
                {
                    popups.Add(new CombatDamagePopup { TargetUid = e.TargetUid, Amount = e.Amount });
                }
            }

            return popups.Count > 0 ? popups.ToArray() : Array.Empty<CombatDamagePopup>();
        }

        private void SpawnDamagePopups(CombatDamagePopup[] popups)
        {
            if (popups == null || popups.Length == 0)
            {
                return;
            }

            ResolveManagers();
            for (var i = 0; i < popups.Length; i++)
            {
                var popup = popups[i];
                if (popup.Amount <= 0 || popup.TargetUid <= 0)
                {
                    continue;
                }

                if (cardManager != null
                    && cardManager.TryGet(popup.TargetUid, out var view)
                    && view?.Transform != null)
                {
                    SpawnDamageNumberAt(view.Transform.position, popup.Amount);
                }
            }
        }

        private async UniTask DrainDealsAsync(PostKillCardDeal[] deals, CancellationToken ct)
        {
            var dealInterval = deckManager.LayoutSettings != null
                ? deckManager.LayoutSettings.dealInterval
                : 0.05f;
            var moveDuration = deckManager.LayoutSettings != null
                ? deckManager.LayoutSettings.moveDuration
                : 0.28f;
            var dealtAny = false;

            for (var i = 0; i < deals.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                var deal = deals[i];
                if (deal.Uid <= 0 || deal.Slot <= 0)
                {
                    continue;
                }

                if (fieldManager.TryGetCardAt(deal.Slot, out var already)
                    && already != null
                    && already.Uid == deal.Uid)
                {
                    CoreCardPresentationMapper.ApplyToManagedCard(already);
                    continue;
                }

                var hand = CardHandManagerSingleton.Instance;
                if (hand != null && hand.ContainsUid(deal.Uid))
                {
                    Debug.LogWarning(
                        $"[InBattleManager] Drain 跳过补牌：uid={deal.Uid} 已在手牌，目标格 {deal.Slot}");
                    continue;
                }

                // 缺牌时先入组再发，始终走卡组完整缓动；禁止瞬移落锚兜底。
                var ensureCard = ResolveOrSpawnDeckCardForDeal(deal);
                var ok = await deckManager.DealCardByUidAsync(
                    deal.Uid,
                    deal.Slot,
                    ensureCard: ensureCard,
                    skipBusyGuard: true,
                    awaitMove: false,
                    cancellationToken: ct);
                if (ok)
                {
                    dealtAny = true;
                    if (cardManager.TryGet(deal.Uid, out var dealt))
                    {
                        CoreCardPresentationMapper.ApplyToManagedCard(dealt);
                    }
                }
                else
                {
                    Debug.LogWarning(
                        $"[InBattleManager] 补牌发牌失败 uid={deal.Uid} slot={deal.Slot}，留给安全网对齐。");
                }

                if (i < deals.Length - 1 && dealInterval > 0f)
                {
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(dealInterval),
                        cancellationToken: ct);
                }
            }

            // 末张飞入播完后再 SoftAlign/Sync，避免 KillMotion 掐掉轨迹。
            if (dealtAny && moveDuration > 0f)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(moveDuration),
                    cancellationToken: ct);
            }
        }

        /// <summary>
        /// 为补牌准备可入组的视图：已在卡组则返回 null；否则复用游离视图或 Spawn 新视图（CardDeckMode）。
        /// </summary>
        private ManagedCard ResolveOrSpawnDeckCardForDeal(PostKillCardDeal deal)
        {
            ResolveManagers();
            if (deckManager != null && deckManager.ContainsUid(deal.Uid))
            {
                return null;
            }

            if (cardManager != null && cardManager.TryGet(deal.Uid, out var existing) && existing != null)
            {
                return existing;
            }

            if (cardManager == null)
            {
                return null;
            }

            var defId = string.IsNullOrEmpty(deal.DefId)
                ? CardManagerSingleton.StandardDefId
                : deal.DefId;
            var view = cardManager.SpawnView(deal.Uid, defId, initialMode: CardDisplayMode.CardDeckMode);
            if (view != null)
            {
                CoreCardPresentationMapper.ApplyToManagedCard(view);
            }

            return view;
        }

        private static PickupItemPresentationResult ApplyPickupItemFromCore(int groundSlot)
        {
            var arch = NineGridArchitecture.Current;
            var pipeline = arch.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            var result = arch.GetSystem<IPhaseSystem>().ApplyPickupItem(SlotId.Board(groundSlot));
            var summary = new PickupItemPresentationResult { Accepted = result.Accepted };
            if (!result.Accepted)
            {
                Debug.LogWarning($"[InBattleManager] PickupItem 被拒: {result.Reason}");
                summary.Moves = Array.Empty<PostKillCardMove>();
                summary.Deals = Array.Empty<PostKillCardDeal>();
                return summary;
            }

            FillBoardDeltaFromEventLog(pipeline, startIndex, out var moves, out var deals, out var pickedUid);
            summary.CardUid = pickedUid;
            summary.Moves = moves;
            summary.Deals = deals;

            if (pickedUid > 0
                && arch.GetModel<CardRegistry>().TryGet(pickedUid, out var card))
            {
                summary.AcquiredToHand = card.Zone.Value == ZoneId.ItemSlots;
                summary.RemovedWithoutHand = card.Zone.Value == ZoneId.Removed;
            }

            var phase = arch.GetSystem<IPhaseSystem>().CurrentPhase;
            summary.NodeClearedOrRewardPhase =
                phase == GamePhase.RewardItemChoice
                || phase == GamePhase.ClearCheck
                || phase == GamePhase.NodeCompleted
                || arch.GetSystem<IDeckSystem>().IsNodeCleared();
            return summary;
        }

        private static PostKillBoardPresentationResult ApplyClickEmptyFromCore(int groundSlot)
        {
            var arch = NineGridArchitecture.Current;
            var pipeline = arch.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            var result = arch.GetSystem<IPhaseSystem>().ClickEmpty(SlotId.Board(groundSlot));
            var phase = arch.GetSystem<IPhaseSystem>().CurrentPhase;
            var summary = new PostKillBoardPresentationResult
            {
                Accepted = result.Accepted,
                AvatarDefeated = phase == GamePhase.Defeat,
                NodeClearedOrRewardPhase =
                    phase == GamePhase.RewardItemChoice
                    || phase == GamePhase.ClearCheck
                    || phase == GamePhase.NodeCompleted
                    || arch.GetSystem<IDeckSystem>().IsNodeCleared(),
            };

            if (!result.Accepted)
            {
                Debug.LogWarning($"[InBattleManager] ClickEmpty 被拒: {result.Reason}");
                summary.Moves = Array.Empty<PostKillCardMove>();
                summary.Deals = Array.Empty<PostKillCardDeal>();
                summary.DamagePopups = Array.Empty<CombatDamagePopup>();
                return summary;
            }

            FillBoardDeltaFromEventLog(pipeline, startIndex, out var moves, out var deals, out _);
            summary.Moves = moves;
            summary.Deals = deals;
            summary.DamagePopups = CollectDamagePopups(pipeline.EventLog.Entries, startIndex);
            return summary;
        }

        private static UseItemPresentationResult ApplyUseItemFromCore(
            int itemUid,
            int? targetCardUid,
            string selectedOption = null)
        {
            var arch = NineGridArchitecture.Current;
            var pipeline = arch.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            int[] selected = null;
            if (targetCardUid.HasValue && targetCardUid.Value > 0)
            {
                selected = new[] { targetCardUid.Value };
            }

            var result = arch.GetSystem<IPhaseSystem>().ApplyUseItem(itemUid, selected, selectedOption);
            var summary = new UseItemPresentationResult { Accepted = result.Accepted };
            if (!result.Accepted)
            {
                Debug.LogWarning($"[InBattleManager] UseItem 被拒: {result.Reason}");
                return summary;
            }

            var killedUids = new List<int>(2);
            var entries = pipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type == CoreEventType.CardKilled && e.CardUid > 0)
                {
                    summary.TargetKilled = true;
                    if (!killedUids.Contains(e.CardUid))
                    {
                        killedUids.Add(e.CardUid);
                    }
                }
            }

            // 飞刀等：EventLog 偶发漏 CardKilled 时，用选定目标的 Graveyard/Removed/Hp 兜底。
            if (!summary.TargetKilled
                && targetCardUid.HasValue
                && targetCardUid.Value > 0
                && arch.GetModel<CardRegistry>().TryGet(targetCardUid.Value, out var target)
                && (target.Zone.Value == ZoneId.Graveyard
                    || target.Zone.Value == ZoneId.Removed
                    || arch.GetSystem<IStatSystem>().GetEffectiveInt(target, StatId.Hp) <= 0)
                && target.Kind != CardKind.Avatar)
            {
                summary.TargetKilled = true;
                killedUids.Add(targetCardUid.Value);
            }

            summary.KilledTargetUids = killedUids.Count > 0
                ? killedUids.ToArray()
                : Array.Empty<int>();

            var phase = arch.GetSystem<IPhaseSystem>().CurrentPhase;
            summary.AvatarDefeated = phase == GamePhase.Defeat;
            // 局内宝箱：PendingChoice + InteractionLoop；通关奖励：RewardItemChoice。
            summary.RewardChoicePending =
                arch.GetModel<PendingChoiceModel>().Kind.Value == PendingChoiceKind.Reward;
            summary.NodeClearedOrRewardPhase =
                phase == GamePhase.RewardItemChoice
                || phase == GamePhase.ClearCheck
                || phase == GamePhase.NodeCompleted
                || phase == GamePhase.RoomChoice
                || arch.GetSystem<IDeckSystem>().IsNodeCleared();

            FillBoardDeltaFromEventLog(pipeline, startIndex, out var moves, out var deals, out _);
            if ((moves != null && moves.Length > 0) || (deals != null && deals.Length > 0) || summary.TargetKilled)
            {
                // DamagePopups 留空：PresentUseItemEffectsAsync 已用 SpawnRecentDamageNumbers 覆盖本段伤害。
                summary.PostKillBoard = new PostKillBoardPresentationResult
                {
                    Accepted = true,
                    Moves = moves ?? Array.Empty<PostKillCardMove>(),
                    Deals = deals ?? Array.Empty<PostKillCardDeal>(),
                    DamagePopups = Array.Empty<CombatDamagePopup>(),
                    NodeClearedOrRewardPhase = summary.NodeClearedOrRewardPhase,
                    AvatarDefeated = summary.AvatarDefeated,
                };
            }

            return summary;
        }

        private static void FillBoardDeltaFromEventLog(
            IActionPipelineSystem pipeline,
            int startIndex,
            out PostKillCardMove[] moves,
            out PostKillCardDeal[] deals,
            out int pickedUid)
        {
            var moveList = new List<PostKillCardMove>(8);
            var dealList = new List<PostKillCardDeal>(8);
            pickedUid = 0;
            var registry = NineGridArchitecture.Current.GetModel<CardRegistry>();
            var entries = pipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type == CoreEventType.ItemPicked && e.CardUid > 0 && pickedUid == 0)
                {
                    pickedUid = e.CardUid;
                }

                if (e.Type == CoreEventType.CardMoved
                    && e.CardUid > 0
                    && e.FromSlot.IsBoardSlot
                    && e.ToSlot.IsBoardSlot)
                {
                    moveList.Add(new PostKillCardMove
                    {
                        Uid = e.CardUid,
                        FromSlot = e.FromSlot.Index,
                        ToSlot = e.ToSlot.Index,
                    });
                }
                else if (e.Type == CoreEventType.CardDealt
                         && e.CardUid > 0
                         && e.ToSlot.IsBoardSlot)
                {
                    var defId = string.Empty;
                    if (registry.TryGet(e.CardUid, out var coreCard))
                    {
                        defId = coreCard.DefId;
                    }

                    dealList.Add(new PostKillCardDeal
                    {
                        Uid = e.CardUid,
                        Slot = e.ToSlot.Index,
                        DefId = defId,
                    });
                }
            }

            moves = moveList.ToArray();
            deals = dealList.ToArray();
        }

        private async UniTask<bool> ValidateHandDragApplyAsync(ManagedCard card, int? targetGroundSlot)
        {
            if (card == null)
            {
                return false;
            }

            if (CombatHitSink.ChoiceOverlayActive)
            {
                return false;
            }

            ResolveManagers();
            int? targetUid = null;
            ManagedCard targetCard = null;
            if (targetGroundSlot.HasValue
                && fieldManager != null
                && fieldManager.TryGetCardAt(targetGroundSlot.Value, out targetCard)
                && targetCard != null)
            {
                if (targetCard.CoreKind != CardPresentationKind.Monster
                    && HelpCardNeedsMonsterTarget(card.DefId))
                {
                    return false;
                }

                targetUid = targetCard.Uid;
            }
            else if (HelpCardNeedsMonsterTarget(card.DefId))
            {
                // 飞刀等：必须落到怪物上方，否则回手。
                return false;
            }

            string selectedOption = null;
            if (IsStatBoostCard(card.DefId))
            {
                // 先藏起本体，避免三选一期间手牌卡仍停在拖放位置。
                HideHandCardForChoice(card);
                selectedOption = await PresentStatBoostChoiceAsync();
                if (string.IsNullOrEmpty(selectedOption))
                {
                    RestoreHandCardAfterChoiceCancel(card);
                    return false;
                }
            }

            var useResult = ApplyUseItemFromCore(card.Uid, targetUid, selectedOption);
            if (!useResult.Accepted)
            {
                if (IsStatBoostCard(card.DefId))
                {
                    RestoreHandCardAfterChoiceCancel(card);
                }

                return false;
            }

            PresentUseItemEffectsAsync(useResult).Forget();
            return true;
        }

        private static void HideHandCardForChoice(ManagedCard card)
        {
            if (card?.Transform == null)
            {
                return;
            }

            CardDeckTween.KillMotion(card.Transform);
            card.Transform.localScale = Vector3.zero;
        }

        private static void RestoreHandCardAfterChoiceCancel(ManagedCard card)
        {
            if (card?.Transform == null)
            {
                return;
            }

            card.Transform.localScale = Vector3.one;
        }

        private static bool IsStatBoostCard(string defId)
        {
            return !string.IsNullOrEmpty(defId)
                   && string.Equals(defId, StatBoostCardDefId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HelpCardNeedsMonsterTarget(string defId)
        {
            if (string.IsNullOrEmpty(defId))
            {
                return false;
            }

            return defId.IndexOf("throwing_knife", StringComparison.OrdinalIgnoreCase) >= 0
                   || defId.IndexOf("fireball", StringComparison.OrdinalIgnoreCase) >= 0
                   || defId.IndexOf("impact_tutorial", StringComparison.OrdinalIgnoreCase) >= 0
                   || defId.IndexOf("shield_bash", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private async UniTaskVoid PresentUseItemEffectsAsync(UseItemPresentationResult useResult)
        {
            try
            {
                var ct = EnsurePresentationToken();
                CoreCardPresentationMapper.SyncAllSpawnedCards();
                SpawnRecentDamageNumbers();

                // 击杀必须先 Vacate 尸体，再 Drain/Sync；否则 Register 会静默挤占格留下钉住幽灵。
                if (useResult.TargetKilled)
                {
                    BeginUseItemLethalVictims(useResult, ct);
                }

                if (useResult.PostKillBoard.Accepted
                    && ((useResult.PostKillBoard.Moves != null && useResult.PostKillBoard.Moves.Length > 0)
                        || (useResult.PostKillBoard.Deals != null && useResult.PostKillBoard.Deals.Length > 0)))
                {
                    await DrainPostKillBoardAsync(useResult.PostKillBoard, ct);
                }
                else
                {
                    SoftAlignBoardAnchorsToCore();
                    SyncBoardOccupancyFromCore();
                }

                if (useResult.AvatarDefeated)
                {
                    CombatHitSink.RequestBattleEnded(victory: false);
                    return;
                }

                if (useResult.RewardChoicePending)
                {
                    await PresentRewardChoiceFromCoreAsync();
                    return;
                }

                if (useResult.NodeClearedOrRewardPhase)
                {
                    CombatHitSink.RequestBattleEnded(victory: true);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// UseItem 击杀：对齐 FieldBattle 卸尸（MarkFieldDead → Vacate → 异步 Release）。
        /// </summary>
        private void BeginUseItemLethalVictims(UseItemPresentationResult useResult, CancellationToken cancellationToken)
        {
            ResolveManagers();
            var battle = FieldBattleManagerSingleton.Instance;
            if (battle == null || cardManager == null)
            {
                Debug.LogWarning("[InBattleManager] UseItem 击杀卸尸缺少 FieldBattle/CardManager。");
                return;
            }

            var killed = useResult.KilledTargetUids;
            if (killed == null || killed.Length == 0)
            {
                return;
            }

            for (var i = 0; i < killed.Length; i++)
            {
                var uid = killed[i];
                if (uid <= 0 || !cardManager.TryGet(uid, out var victim) || victim == null)
                {
                    continue;
                }

                battle.TryBeginLethalVictimPresentation(victim, cancellationToken);
            }
        }

        private void SpawnRecentDamageNumbers()
        {
            // 最近一次 UseItem/Combat 的 DamageDealt：对仍在场上的目标飘字。
            var arch = NineGridArchitecture.Current;
            var entries = arch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            var start = Math.Max(0, entries.Count - 32);
            for (var i = start; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type != CoreEventType.DamageDealt || e.Amount <= 0 || e.TargetUid <= 0)
                {
                    continue;
                }

                if (cardManager != null
                    && cardManager.TryGet(e.TargetUid, out var view)
                    && view?.Transform != null)
                {
                    SpawnDamageNumberAt(view.Transform.position, e.Amount);
                }
            }
        }

        private async UniTask PresentRewardChoiceFromCoreAsync()
        {
            var selector = SelectorManagerSingleton.Instance;
            if (selector == null)
            {
                Debug.LogWarning("[InBattleManager] 奖励相位但无 SelectorManager。");
                return;
            }

            var arch = NineGridArchitecture.Current;
            var pending = arch.GetModel<PendingChoiceModel>();
            var options = pending.RewardOptions;
            if (options == null || options.Count == 0)
            {
                Debug.LogWarning("[InBattleManager] 奖励选项为空。");
                return;
            }

            var defIds = new string[options.Count];
            for (var i = 0; i < options.Count; i++)
            {
                defIds[i] = options[i].DefId;
            }

            CombatHitSink.ChoiceOverlayActive = true;
            try
            {
                // 点选即推进 Core；退场动画（约 5s 掉落）在后台播，不再锁输入。
                var pick = await WaitBouncePickAsync(selector, defIds, allowEscapeSkip: true);
                if (pick.Cancelled)
                {
                    return;
                }

                var pipeline = arch.GetSystem<IActionPipelineSystem>();
                var startIndex = pipeline.EventLog.Entries.Count;
                var phaseSystem = arch.GetSystem<IPhaseSystem>();
                CoreCommandResult result;
                if (pick.SkipRequested || pick.Index < 0)
                {
                    result = phaseSystem.SkipHelpChoice();
                    if (!result.Accepted)
                    {
                        Debug.LogWarning($"[InBattleManager] SkipHelpChoice 被拒: {result.Reason}");
                        return;
                    }
                }
                else
                {
                    result = phaseSystem.SelectReward(pick.Index);
                    if (!result.Accepted)
                    {
                        Debug.LogWarning($"[InBattleManager] SelectReward 被拒: {result.Reason}");
                        return;
                    }
                }

                // 选完立刻解锁战场；盘面/遗物同步不阻塞输入。
                CombatHitSink.ChoiceOverlayActive = false;

                FillBoardDeltaFromEventLog(pipeline, startIndex, out var moves, out var deals, out _);
                var phase = phaseSystem.CurrentPhase;
                var boardDelta = new PostKillBoardPresentationResult
                {
                    Accepted = true,
                    Moves = moves ?? Array.Empty<PostKillCardMove>(),
                    Deals = deals ?? Array.Empty<PostKillCardDeal>(),
                    DamagePopups = Array.Empty<CombatDamagePopup>(),
                    NodeClearedOrRewardPhase =
                        phase == GamePhase.RewardItemChoice
                        || phase == GamePhase.ClearCheck
                        || phase == GamePhase.NodeCompleted
                        || phase == GamePhase.RoomChoice
                        || arch.GetSystem<IDeckSystem>().IsNodeCleared(),
                    AvatarDefeated = phase == GamePhase.Defeat,
                };

                if ((boardDelta.Moves != null && boardDelta.Moves.Length > 0)
                    || (boardDelta.Deals != null && boardDelta.Deals.Length > 0))
                {
                    await DrainPostKillBoardAsync(boardDelta, EnsurePresentationToken());
                }
                else
                {
                    CoreCardPresentationMapper.SyncAllSpawnedCards();
                    SoftAlignBoardAnchorsToCore();
                    SyncBoardOccupancyFromCore();
                }

                if (relicManager != null)
                {
                    relicManager.SyncFromCore();
                }

                if (boardDelta.AvatarDefeated)
                {
                    CombatHitSink.RequestBattleEnded(victory: false);
                    return;
                }

                // 通关奖励选完后进入 RoomChoice / NodeCompleted：交主循环；局内宝箱回 InteractionLoop。
                if (phase == GamePhase.RoomChoice
                    || phase == GamePhase.NodeCompleted
                    || phase == GamePhase.Victory)
                {
                    CombatHitSink.RequestBattleEnded(victory: true);
                }
            }
            finally
            {
                CombatHitSink.ChoiceOverlayActive = false;
            }
        }

        /// <summary>
        /// 属性提升：UseItem 前 Bounce 三选一，回传 Attack / Armor / Hp。
        /// </summary>
        private async UniTask<string> PresentStatBoostChoiceAsync()
        {
            var selector = SelectorManagerSingleton.Instance;
            if (selector == null)
            {
                Debug.LogWarning("[InBattleManager] 属性提升选择缺少 SelectorManager。");
                return null;
            }

            CombatHitSink.ChoiceOverlayActive = true;
            try
            {
                var pick = await WaitBouncePickAsync(selector, StatBoostOptions, allowEscapeSkip: false);
                if (pick.Cancelled || pick.Index < 0 || pick.Index >= StatBoostOptions.Length)
                {
                    return null;
                }

                return StatBoostOptions[pick.Index];
            }
            finally
            {
                CombatHitSink.ChoiceOverlayActive = false;
            }
        }

        private readonly struct BouncePickResult
        {
            public readonly int Index;
            public readonly bool SkipRequested;
            public readonly bool Cancelled;

            public BouncePickResult(int index, bool skipRequested, bool cancelled)
            {
                Index = index;
                SkipRequested = skipRequested;
                Cancelled = cancelled;
            }
        }

        /// <summary>
        /// 等玩家点选（onPicked），不等退场动画（onFinished），避免 fallDuration≈5s 锁死输入。
        /// </summary>
        private static async UniTask<BouncePickResult> WaitBouncePickAsync(
            SelectorManagerSingleton selector,
            IReadOnlyList<string> optionDefIds,
            bool allowEscapeSkip)
        {
            var picked = -1;
            var pickedDone = false;
            selector.BeginBounceChoice(
                optionDefIds,
                (index, _) =>
                {
                    picked = index;
                    pickedDone = true;
                });

            while (!pickedDone)
            {
                if (allowEscapeSkip && Input.GetKeyDown(KeyCode.Escape))
                {
                    selector.HideChoice();
                    return new BouncePickResult(-1, skipRequested: true, cancelled: false);
                }

                await UniTask.Yield();
            }

            return new BouncePickResult(picked, skipRequested: false, cancelled: false);
        }

        /// <summary>
        /// 占格已与 Core 一致时，把 Transform 软对齐到锚点（不 Release）。
        /// </summary>
        private void SoftAlignBoardAnchorsToCore()
        {
            ResolveManagers();
            if (fieldManager == null || cardManager == null)
            {
                return;
            }

            var board = NineGridArchitecture.Current.GetModel<BoardModel>();
            var avatarUid = board.AvatarUid.Value;
            for (var slot = SlotId.MinBoardIndex; slot <= SlotId.MaxBoardIndex; slot++)
            {
                if (slot == GroundSlotTopology.AvatarReservedSlot)
                {
                    continue;
                }

                var uid = board.GetCardUid(SlotId.Board(slot));
                if (uid <= 0 || uid == avatarUid)
                {
                    continue;
                }

                if (!fieldManager.TryGetCardAt(slot, out var card)
                    || card?.Transform == null
                    || card.Uid != uid)
                {
                    continue;
                }

                var anchor = fieldManager.GetGroundAnchor(slot);
                if (anchor == null)
                {
                    continue;
                }

                if ((card.Transform.position - anchor.position).sqrMagnitude > 0.0001f)
                {
                    CardDeckTween.KillMotion(card.Transform);
                    card.Transform.position = anchor.position;
                    cardManager.RefreshDisplayMode(card);
                }
            }
        }

        private static bool EstimateWillKillFromCore(int attackerUid, int targetUid)
        {
            var arch = NineGridArchitecture.Current;
            var registry = arch.GetModel<CardRegistry>();
            if (!registry.TryGet(attackerUid, out var attacker) || !registry.TryGet(targetUid, out var target))
            {
                return false;
            }

            var stats = arch.GetSystem<IStatSystem>();
            var attack = Math.Max(0, stats.GetEffectiveInt(attacker, StatId.Attack));
            if (attacker.Kind == CardKind.Monster)
            {
                attack += (int)Math.Round(stats.EvaluateRule(RuleId.EnemyAttackDelta, 0f, stats.CreateContext(attacker)));
            }

            var armor = Math.Max(0, stats.GetEffectiveInt(target, StatId.Armor));
            var hp = Math.Max(0, stats.GetEffectiveInt(target, StatId.Hp));
            var hpLoss = Math.Min(hp, Math.Max(0, attack - armor));
            return hp - hpLoss <= 0;
        }

        private static void SyncManagedCardPresentation(ManagedCard card)
        {
            CoreCardPresentationMapper.ApplyToManagedCard(card);
        }

        private static void SpawnDamageNumberAt(Vector3 worldPosition, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            if (DamageNumberManagerSingleton.TryGetInstance(out var manager))
            {
                manager.SpawnAtWorldPosition(worldPosition, amount);
            }
        }

        /// <summary>
        /// 安全网：两阶段对齐 Core 占格。
        /// 1) 卸下所有与 Core 不一致的占格（含仍在盘面但错位的 uid，避免目标格占用阻塞迁移）；
        /// 2) 按 Core 落位（已有视图迁/放锚，缺失则 Spawn）。
        /// </summary>
        private void SyncBoardOccupancyFromCore()
        {
            ResolveManagers();
            if (cardManager == null || fieldManager == null)
            {
                return;
            }

            var arch = NineGridArchitecture.Current;
            var board = arch.GetModel<BoardModel>();
            var registry = arch.GetModel<CardRegistry>();
            var avatarUid = board.AvatarUid.Value;
            var hand = CardHandManagerSingleton.Instance;

            // Phase 1：卸下与 Core 不一致的占格（错位也先卸，再统一落位）。
            var snapshot = fieldManager.GetSnapshot();
            for (var i = 0; i < snapshot.Slots.Length; i++)
            {
                var occ = snapshot.Slots[i];
                if (occ.IsEmpty || occ.Uid == avatarUid || occ.IsAvatarReserved)
                {
                    continue;
                }

                var coreSlotUid = board.GetCardUid(SlotId.Board(occ.Slot));
                if (coreSlotUid == occ.Uid)
                {
                    continue;
                }

                // 手牌视图绝不能被 Sync 拽回场地；仅清场地占格。
                if (hand != null && hand.ContainsUid(occ.Uid))
                {
                    fieldManager.ClearSlotOccupancy(occ.Slot, skipBusyGuard: true);
                    continue;
                }

                var stillOnBoard = false;
                for (var s = SlotId.MinBoardIndex; s <= SlotId.MaxBoardIndex; s++)
                {
                    if (board.GetCardUid(SlotId.Board(s)) == occ.Uid)
                    {
                        stillOnBoard = true;
                        break;
                    }
                }

                if (stillOnBoard)
                {
                    // 错位：只清占格，保留视图供 Phase 2 落锚。
                    fieldManager.ClearSlotOccupancy(occ.Slot, skipBusyGuard: true);
                }
                else
                {
                    fieldManager.RequestRemoveFromField(
                        occ.Uid,
                        animate: false,
                        skipBusyGuard: true,
                        startExplore: false);
                }
            }

            // Phase 2：按 Core 落位。
            for (var slot = SlotId.MinBoardIndex; slot <= SlotId.MaxBoardIndex; slot++)
            {
                if (slot == GroundSlotTopology.AvatarReservedSlot)
                {
                    continue;
                }

                var uid = board.GetCardUid(SlotId.Board(slot));
                if (uid <= 0 || uid == avatarUid)
                {
                    continue;
                }

                if (hand != null && hand.ContainsUid(uid))
                {
                    Debug.LogWarning(
                        $"[InBattleManager] Sync 跳过：uid={uid} 在手牌，Core 却要求场地格 {slot}");
                    continue;
                }

                if (fieldManager.TryGetCardAt(slot, out var existing) && existing != null && existing.Uid == uid)
                {
                    CoreCardPresentationMapper.ApplyToManagedCard(existing);
                    var anchor = fieldManager.GetGroundAnchor(slot);
                    if (existing.Transform != null
                        && anchor != null
                        && (existing.Transform.position - anchor.position).sqrMagnitude > 0.0001f)
                    {
                        CardDeckTween.KillMotion(existing.Transform);
                        existing.Transform.position = anchor.position;
                        cardManager.RefreshDisplayMode(existing);
                    }

                    continue;
                }

                if (!registry.TryGet(uid, out var coreCard))
                {
                    continue;
                }

                if (cardManager.TryGet(uid, out var view) && view != null)
                {
                    CoreCardPresentationMapper.ApplyToManagedCard(view);
                    if (fieldManager.TryGetSlotOf(view.Uid, out var currentSlot))
                    {
                        if (currentSlot != slot)
                        {
                            fieldManager.ClearSlotOccupancy(currentSlot, skipBusyGuard: true);
                            fieldManager.RequestPlaceCardAtAnchor(
                                slot,
                                view,
                                skipBusyGuard: true,
                                snapToAnchor: true);
                        }
                    }
                    else
                    {
                        fieldManager.RequestPlaceCardAtAnchor(
                            slot,
                            view,
                            skipBusyGuard: true,
                            snapToAnchor: true);
                    }

                    continue;
                }

                // 视图缺失：Spawn 后必须落锚点（不再只登记占格）。
                view = cardManager.SpawnView(uid, coreCard.DefId, initialMode: CardDisplayMode.GroundCardMode);
                if (view == null)
                {
                    continue;
                }

                CoreCardPresentationMapper.ApplyToManagedCard(view);
                fieldManager.RequestPlaceCardAtAnchor(
                    slot,
                    view,
                    skipBusyGuard: true,
                    snapToAnchor: true);
            }

            SweepOrphanCardViews(avatarUid, hand);
            CoreCardPresentationMapper.SyncAllSpawnedCards();
        }

        /// <summary>
        /// 清扫已不在手牌/卡组/场地占格、且 Core 为 Graveyard/Removed（或 registry 无）的游离视图。
        /// 堵住「Register 挤占后 Sync 只扫占格表」漏掉的尸体钉住。
        /// </summary>
        private void SweepOrphanCardViews(int avatarUid, CardHandManagerSingleton hand)
        {
            ResolveManagers();
            if (cardManager == null || fieldManager == null)
            {
                return;
            }

            var registry = NineGridArchitecture.Current.GetModel<CardRegistry>();
            var deck = deckManager;
            var toRelease = new List<int>(4);

            foreach (var pair in cardManager.CardsByUid)
            {
                var uid = pair.Key;
                var view = pair.Value;
                if (uid <= 0 || uid == avatarUid || view == null)
                {
                    continue;
                }

                // 打出消失 / 拖拽中：生命周期由手牌路径负责，勿抢 Release。
                if (view.DisplayMode == CardDisplayMode.RemovedMode
                    || view.DisplayMode == CardDisplayMode.DragCardMode)
                {
                    continue;
                }

                if (hand != null && hand.ContainsUid(uid))
                {
                    continue;
                }

                if (deck != null && deck.ContainsUid(uid))
                {
                    continue;
                }

                if (fieldManager.TryGetSlotOf(uid, out _))
                {
                    continue;
                }

                if (registry.TryGet(uid, out var coreCard))
                {
                    if (coreCard.Zone.Value != ZoneId.Graveyard
                        && coreCard.Zone.Value != ZoneId.Removed)
                    {
                        continue;
                    }
                }

                toRelease.Add(uid);
            }

            for (var i = 0; i < toRelease.Count; i++)
            {
                var uid = toRelease[i];
                Debug.LogWarning(
                    $"[InBattleManager] 清扫游离卡视图 uid={uid}（Core 已离场且不在手牌/卡组/占格）。");
                cardManager.Release(uid);
            }
        }

        private static void OnBattleEndedFromCombat(bool victory)
        {
            var loop = MainGameLoopManagerSingleton.Instance;
            if (loop == null)
            {
                Debug.LogWarning("[InBattleManager] 战斗结束但未找到 MainGameLoop。");
                return;
            }

            if (victory)
            {
                loop.NotifyBattleVictory();
            }
            else
            {
                loop.NotifyBattleDefeat();
            }
        }
    }
}
