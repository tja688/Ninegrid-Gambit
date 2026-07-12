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
        private const int BoardSelectLockWaitMs = 3000;
        private static readonly string[] StatBoostOptions = { "Attack", "Armor", "Hp" };

        private bool _isBusy;
        private bool _settlementRaised;
        private bool _fieldSignalSubscribed;
        private bool _drainInFlight;
        private CancellationTokenSource _presentationCts;
        private int _nodeEventLogStart;

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
        /// DevTest：强制判定本节点胜利，写入通关奖励相位并触发结算推进（纯流程测试）。
        /// </summary>
        public bool TryCheatForceNodeVictory()
        {
            if (_settlementRaised)
            {
                Debug.LogWarning("[InBattleManager] 强制胜利跳过：本节点已结算。");
                return false;
            }

            var arch = NineGridArchitecture.Current;
            var phase = arch.GetSystem<IPhaseSystem>().CurrentPhase;
            if (phase == GamePhase.RewardItemChoice)
            {
                Debug.Log("[InBattleManager] DevTest 强制胜利：已在奖励相位，直接推进结算。");
                RaiseSettlementReady();
                return true;
            }

            if (phase != GamePhase.InteractionLoop)
            {
                Debug.LogWarning($"[InBattleManager] 强制胜利失败：phase={phase}（需 InteractionLoop）。");
                return false;
            }

            CheatMakeNodeCleared(arch);

            FieldBattleManagerSingleton.Instance?.CancelBattleWork();
            CombatHitSink.ForceEndPresentationLock("CheatForceNodeVictory");
            CancelPresentationWork();

            var pipeline = arch.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.ClearCheck));
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.NodeCompleted));
            pipeline.Enqueue(new NodeCompletedAction());
            pipeline.RunToCompletion();
            // 与正常通关路径一致：进入奖励相位前结算残留帮助卡。
            arch.GetSystem<IEconomySystem>().SettleUnusedHelpCards();
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.RewardItemChoice));
            pipeline.Enqueue(new OfferRewardChoiceAction("help.choice", 3));
            pipeline.RunToCompletion();

            Debug.Log("[InBattleManager] DevTest 强制节点胜利 → RewardItemChoice");
            RaiseSettlementReady();
            return true;
        }

        private void CheatMakeNodeCleared(IArchitecture arch)
        {
            var registry = arch.GetModel<CardRegistry>();
            var board = arch.GetModel<BoardModel>();
            var deck = arch.GetModel<DeckModel>();

            ResolveManagers();
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                var uid = board.GetCardUid(slot);
                if (uid <= 0
                    || !registry.TryGet(uid, out var card)
                    || card.Kind != CardKind.Monster)
                {
                    continue;
                }

                board.ClearSlot(slot);
                card.Zone.Value = ZoneId.None;
                card.Slot.Value = SlotId.None;
                fieldManager?.RequestRemoveFromField(uid, animate: false, skipBusyGuard: true);
            }

            var drawUids = new List<int>(deck.DrawPileUids);
            for (var i = 0; i < drawUids.Count; i++)
            {
                if (registry.TryGet(drawUids[i], out var card))
                {
                    deck.RemoveCard(card);
                }
                else
                {
                    deck.RemoveUid(drawUids[i]);
                }
            }

            var enemyPoolUids = new List<int>(deck.EnemyCardPoolUids);
            for (var i = 0; i < enemyPoolUids.Count; i++)
            {
                if (registry.TryGet(enemyPoolUids[i], out var card))
                {
                    deck.RemoveCard(card);
                }
                else
                {
                    deck.RemoveUid(enemyPoolUids[i]);
                }
            }
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
            _nodeEventLogStart = 0;
            try
            {
                BattleTraceRecorder.Clear();
                DiagTraceShared.EnsureSessionIdentity(snapshot.Seed);
                BattleTraceRecorder.BeginSessionIfNeeded(snapshot.Seed);
                // FlowTrace 不清空：同局 StartRun 等流程事件保留；失败重开由 BeginRun.RotateSessionForNewRun 处理。
                FlowTraceRecorder.BeginSessionIfNeeded(snapshot.Seed);
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
            CombatHitSink.ForceEndPresentationLock("ClearPresentationSurface");
            _drainInFlight = false;
            ResetPresentationSurface();
            _settlementRaised = false;
            _isBusy = false;
        }

        /// <summary>
        /// 仅清卡牌/场地表现，保留遗物/技能/PlayerInfo 等持久 HUD（节点切换、胜负公告用）。
        /// </summary>
        public void ClearCardPresentationSurface()
        {
            CancelPresentationWork();
            FieldBattleManagerSingleton.Instance?.CancelBattleWork();
            CombatHitSink.ForceEndPresentationLock("ClearCardPresentationSurface");
            _drainInFlight = false;
            ResetCardPresentationSurface();
            _settlementRaised = false;
            _isBusy = false;
        }

        /// <summary>
        /// 局内持久 HUD 统一兜底刷新：遗物/技能栏 + PlayerInfo 数值。
        /// 节点开始、节点结束领奖后、房间事件收益后调用。
        /// </summary>
        public void RefreshPersistentInBattleUi(bool animate = false)
        {
            ResolveManagers();
            relicManager?.SyncFromCore();
            skillManager?.SyncFromCore();
            PlayerInfoHudPresenter.TryGetInstance()?.SyncFromCore(animate);
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
        /// 表现侧板面可能已无敌时回调；仅在内核已进入奖励相位（或 pending Reward）时推进结算。
        /// 不可单靠 IsNodeCleared：击杀当下 cleared 已为 true，但 OfferReward 须等 ResolvePostKillBoard。
        /// </summary>
        public void NotifyPresentationBoardMayBeClear()
        {
            TryEnterNodeSettlement();
        }

        /// <summary>
        /// 若内核已落在奖励相位（或 pending Reward），触发结算推进事件（UI stub）。
        /// 不单独用 IsNodeCleared 放行，避免 Vacate 抢在 PostKill 前推进主循环。
        /// </summary>
        public bool TryEnterNodeSettlement()
        {
            if (_settlementRaised)
            {
                return false;
            }

            var arch = NineGridArchitecture.Current;
            var phase = arch.GetSystem<IPhaseSystem>().CurrentPhase;
            var pending = arch.GetModel<PendingChoiceModel>();
            var inReward = phase == GamePhase.RewardItemChoice
                || (pending.Kind.Value == PendingChoiceKind.Reward
                    && pending.RewardOptions != null
                    && pending.RewardOptions.Count > 0);

            if (!inReward)
            {
                return false;
            }

            Debug.Log(
                $"[InBattleManager] 节点结算就绪 phase={phase} pending={pending.Kind.Value}");
            RaiseSettlementReady();
            return true;
        }

        /// <summary>
        /// 节点结算就绪：立刻并行启动残留帮助卡结算演出（不阻塞主循环推进奖励/房间），
        /// 再通知主循环。内核金币已在通关判定当拍一次性入账（CompleteNodeIfCleared）。
        /// </summary>
        private void RaiseSettlementReady()
        {
            _settlementRaised = true;
            RunUnusedHelpCardSettlementPresentationAsync(
                _nodeEventLogStart,
                EnsurePresentationToken()).Forget();
            OnNodeSettlementReady?.Invoke();
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
                // 清场/跨关取消：视图由 ResetPresentationSurface 兜底回收。
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[InBattleManager] 残留帮助卡结算演出异常: " + ex.Message);
            }
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

            try
            {
                // 跨关前等前关 Drain/tween 收束，避免与 Opening 发牌交错。
                await WaitPresentationIdleAsync(cancellationToken);
                CancelPresentationWork();
                // StartNode 前清零卡牌占格，保留遗物/技能/PlayerInfo 持久 HUD。
                ResetCardPresentationSurface();

                var arch = NineGridArchitecture.Current;
                var phase = arch.GetSystem<IPhaseSystem>();
                if (!phase.CanExecute(GameCommandKind.StartNode))
                {
                    Debug.LogWarning(
                        $"[InBattleManager] StartNode 非法 phase={phase.CurrentPhase}，先 BootstrapRun。");
                    BootstrapRun();
                }

                options ??= NodeDeckOptions.CreateDefaultBattle();
                // 记录本节点 EventLog 起点：结算演出据此定位 unusedHelpCards 金币事件。
                _nodeEventLogStart = arch.GetSystem<IActionPipelineSystem>().EventLog.Entries.Count;
                var result = phase.StartNode(options);
                if (!result.Accepted)
                {
                    Debug.LogError($"[InBattleManager] StartNode 被拒: {result.Reason}");
                    return;
                }

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
                    Debug.LogWarning("[InBattleManager] BattleTrace StartNode: " + ex.Message);
                }

                var plan = CaptureOpeningPresentationPlan(arch);
                var presentationCt = RenewPresentationToken();
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    presentationCt);
                FieldTraceHelper.SetBatchTag(FlowTraceBatchTags.Opening);
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
                    FieldTraceHelper.ClearBatchTag();
                }

                cardManager?.AuditRegistryIntegrity("Opening.Settled");
                RegistryTraceRecorder.ResetVisualBaselineForOpening();
                if (phase.CurrentPhase == GamePhase.InteractionLoop)
                {
                    cardManager?.AuditRegistryIntegrity("InteractionLoop.Idle");
                    RegistryTraceRecorder.BeginIdleWatch(linkedCts.Token);
                }

                RefreshPersistentInBattleUi(animate: false);

                // 开局即空怪：IsNodeCleared 但尚未 OfferReward，先走 PostKill→CompleteNodeIfCleared。
                if (phase.CurrentPhase == GamePhase.InteractionLoop
                    && arch.GetSystem<IDeckSystem>().IsNodeCleared())
                {
                    ResolvePostKillBoardFromCore();
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

                    // 开局在 InBattle 忙碌期内；与 DrainDeals 一致跳过场地忙锁。
                    // ensureCard：卡不在组内时兜底补入再发。
                    cardManager.TryGet(placement.Uid, out var ensureCard);
                    var ok = await deckManager.DealCardByUidAsync(
                        placement.Uid,
                        placement.GroundSlot,
                        ensureCard: ensureCard,
                        skipBusyGuard: true,
                        awaitMove: false,
                        cancellationToken: cancellationToken);
                    FieldTraceHelper.RecordOpeningDealProgress(
                        placement.Uid,
                        placement.GroundSlot,
                        ok,
                        i);
                    if (!ok)
                    {
                        Debug.LogWarning(
                            $"[InBattleManager] 就位失败 uid={placement.Uid} slot={placement.GroundSlot}，继续其余格并由 Sync 兜底。");
                        continue;
                    }

                    dealtAny = true;
                    if (i < ring.Count - 1 && dealInterval > 0f)
                    {
                        await UniTask.Delay(
                            TimeSpan.FromSeconds(dealInterval),
                            cancellationToken: cancellationToken);
                    }
                }

                // 末张飞入播完后再 SoftAlign/Sync，避免 KillMotion 掐掉轨迹。
                if (dealtAny && moveDuration > 0f)
                {
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(moveDuration),
                        cancellationToken: cancellationToken);
                }
            }
            finally
            {
                // 与 Drain/UseItem 对齐：开局发牌后必须 Sync，否则会出现
                // 表现空槽可点、Core 非空拒 ClickEmpty（道具旋转 Sync 后“自愈”）。
                SoftAlignBoardAnchorsToCore();
                SyncBoardOccupancyFromCore();
                CoreCardPresentationMapper.SyncAllSpawnedCards();
                UpdateAvatarDebugText();
            }
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
            ResetCardPresentationSurface();
            ClearPersistentInBattleHud();
        }

        private void ResetCardPresentationSurface()
        {
            ResolveManagers();
            // 先取消交战/手牌异步，再强制清占格与手牌槽，最后统一 Release 视图。
            FieldBattleManagerSingleton.Instance?.CancelBattleWork();
            CardHandManagerSingleton.Instance?.ClearHand();
            deckManager?.ResetToStandby();
            fieldManager?.ClearField(force: true);
            cardManager?.ReleaseAll("Presentation.ResetCardSurface");
            DescriptionManagerSingleton.TryGetInstance()?.Clear();
            _isBusy = false;
        }

        private void ClearPersistentInBattleHud()
        {
            ResolveManagers();
            relicManager?.Clear();
            skillManager?.Clear();
            PlayerInfoHudPresenter.TryGetInstance()?.ClearSnapshot();
        }

        /// <summary>
        /// 等待 Drain / 场地 / 手牌 / 卡组 / 交战 tween 收束，避免跨关清场与开局发牌交错。
        /// 超时后打 Warn 并继续（由后续 Cancel + Reset 硬清）。
        /// </summary>
        private async UniTask WaitPresentationIdleAsync(
            CancellationToken cancellationToken,
            float timeoutSeconds = 2f)
        {
            ResolveManagers();
            var deadline = Time.realtimeSinceStartup + Mathf.Max(0.1f, timeoutSeconds);
            while (Time.realtimeSinceStartup < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fieldBusy = fieldManager != null && fieldManager.IsFieldBusy;
                var hand = CardHandManagerSingleton.Instance;
                var handBusy = hand != null && hand.IsBusy;
                var deckBusy = deckManager != null && deckManager.IsBusy;
                var battle = FieldBattleManagerSingleton.Instance;
                var battleBusy = battle != null && battle.IsBusy;

                if (!_drainInFlight
                    && !fieldBusy
                    && !handBusy
                    && !deckBusy
                    && !battleBusy
                    && !CombatHitSink.PresentationLocked)
                {
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            Debug.LogWarning(
                $"[InBattleManager] WaitPresentationIdle 超时({timeoutSeconds:0.##}s)：drain={_drainInFlight} " +
                $"fieldBusy={fieldManager != null && fieldManager.IsFieldBusy} " +
                $"presentationLocked={CombatHitSink.PresentationLocked}，继续清场。");
        }

        private void CancelPresentationWork()
        {
            if (_presentationCts != null)
            {
                _presentationCts.Cancel();
                _presentationCts.Dispose();
                _presentationCts = null;
            }

            CombatHitSink.ForceEndPresentationLock("CancelPresentationWork");
            _drainInFlight = false;
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
            RefreshPersistentInBattleUi(animate: false);
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
            CombatHitSink.NotifyNodeSettlementReady = OnNodeSettlementFromCombat;
            FieldTraceHelper.RegisterSinkHandlers();
            PerfTraceRecorder.RegisterSinkHandlers();
            RegistryTraceRecorder.RegisterSinkHandlers();
            RegisterHandBridge();
        }

        private void UnregisterCombatHitSink()
        {
            FieldTraceHelper.UnregisterSinkHandlers();
            PerfTraceRecorder.UnregisterSinkHandlers();
            RegistryTraceRecorder.UnregisterSinkHandlers();
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

            CombatHitSink.ForceEndPresentationLock("UnregisterCombatHitSink");
            _drainInFlight = false;

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

            if (CombatHitSink.NotifyNodeSettlementReady == OnNodeSettlementFromCombat)
            {
                CombatHitSink.NotifyNodeSettlementReady = null;
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

            BoardCardSelectModeController.SelectionCompletedAsync -= OnBoardSelectionCompletedAsync;
            BoardCardSelectModeController.SelectionCompletedAsync += OnBoardSelectionCompletedAsync;
            BoardCardSelectModeController.SelectionAbortedAsync -= OnBoardSelectionAbortedAsync;
            BoardCardSelectModeController.SelectionAbortedAsync += OnBoardSelectionAbortedAsync;
        }

        private void UnregisterHandBridge()
        {
            var hand = CardHandManagerSingleton.Instance;
            if (hand == null)
            {
                return;
            }

            hand.DragApplyValidator -= ValidateHandDragApplyAsync;
            BoardCardSelectModeController.SelectionCompletedAsync -= OnBoardSelectionCompletedAsync;
            BoardCardSelectModeController.SelectionAbortedAsync -= OnBoardSelectionAbortedAsync;
            BoardCardSelectModeController.End();
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
                if (BattleTraceRecorder.Enabled || FlowTraceRecorder.Enabled)
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
                        RecordCombatHitFlowSummary(
                            reason,
                            attackerSnap,
                            targetSnap,
                            phaseBefore,
                            phaseSystem.CurrentPhase.ToString(),
                            accepted: false,
                            damageAmount: 0,
                            targetKilled: false,
                            avatarDefeated: false,
                            avatarHpAfter: targetSnap != null ? targetSnap.hp : -1);
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

            PresentGoldGainsFromEventLog(startIndex, ResolveCardWorldPosition(targetUid));
            PresentEffectTriggersFromEventLog(startIndex);

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
                if (BattleTraceRecorder.Enabled || FlowTraceRecorder.Enabled)
                {
                    var endIndex = entries.Count;
                    if (BattleTraceRecorder.Enabled)
                    {
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

                    RecordCombatHitFlowSummary(
                        reason,
                        attackerSnap,
                        targetSnap,
                        phaseBefore,
                        phase.ToString(),
                        accepted: true,
                        damageAmount: summary.DamageAmount,
                        targetKilled: summary.TargetKilled,
                        avatarDefeated: summary.AvatarDefeated,
                        avatarHpAfter: ResolveAvatarHpAfterHit(arch, targetUid, summary));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[InBattleManager] BattleTrace post-hit: " + ex.Message);
            }

            return summary;
        }

        private static int ResolveAvatarHpAfterHit(
            IArchitecture arch,
            int targetUid,
            CombatHitPresentationResult summary)
        {
            try
            {
                var board = arch.GetModel<BoardModel>();
                var avatarUid = board.AvatarUid.Value;
                // 始终读打后有效 HP；勿信 summary.RemainingHp（0 伤时常为默认 0）。
                if (arch.GetModel<CardRegistry>().TryGet(avatarUid, out var avatar) && avatar != null)
                {
                    return arch.GetSystem<IStatSystem>().GetEffectiveInt(avatar, StatId.Hp);
                }

                if (targetUid == avatarUid && summary.DamageAmount > 0)
                {
                    return summary.RemainingHp;
                }
            }
            catch
            {
                // ignore
            }

            return -1;
        }

        private static void RecordCombatHitFlowSummary(
            string reason,
            BattleTraceCardSnap attackerSnap,
            BattleTraceCardSnap targetSnap,
            string phaseBefore,
            string phaseAfter,
            bool accepted,
            int damageAmount,
            bool targetKilled,
            bool avatarDefeated,
            int avatarHpAfter)
        {
            try
            {
                if (!FlowTraceRecorder.Enabled)
                {
                    return;
                }

                FlowTraceRecorder.BeginSessionIfNeeded();
                FlowTraceRecorder.Record(
                    FlowTraceCategory.CombatSummary,
                    FlowTraceNames.CombatHitSummary,
                    new Dictionary<string, string>
                    {
                        { "reason", reason ?? string.Empty },
                        { "attackerDefId", attackerSnap != null ? attackerSnap.defId : string.Empty },
                        { "targetDefId", targetSnap != null ? targetSnap.defId : string.Empty },
                        { "attackerUid", attackerSnap != null ? attackerSnap.uid.ToString() : "0" },
                        { "targetUid", targetSnap != null ? targetSnap.uid.ToString() : "0" },
                        { "damage", damageAmount.ToString() },
                        { "targetKilled", targetKilled ? "true" : "false" },
                        { "avatarDefeated", avatarDefeated ? "true" : "false" },
                        { "avatarHpAfter", avatarHpAfter.ToString() },
                    },
                    phaseBefore: phaseBefore,
                    phaseAfter: phaseAfter,
                    accepted: accepted,
                    refBattleOpIndex: BattleTraceRecorder.LastOpIndex);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[InBattleManager] FlowTrace CombatHitSummary: " + ex.Message);
            }
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
                summary.RemovedUids = Array.Empty<int>();
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
                        RecordPostKillFlowSummary(
                            arch,
                            phaseBefore,
                            phase.ToString(),
                            accepted: false,
                            moveCount: 0,
                            dealCount: 0);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[InBattleManager] BattleTrace PostKill reject: " + ex.Message);
                }

                return summary;
            }

            FillBoardDeltaFromEventLog(pipeline, startIndex, out var moves, out var deals, out _, out var removedUids);
            summary.Moves = moves;
            summary.Deals = deals;
            summary.RemovedUids = removedUids;
            summary.DamagePopups = CollectDamagePopups(pipeline.EventLog.Entries, startIndex);
            PresentGoldGainsFromEventLog(startIndex);
            PresentEffectTriggersFromEventLog(startIndex);

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
                    RecordPostKillFlowSummary(
                        arch,
                        phaseBefore,
                        phase.ToString(),
                        accepted: true,
                        moveCount: moves != null ? moves.Length : 0,
                        dealCount: deals != null ? deals.Length : 0);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[InBattleManager] BattleTrace PostKill: " + ex.Message);
            }

            return summary;
        }

        private static void RecordPostKillFlowSummary(
            IArchitecture arch,
            string phaseBefore,
            string phaseAfter,
            bool accepted,
            int moveCount,
            int dealCount)
        {
            try
            {
                if (!FlowTraceRecorder.Enabled)
                {
                    return;
                }

                var deckEmpty = true;
                var enemyDrawEmpty = true;
                var drawPileCount = 0;
                try
                {
                    var deck = arch.GetModel<DeckModel>();
                    drawPileCount = deck.DrawPileUids.Count;
                    deckEmpty = drawPileCount == 0;
                    enemyDrawEmpty = !arch.GetSystem<IDeckSystem>().HasEnemyInDrawPile();
                }
                catch
                {
                    // ignore
                }

                FlowTraceRecorder.BeginSessionIfNeeded();
                FlowTraceRecorder.Record(
                    FlowTraceCategory.Deck,
                    FlowTraceNames.PostKillBoard,
                    new Dictionary<string, string>
                    {
                        { "moveCount", moveCount.ToString() },
                        { "dealCount", dealCount.ToString() },
                        { "drawPileCount", drawPileCount.ToString() },
                        { "deckEmpty", deckEmpty ? "true" : "false" },
                        { "enemyDrawEmpty", enemyDrawEmpty ? "true" : "false" },
                    },
                    phaseBefore: phaseBefore,
                    phaseAfter: phaseAfter,
                    accepted: accepted,
                    refBattleOpIndex: BattleTraceRecorder.LastOpIndex);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[InBattleManager] FlowTrace PostKillBoard: " + ex.Message);
            }
        }

        /// <summary>
        /// 表现缓冲缓释：先补牌再旋转 hop（Deals → Moves，对齐 Core Fill→Rotate），末尾两阶段 Sync 安全网。
        /// 若外层已持 PresentationLocked 则不重复加解锁；否则本方法自持锁。
        /// </summary>
        private async UniTask DrainPostKillBoardAsync(
            PostKillBoardPresentationResult result,
            CancellationToken cancellationToken)
        {
            if (!result.Accepted)
            {
                return;
            }

            if (_drainInFlight)
            {
                Debug.LogWarning("[InBattleManager] DrainPostKillBoard 已在进行，拒绝并发缓释。");
                return;
            }

            var ownedByCaller = CombatHitSink.PresentationLocked;
            var acquiredHere = false;
            if (!ownedByCaller)
            {
                if (!CombatHitSink.TryBeginPresentationLock("Drain"))
                {
                    Debug.LogWarning("[InBattleManager] DrainPostKillBoard 无法获取表现锁，跳过。");
                    return;
                }

                acquiredHere = true;
            }

            _drainInFlight = true;
            var moveCount = result.Moves?.Length ?? 0;
            var dealCount = result.Deals?.Length ?? 0;
            FieldTraceHelper.SetBatchTag(FlowTraceBatchTags.PostKill);
            var drainNode = 0;
            int.TryParse(FieldTraceHelper.ResolveNodeIndex(), out drainNode);
            PerfTraceRecorder.OpenBeat(DiagBeatKinds.PostKillDrain, drainNode);
            try
            {
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

                FieldTraceHelper.RecordDrainBegin(
                    moveCount,
                    dealCount,
                    drainInFlight: true,
                    fieldBusy: fieldManager.IsBusy,
                    presentationLocked: CombatHitSink.PresentationLocked);
                FieldTraceHelper.RecordOccupancySnapshot("drainBefore");
                fieldManager.ClearOccupancyConflictFlag();

                // 对齐 Core Fill→Rotate / EventLog：先补牌（CardDealt），技能移除退场，再 hop，再二次补空。
                if (result.Deals != null && result.Deals.Length > 0)
                {
                    await DrainDealsAsync(result.Deals, ct);
                }

                if (result.RemovedUids != null && result.RemovedUids.Length > 0)
                {
                    await PresentSkillRemovedCardsAsync(result.RemovedUids, ct);
                }

                if (result.Moves != null && result.Moves.Length > 0)
                {
                    await fieldManager.ApplyBoardMovesAndHopAsync(
                        result.Moves,
                        ct,
                        skipBusyGuard: true);
                }

                SoftAlignBoardAnchorsToCore();

                // 技能移除退场 + hop 完成后：有牌则补空槽；交互由 PresentationLocked 挂起至补牌播完。
                if (result.RemovedUids != null && result.RemovedUids.Length > 0)
                {
                    await DrainPostRemoveRefillAsync(ct);
                }

                SyncBoardOccupancyFromCore();
                if (fieldManager.HasOccupancyConflictSinceClear)
                {
                    Debug.LogWarning(
                        "[InBattleManager] Drain 期间发生 OccupancyConflict，再次强制 SyncBoardOccupancyFromCore。");
                    SyncBoardOccupancyFromCore();
                }

                FieldTraceHelper.RecordOccupancySnapshot("drainAfter");
                FieldTraceHelper.RecordDrainEnd(
                    moveCount,
                    dealCount,
                    drainInFlight: true,
                    fieldBusy: fieldManager.IsBusy,
                    presentationLocked: CombatHitSink.PresentationLocked);
                SpawnDamagePopups(result.DamagePopups, fallbackVictim: null, fallbackAmount: 0);
                UpdateAvatarDebugText();

                if (result.NodeClearedOrRewardPhase)
                {
                    TryEnterNodeSettlement();
                }
            }
            finally
            {
                _drainInFlight = false;
                PerfTraceRecorder.CloseBeat();
                FieldTraceHelper.ClearBatchTag();
                if (acquiredHere)
                {
                    CombatHitSink.EndPresentationLock("Drain");
                }
            }
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

        /// <summary>
        /// 按 DamageDealt 事件序分别飘字；无 popups 时回退主目标 DamageAmount（对齐 FieldBattle 强兜底）。
        /// </summary>
        private void SpawnDamagePopups(
            CombatDamagePopup[] popups,
            ManagedCard fallbackVictim,
            int fallbackAmount)
        {
            ResolveManagers();
            if (popups != null && popups.Length > 0)
            {
                for (var i = 0; i < popups.Length; i++)
                {
                    var popup = popups[i];
                    if (popup.Amount <= 0 || popup.TargetUid <= 0)
                    {
                        continue;
                    }

                    Vector3? pos = null;
                    if (cardManager != null
                        && cardManager.TryGet(popup.TargetUid, out var view)
                        && view?.Transform != null)
                    {
                        pos = view.Transform.position;
                    }
                    else if (fallbackVictim != null
                             && fallbackVictim.Uid == popup.TargetUid
                             && fallbackVictim.Transform != null)
                    {
                        pos = fallbackVictim.Transform.position;
                    }

                    if (pos.HasValue)
                    {
                        SpawnDamageNumberAt(pos.Value, popup.Amount);
                    }
                }

                return;
            }

            if (fallbackAmount > 0 && fallbackVictim?.Transform != null)
            {
                SpawnDamageNumberAt(fallbackVictim.Transform.position, fallbackAmount);
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
                summary.RemovedUids = Array.Empty<int>();
                return summary;
            }

            FillBoardDeltaFromEventLog(pipeline, startIndex, out var moves, out var deals, out var pickedUid, out var removedUids);
            summary.CardUid = pickedUid;
            summary.Moves = moves;
            summary.Deals = deals;
            summary.RemovedUids = removedUids;

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

            // 金币卡等即时改 Coins：先按事件带出生点开演，再静默刷 HUD（避免二次开演/跳变）。
            PresentGoldGainsFromEventLog(startIndex, ResolveCardWorldPosition(pickedUid));
            PresentEffectTriggersFromEventLog(startIndex);
            PlayerInfoHudPresenter.TryGetInstance()?.SyncFromCore(animate: false);
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
                summary.RemovedUids = Array.Empty<int>();
                summary.DamagePopups = Array.Empty<CombatDamagePopup>();
                return summary;
            }

            FillBoardDeltaFromEventLog(pipeline, startIndex, out var moves, out var deals, out _, out var removedUids);
            summary.Moves = moves;
            summary.Deals = deals;
            summary.RemovedUids = removedUids;
            summary.DamagePopups = CollectDamagePopups(pipeline.EventLog.Entries, startIndex);
            PresentGoldGainsFromEventLog(startIndex, ResolveBoardSlotWorldPosition(groundSlot));
            PresentEffectTriggersFromEventLog(startIndex);
            return summary;
        }

        private static UseItemPresentationResult ApplyUseItemFromCore(
            int itemUid,
            int[] selectedCardUids,
            string selectedOption = null)
        {
            var arch = NineGridArchitecture.Current;
            var pipeline = arch.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            int[] selected = null;
            if (selectedCardUids != null && selectedCardUids.Length > 0)
            {
                selected = selectedCardUids;
            }

            var result = arch.GetSystem<IPhaseSystem>().ApplyUseItem(itemUid, selected, selectedOption);
            var summary = new UseItemPresentationResult
            {
                Accepted = result.Accepted,
                DamagePopups = Array.Empty<CombatDamagePopup>(),
                PrimaryTargetUid = selected != null && selected.Length > 0 ? selected[0] : 0,
            };
            if (!result.Accepted)
            {
                Debug.LogWarning($"[InBattleManager] UseItem 被拒: {result.Reason}");
                return summary;
            }

            var killedUids = new List<int>(2);
            var entries = pipeline.EventLog.Entries;
            var primaryUid = summary.PrimaryTargetUid;
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

                // 主目标伤害标量：与 CombatHit 一致，取本段最后一次命中主目标的 DamageDealt。
                if (e.Type == CoreEventType.DamageDealt
                    && e.Amount > 0
                    && primaryUid > 0
                    && e.TargetUid == primaryUid)
                {
                    summary.DamageAmount = e.Amount;
                }
            }

            // 飞刀等：EventLog 偶发漏 CardKilled 时，用选定目标的 Graveyard/Removed/Hp 兜底。
            if (!summary.TargetKilled
                && primaryUid > 0
                && arch.GetModel<CardRegistry>().TryGet(primaryUid, out var target)
                && (target.Zone.Value == ZoneId.Graveyard
                    || target.Zone.Value == ZoneId.Removed
                    || arch.GetSystem<IStatSystem>().GetEffectiveInt(target, StatId.Hp) <= 0)
                && target.Kind != CardKind.Avatar)
            {
                summary.TargetKilled = true;
                killedUids.Add(primaryUid);
            }

            summary.KilledTargetUids = killedUids.Count > 0
                ? killedUids.ToArray()
                : Array.Empty<int>();
            summary.DamagePopups = CollectDamagePopups(entries, startIndex);
            PresentGoldGainsFromEventLog(
                startIndex,
                ResolveCardWorldPosition(summary.PrimaryTargetUid));
            PresentEffectTriggersFromEventLog(startIndex);

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

            FillBoardDeltaFromEventLog(pipeline, startIndex, out var moves, out var deals, out _, out var removedUids);
            if ((moves != null && moves.Length > 0)
                || (deals != null && deals.Length > 0)
                || (removedUids != null && removedUids.Length > 0)
                || summary.TargetKilled)
            {
                // 飘字由 PresentUseItemEffectsAsync 用 summary.DamagePopups 强兜底；此处不重复塞。
                summary.PostKillBoard = new PostKillBoardPresentationResult
                {
                    Accepted = true,
                    Moves = moves ?? Array.Empty<PostKillCardMove>(),
                    Deals = deals ?? Array.Empty<PostKillCardDeal>(),
                    RemovedUids = removedUids ?? Array.Empty<int>(),
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
            FillBoardDeltaFromEventLog(pipeline, startIndex, out moves, out deals, out pickedUid, out _);
        }

        private static void FillBoardDeltaFromEventLog(
            IActionPipelineSystem pipeline,
            int startIndex,
            out PostKillCardMove[] moves,
            out PostKillCardDeal[] deals,
            out int pickedUid,
            out int[] removedUids)
        {
            var moveList = new List<PostKillCardMove>(8);
            var dealList = new List<PostKillCardDeal>(8);
            var removeList = new List<int>(4);
            var removedSet = new HashSet<int>(4);
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

                if ((e.Type == CoreEventType.CardRemoved || e.Type == CoreEventType.CardKilled)
                    && e.CardUid > 0
                    && removedSet.Add(e.CardUid))
                {
                    removeList.Add(e.CardUid);
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

            // 同批已移除的牌不再 hop（避免碾压后仍飞到目标格再被 Sync 硬删）。
            if (removedSet.Count > 0 && moveList.Count > 0)
            {
                for (var i = moveList.Count - 1; i >= 0; i--)
                {
                    if (removedSet.Contains(moveList[i].Uid))
                    {
                        moveList.RemoveAt(i);
                    }
                }
            }

            moves = moveList.ToArray();
            deals = dealList.ToArray();
            removedUids = removeList.Count > 0 ? removeList.ToArray() : Array.Empty<int>();
        }

        /// <summary>
        /// 扫描 EffectTriggered：对效果所有者播「基础卡牌效果触发」脉冲（发射后不管）。
        /// 仅九宫格在场卡播脉冲；卡组 / 手牌 / 已移除不播。
        /// </summary>
        public static void PresentEffectTriggersFromEventLog(int startIndex)
        {
            if (startIndex < 0)
            {
                return;
            }

            var arch = NineGridArchitecture.Current;
            var entries = arch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            if (startIndex >= entries.Count)
            {
                return;
            }

            var cardManager = CardManagerSingleton.Instance;
            if (cardManager == null)
            {
                return;
            }

            var seen = new HashSet<int>();
            for (var i = startIndex; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type != CoreEventType.EffectTriggered || e.CardUid <= 0)
                {
                    continue;
                }

                if (!seen.Add(e.CardUid))
                {
                    continue;
                }

                if (!cardManager.TryGet(e.CardUid, out var card)
                    || card == null
                    || card.IsFieldDead
                    || card.DisplayMode == CardDisplayMode.RemovedMode
                    || card.DisplayMode == CardDisplayMode.CardDeckMode
                    || card.DisplayMode == CardDisplayMode.HandCardMode)
                {
                    continue;
                }

                if (!IsCoreCardOnBoardForEffectPresentation(e.CardUid))
                {
                    continue;
                }

                if (card.TryGetEffectManager(out var effectManager))
                {
                    effectManager.PlayEffectTriggerPulse();
                }
            }
        }

        private static bool IsCoreCardOnBoardForEffectPresentation(int uid)
        {
            if (uid <= 0)
            {
                return false;
            }

            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                return false;
            }

            var registry = arch.GetModel<CardRegistry>();
            CardInstance coreCard;
            if (!registry.TryGet(uid, out coreCard))
            {
                return false;
            }

            return coreCard.Zone.Value == ZoneId.Board;
        }

        private async UniTask PresentSkillRemovedCardsAsync(int[] removedUids, CancellationToken ct)
        {
            ResolveManagers();
            if (removedUids == null || removedUids.Length == 0 || cardManager == null)
            {
                return;
            }

            var battle = FieldBattleManagerSingleton.Instance;
            if (battle == null)
            {
                return;
            }

            var tasks = new List<UniTask>(removedUids.Length);
            for (var i = 0; i < removedUids.Length; i++)
            {
                var uid = removedUids[i];
                if (uid <= 0 || !cardManager.TryGet(uid, out var card) || card == null)
                {
                    continue;
                }

                // 交战主目标尸体已 MarkFieldDead + 异步 Finalize，勿重复播死。
                if (card.IsFieldDead && card.DisplayMode == CardDisplayMode.RemovedMode)
                {
                    continue;
                }

                if (card.DisplayMode == CardDisplayMode.RemovedMode
                    && !fieldManager.TryGetSlotOf(uid, out _))
                {
                    continue;
                }

                tasks.Add(battle.PresentRemovedFieldCardAsync(card, ct));
            }

            if (tasks.Count > 0)
            {
                await UniTask.WhenAll(tasks);
            }
        }

        /// <summary>
        /// 技能移除退场后：Core FillEmptySlots + 播补牌。仅 InteractionLoop 且牌堆有牌时执行。
        /// </summary>
        private async UniTask DrainPostRemoveRefillAsync(CancellationToken ct)
        {
            var arch = NineGridArchitecture.Current;
            var phaseSystem = arch.GetSystem<IPhaseSystem>();
            if (phaseSystem.CurrentPhase != GamePhase.InteractionLoop)
            {
                return;
            }

            var deck = arch.GetModel<DeckModel>();
            if (deck == null || deck.DrawPileUids == null || deck.DrawPileUids.Count <= 0)
            {
                return;
            }

            // 已无空槽则跳过，避免空跑。
            var board = arch.GetModel<BoardModel>();
            var hasEmpty = false;
            for (var s = SlotId.MinBoardIndex; s <= SlotId.MaxBoardIndex; s++)
            {
                if (s == GroundSlotTopology.AvatarReservedSlot)
                {
                    continue;
                }

                if (board.GetCardUid(SlotId.Board(s)) <= 0)
                {
                    hasEmpty = true;
                    break;
                }
            }

            if (!hasEmpty)
            {
                return;
            }

            var pipeline = arch.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            arch.GetSystem<IBoardSystem>().FillEmptySlots();
            FillBoardDeltaFromEventLog(pipeline, startIndex, out _, out var refillDeals, out _);
            PresentEffectTriggersFromEventLog(startIndex);
            if (refillDeals != null && refillDeals.Length > 0)
            {
                await DrainDealsAsync(refillDeals, ct);
            }
        }

        private async UniTask<bool> ValidateHandDragApplyAsync(ManagedCard card, int? targetGroundSlot)
        {
            if (card == null)
            {
                return false;
            }

            if (CombatHitSink.ChoiceOverlayActive || CombatHitSink.PresentationLocked)
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

            if (HelpCardBoardSelectResolver.TryGetRequiredBoardSelectCount(card.DefId, out var boardSelectCount))
            {
                if (!BoardCardSelectModeController.Begin(card.Uid, card.DefId, boardSelectCount))
                {
                    if (IsStatBoostCard(card.DefId))
                    {
                        RestoreHandCardAfterChoiceCancel(card);
                    }

                    return false;
                }

                return true;
            }

            if (!CombatHitSink.TryBeginPresentationLock("UseItem"))
            {
                if (IsStatBoostCard(card.DefId))
                {
                    RestoreHandCardAfterChoiceCancel(card);
                }

                return false;
            }

            int[] selectedUids = null;
            if (targetUid.HasValue && targetUid.Value > 0)
            {
                selectedUids = new[] { targetUid.Value };
            }

            var useResult = ApplyUseItemFromCore(card.Uid, selectedUids, selectedOption);
            if (!useResult.Accepted)
            {
                CombatHitSink.EndPresentationLock("UseItem-rejected");
                if (IsStatBoostCard(card.DefId))
                {
                    RestoreHandCardAfterChoiceCancel(card);
                }

                return false;
            }

            PresentUseItemEffectsAsync(useResult).Forget();
            return true;
        }

        private async UniTask OnBoardSelectionCompletedAsync(int itemUid, int[] selectedUids)
        {
            var defId = BoardCardSelectModeController.ItemDefId;
            var requiredCount = BoardCardSelectModeController.RequiredCount;
            if (itemUid <= 0 || selectedUids == null || selectedUids.Length == 0)
            {
                await RestoreBoardSelectItemToHandAsync(itemUid, defId, "invalid-selection");
                return;
            }

            SoftAlignBoardAnchorsToCore();
            SyncBoardOccupancyFromCore();

            if (!TryValidateBoardSelectTargets(selectedUids, requiredCount, out var validateReason))
            {
                Debug.LogWarning(
                    $"[InBattleManager] BoardSelect 目标校验失败 itemUid={itemUid} reason={validateReason} uids={string.Join(",", selectedUids)}");
                await RestoreBoardSelectItemToHandAsync(itemUid, defId, $"invalid-targets:{validateReason}");
                return;
            }

            if (!await TryAcquirePresentationLockForBoardSelectAsync("UseItem"))
            {
                await RestoreBoardSelectItemToHandAsync(itemUid, defId, "lock-timeout");
                return;
            }

            var useResult = ApplyUseItemFromCore(itemUid, selectedUids, null);
            if (!useResult.Accepted)
            {
                CombatHitSink.EndPresentationLock("UseItem-rejected");
                await RestoreBoardSelectItemToHandAsync(itemUid, defId, "use-rejected");
                return;
            }

            RegistryTraceSink.NotifyUserInteraction?.Invoke("BoardSelectUseItemAccepted");
            PresentUseItemEffectsAsync(useResult).Forget();
            await UniTask.CompletedTask;
        }

        private UniTask OnBoardSelectionAbortedAsync(int itemUid, string defId, string reason)
        {
            return RestoreBoardSelectItemToHandAsync(itemUid, defId, reason);
        }

        private static async UniTask<bool> TryAcquirePresentationLockForBoardSelectAsync(string reason)
        {
            const int stepMs = 50;
            var elapsed = 0;
            while (elapsed < BoardSelectLockWaitMs)
            {
                if (!CombatHitSink.ChoiceOverlayActive
                    && !CombatHitSink.PresentationLocked
                    && CombatHitSink.TryBeginPresentationLock(reason))
                {
                    return true;
                }

                await UniTask.Delay(stepMs);
                elapsed += stepMs;
            }

            return false;
        }

        private static bool TryValidateBoardSelectTargets(int[] selectedUids, int requiredCount, out string failReason)
        {
            failReason = null;
            if (selectedUids == null || selectedUids.Length == 0)
            {
                failReason = "empty";
                return false;
            }

            if (requiredCount > 0 && selectedUids.Length != requiredCount)
            {
                failReason = "count-mismatch";
                return false;
            }

            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                failReason = "no-arch";
                return false;
            }

            var registry = arch.GetModel<CardRegistry>();
            var board = arch.GetModel<BoardModel>();
            for (var i = 0; i < selectedUids.Length; i++)
            {
                var uid = selectedUids[i];
                if (uid <= 0 || !registry.TryGet(uid, out var card) || card == null)
                {
                    failReason = $"missing-uid:{uid}";
                    return false;
                }

                if (card.Kind == CardKind.Avatar)
                {
                    failReason = $"avatar-uid:{uid}";
                    return false;
                }

                if (card.Zone.Value != ZoneId.Board)
                {
                    failReason = $"zone-{card.Zone.Value}-uid:{uid}";
                    return false;
                }

                var slot = card.Slot.Value;
                if (!slot.IsBoardSlot || board.GetCardUid(slot) != uid)
                {
                    failReason = $"slot-mismatch-uid:{uid}";
                    return false;
                }
            }

            return true;
        }

        private static async UniTask<bool> WaitForHandReadyForRestoreAsync(CardHandManagerSingleton hand)
        {
            if (hand == null)
            {
                return false;
            }

            const int stepMs = 50;
            var elapsed = 0;
            while (elapsed < BoardSelectLockWaitMs)
            {
                if (!hand.IsDragging && hand.CanAcceptCard)
                {
                    return true;
                }

                await UniTask.Delay(stepMs);
                elapsed += stepMs;
            }

            return !hand.IsDragging && hand.CanAcceptCard;
        }

        private async UniTask RestoreBoardSelectItemToHandAsync(int itemUid, string defId, string reason)
        {
            if (itemUid <= 0)
            {
                return;
            }

            Debug.LogWarning(
                $"[InBattleManager] BoardSelectAbort itemUid={itemUid} defId={defId ?? "?"} reason={reason ?? "unknown"}");
            RegistryTraceSink.NotifyUserInteraction?.Invoke($"BoardSelectAbort:{reason}");

            BoardCardSelectModeController.End();

            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                return;
            }

            var registry = arch.GetModel<CardRegistry>();
            if (!registry.TryGet(itemUid, out var coreCard)
                || coreCard.Zone.Value != ZoneId.ItemSlots)
            {
                Debug.LogWarning(
                    $"[InBattleManager] BoardSelectAbort skip restore: item not in ItemSlots uid={itemUid}");
                return;
            }

            var deck = arch.GetModel<DeckModel>();
            var inSlots = false;
            for (var i = 0; i < deck.ItemSlotUids.Count; i++)
            {
                if (deck.ItemSlotUids[i] == itemUid)
                {
                    inSlots = true;
                    break;
                }
            }

            if (!inSlots)
            {
                return;
            }

            defId = string.IsNullOrEmpty(defId) ? coreCard.DefId : defId;
            ResolveManagers();
            if (cardManager == null)
            {
                return;
            }

            if (!cardManager.TryGet(itemUid, out var card) || card == null)
            {
                card = cardManager.SpawnView(itemUid, defId, initialMode: CardDisplayMode.HandCardMode);
                if (card != null)
                {
                    CoreCardPresentationMapper.ApplyToManagedCard(card);
                }
            }
            else
            {
                cardManager.SetDisplayMode(card, CardDisplayMode.HandCardMode);
                CardOpacityUtility.ResetAlpha(card);
            }

            if (card == null)
            {
                return;
            }

            var hand = CardHandManagerSingleton.Instance;
            if (hand == null)
            {
                return;
            }

            if (!await WaitForHandReadyForRestoreAsync(hand))
            {
                Debug.LogWarning(
                    $"[InBattleManager] BoardSelectAbort restore hand busy timeout itemUid={itemUid}");
                return;
            }

            var restored = await hand.PullFromGroundAsync(card);
            if (!restored)
            {
                Debug.LogWarning(
                    $"[InBattleManager] BoardSelectAbort PullFromGround failed itemUid={itemUid}");
            }
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
                ResolveManagers();
                CoreCardPresentationMapper.SyncAllSpawnedCards();
                UpdateAvatarDebugText();

                // 飞刀等 UseItem 直伤：对齐 FieldBattle 强兜底（本段 popups + 主目标 DamageAmount）。
                // 必须在 Vacate 尸体之前飘字，否则目标 Transform 可能已卸。
                ManagedCard fallbackVictim = null;
                if (useResult.PrimaryTargetUid > 0
                    && cardManager != null
                    && cardManager.TryGet(useResult.PrimaryTargetUid, out var primary)
                    && primary != null)
                {
                    fallbackVictim = primary;
                }

                SpawnDamagePopups(useResult.DamagePopups, fallbackVictim, useResult.DamageAmount);

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
                    // 局内宝箱等：仍在 InteractionLoop，当场 Bounce；通关奖励由主循环接 OnNodeSettlementReady。
                    var phase = NineGridArchitecture.Current.GetSystem<IPhaseSystem>().CurrentPhase;
                    if (phase == GamePhase.RewardItemChoice)
                    {
                        TryEnterNodeSettlement();
                    }
                    else
                    {
                        await PresentRewardChoiceFromCoreAsync(hoverOnNotice: false);
                    }

                    return;
                }

                if (useResult.NodeClearedOrRewardPhase)
                {
                    TryEnterNodeSettlement();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                CombatHitSink.EndPresentationLock("UseItem-effects");
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

        /// <summary>
        /// 展示 Core PendingChoice 奖励 Bounce；主流程传 <paramref name="hoverOnNotice"/> 写 NoticeText。
        /// </summary>
        public async UniTask PresentRewardChoiceFromCoreAsync(bool hoverOnNotice = false)
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
                var pick = await WaitBouncePickAsync(
                    selector,
                    defIds,
                    allowEscapeSkip: true,
                    hoverOnNotice: hoverOnNotice);
                if (pick.Cancelled)
                {
                    return;
                }

                var pipeline = arch.GetSystem<IActionPipelineSystem>();
                var startIndex = pipeline.EventLog.Entries.Count;
                var phaseSystem = arch.GetSystem<IPhaseSystem>();
                var phaseBefore = phaseSystem.CurrentPhase.ToString();
                CoreCommandResult result;
                string chosenDefId;
                string choiceKind;
                if (pick.SkipRequested || pick.Index < 0)
                {
                    chosenDefId = string.Empty;
                    choiceKind = "skip";
                    result = phaseSystem.SkipHelpChoice();
                    if (!result.Accepted)
                    {
                        Debug.LogWarning($"[InBattleManager] SkipHelpChoice 被拒: {result.Reason}");
                        RecordRewardChosenFlow(
                            choiceKind,
                            chosenDefId,
                            -1,
                            phaseBefore,
                            phaseSystem.CurrentPhase.ToString(),
                            accepted: false,
                            reason: result.Reason);
                        return;
                    }
                }
                else
                {
                    chosenDefId = pick.Index >= 0 && pick.Index < defIds.Length
                        ? defIds[pick.Index]
                        : string.Empty;
                    choiceKind = "select";
                    result = phaseSystem.SelectReward(pick.Index);
                    if (!result.Accepted)
                    {
                        Debug.LogWarning($"[InBattleManager] SelectReward 被拒: {result.Reason}");
                        RecordRewardChosenFlow(
                            choiceKind,
                            chosenDefId,
                            pick.Index,
                            phaseBefore,
                            phaseSystem.CurrentPhase.ToString(),
                            accepted: false,
                            reason: result.Reason);
                        return;
                    }
                }

                RecordRewardChosenFlow(
                    choiceKind,
                    chosenDefId,
                    pick.Index,
                    phaseBefore,
                    phaseSystem.CurrentPhase.ToString(),
                    accepted: true,
                    reason: string.Empty);

                // 选完关闭覆盖层；Drain 期间由 PresentationLocked 挡输入（外层已持锁则复用）。
                var acquiredDrainLock = CombatHitSink.TryBeginPresentationLock("RewardDrain");
                CombatHitSink.ChoiceOverlayActive = false;
                try
                {
                    FillBoardDeltaFromEventLog(pipeline, startIndex, out var moves, out var deals, out _, out var removedUids);
                    PresentGoldGainsFromEventLog(startIndex);
                    PresentEffectTriggersFromEventLog(startIndex);
                    var phase = phaseSystem.CurrentPhase;
                    var boardDelta = new PostKillBoardPresentationResult
                    {
                        Accepted = true,
                        Moves = moves ?? Array.Empty<PostKillCardMove>(),
                        Deals = deals ?? Array.Empty<PostKillCardDeal>(),
                        RemovedUids = removedUids ?? Array.Empty<int>(),
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
                        || (boardDelta.Deals != null && boardDelta.Deals.Length > 0)
                        || (boardDelta.RemovedUids != null && boardDelta.RemovedUids.Length > 0))
                    {
                        await DrainPostKillBoardAsync(boardDelta, EnsurePresentationToken());
                    }
                    else
                    {
                        CoreCardPresentationMapper.SyncAllSpawnedCards();
                        UpdateAvatarDebugText();
                        SoftAlignBoardAnchorsToCore();
                        SyncBoardOccupancyFromCore();
                    }

                    RefreshPersistentInBattleUi(animate: false);

                    if (boardDelta.AvatarDefeated)
                    {
                        CombatHitSink.RequestBattleEnded(victory: false);
                        return;
                    }

                    // 整局通关：回主菜单。RoomChoice / NodeCompleted 交主循环继续，不在此宣告胜利。
                    if (phase == GamePhase.Victory)
                    {
                        CombatHitSink.RequestBattleEnded(victory: true);
                    }
                }
                finally
                {
                    if (acquiredDrainLock)
                    {
                        CombatHitSink.EndPresentationLock("RewardDrain");
                    }
                }
            }
            finally
            {
                CombatHitSink.ChoiceOverlayActive = false;
            }
        }

        private static void RecordRewardChosenFlow(
            string choiceKind,
            string chosenDefId,
            int index,
            string phaseBefore,
            string phaseAfter,
            bool accepted,
            string reason)
        {
            try
            {
                FlowTraceRecorder.Record(
                    FlowTraceCategory.CoreGate,
                    FlowTraceNames.RewardChosen,
                    new Dictionary<string, string>
                    {
                        { "kind", choiceKind ?? string.Empty },
                        { "defId", chosenDefId ?? string.Empty },
                        { "index", index.ToString() },
                        { "reason", reason ?? string.Empty },
                    },
                    phaseBefore: phaseBefore,
                    phaseAfter: phaseAfter,
                    accepted: accepted);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[InBattleManager] FlowTrace RewardChosen: " + ex.Message);
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
            bool allowEscapeSkip,
            bool hoverOnNotice = false)
        {
            var picked = -1;
            var pickedDone = false;
            selector.BeginBounceChoice(
                optionDefIds,
                (index, _) =>
                {
                    picked = index;
                    pickedDone = true;
                },
                onFinished: null,
                hoverOnNotice: hoverOnNotice);

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
            CoreCardPresentationMapper.ApplyToManagedCard(card, animate: true);

            var arch = NineGridArchitecture.Current;
            if (arch == null || card == null)
            {
                return;
            }

            var avatarUid = arch.GetModel<BoardModel>().AvatarUid.Value;
            if (avatarUid > 0 && card.Uid == avatarUid)
            {
                PlayerInfoHudPresenter.TryGetInstance()?.SyncFromCore(animate: true);
            }
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

        private const string UnusedHelpCardsGoldReason = "unusedHelpCards";
        private const float UnusedHelpCardVanishDuration = 0.18f;
        private const float UnusedHelpCardStaggerSeconds = 0.07f;

        /// <summary>
        /// 对局结束（通关判定）当拍启动的残留帮助卡结算演出：场上/手牌/卡组中的剩余帮助卡
        /// 逐张错峰并行退场并飞币，不阻塞主循环推进奖励三选一/房间选择。
        /// 结算集合在进入时快照，之后（奖励三选一）新获得的帮助卡视图不会被误清。
        /// </summary>
        public async UniTask PresentUnusedHelpCardSettlementFromEventLogAsync(
            int startIndex,
            CancellationToken cancellationToken = default)
        {
            ResolveManagers();

            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                return;
            }

            var entries = arch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            var unusedDelta = 0;
            var unusedAfter = 0;
            if (entries != null)
            {
                for (var i = Math.Max(0, startIndex); i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (e.Type != CoreEventType.GoldModified
                        || e.Delta <= 0
                        || !string.Equals(e.Message, UnusedHelpCardsGoldReason, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    unusedDelta = e.Delta;
                    unusedAfter = e.Amount;
                    break;
                }
            }

            // 快照本拍应结算的帮助卡（内核区域 + 表现侧手牌/场牌/牌堆/拖拽视图）。
            var settledUids = CollectUnusedHelpCardUidsFromCore();
            AppendPresentationHelpCardUids(settledUids);
            var sweepSet = new HashSet<int>(settledUids);
            if (cardManager != null)
            {
                foreach (var card in cardManager.EnumerateCards())
                {
                    if (card != null
                        && IsPresentationHelpCard(card)
                        && card.DisplayMode != CardDisplayMode.RemovedMode)
                    {
                        sweepSet.Add(card.Uid);
                    }
                }
            }

            if (unusedDelta > 0)
            {
                await PresentUnusedHelpCardsToGoldAsync(
                    settledUids,
                    unusedDelta,
                    unusedAfter,
                    cancellationToken);
                RecordGoldChangedFlow(
                    FlowTraceNames.GoldGained,
                    unusedDelta,
                    unusedAfter,
                    UnusedHelpCardsGoldReason,
                    string.Empty,
                    "ModifyGold");
            }

            // 兜底清掉快照集合中仍存活的帮助卡视图（含手牌），避免下一关前幽灵牌。
            await SweepRemainingHelpCardViewsAsync(sweepSet, cancellationToken);
        }

        /// <summary>
        /// 扫描 EventLog 中的 GoldModified：正 delta 飞币演出，负 delta 静默对齐 HUD，
        /// 并写入 FlowTrace Economy/GoldGained 或 GoldSpent。
        /// </summary>
        /// <param name="skipReason">若与事件 Message 相同则跳过（已由专用演出处理）。</param>
        public static void PresentGoldGainsFromEventLog(
            int startIndex,
            Vector3? originWorld = null,
            string skipReason = null)
        {
            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                return;
            }

            var entries = arch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            if (entries == null || startIndex >= entries.Count)
            {
                return;
            }

            GoldGainFxManagerSingleton.TryGetInstance(out var goldFx);

            for (var i = Math.Max(0, startIndex); i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type != CoreEventType.GoldModified || e.Delta == 0)
                {
                    continue;
                }

                // 残留帮助卡金币由通关当拍的专用逐张演出接管，通用扫描一律跳过，避免双飞/抢占目标值。
                if (string.Equals(e.Message, UnusedHelpCardsGoldReason, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(skipReason)
                    && string.Equals(e.Message, skipReason, StringComparison.Ordinal))
                {
                    continue;
                }

                if (e.Delta < 0)
                {
                    goldFx?.SnapToCore(e.Amount);
                    RecordGoldChangedFlow(
                        FlowTraceNames.GoldSpent,
                        e.Delta,
                        e.Amount,
                        e.Message,
                        e.SourceDefId,
                        e.ActionName);
                    continue;
                }

                var origin = originWorld;
                if (!origin.HasValue && e.CardUid > 0)
                {
                    origin = ResolveCardWorldPosition(e.CardUid);
                }

                if (!origin.HasValue && e.TargetUid > 0)
                {
                    origin = ResolveCardWorldPosition(e.TargetUid);
                }

                goldFx?.PlayGain(e.Delta, e.Amount, origin);
                RecordGoldChangedFlow(
                    FlowTraceNames.GoldGained,
                    e.Delta,
                    e.Amount,
                    e.Message,
                    e.SourceDefId,
                    e.ActionName);
            }
        }

        private async UniTask PresentUnusedHelpCardsToGoldAsync(
            List<int> uids,
            int totalDelta,
            int amountAfter,
            CancellationToken cancellationToken)
        {
            var goldPerCard = ResolveUnusedHelpCardGoldPerCard();
            if (goldPerCard <= 0 && uids.Count > 0)
            {
                goldPerCard = Math.Max(1, totalDelta / uids.Count);
            }

            var snapshots = new List<(ManagedCard card, Vector3 origin)>(uids.Count);
            if (cardManager != null)
            {
                for (var i = 0; i < uids.Count; i++)
                {
                    if (!cardManager.TryGet(uids[i], out var card)
                        || card?.Transform == null
                        || card.DisplayMode == CardDisplayMode.RemovedMode)
                    {
                        continue;
                    }

                    snapshots.Add((card, card.Transform.position));
                }
            }

            // 先卸占位（手牌整清，保证手牌中的也退场），再并行退场飞币。
            CardHandManagerSingleton.Instance?.ClearHand();
            for (var i = 0; i < snapshots.Count; i++)
            {
                var card = snapshots[i].card;
                fieldManager?.TryClearOccupancyForUid(card.Uid, skipBusyGuard: true);
                deckManager?.TryDetachByUid(card.Uid, out _);
            }

            GoldGainFxManagerSingleton.TryGetInstance(out var goldFx);
            var before = Math.Max(0, amountAfter - totalDelta);
            var credited = 0;

            // 并行结算：每张卡仅错峰起始（轻微先后顺序），不再串行等待上一张退场完成。
            var tasks = new List<UniTask>(snapshots.Count);
            for (var i = 0; i < snapshots.Count; i++)
            {
                var (card, origin) = snapshots[i];
                var remaining = totalDelta - credited;
                var slice = remaining <= 0
                    ? 0
                    : Math.Min(goldPerCard > 0 ? goldPerCard : remaining, remaining);
                credited += slice;
                tasks.Add(VanishHelpCardWithGoldAsync(
                    card,
                    origin,
                    slice,
                    before + credited,
                    UnusedHelpCardStaggerSeconds * i,
                    goldFx,
                    cancellationToken));
            }

            await UniTask.WhenAll(tasks);

            if (credited < totalDelta)
            {
                goldFx?.PlayGain(totalDelta - credited, amountAfter, ResolveDeckGoldOriginWorld());
            }
        }

        private async UniTask VanishHelpCardWithGoldAsync(
            ManagedCard card,
            Vector3 origin,
            int goldSlice,
            int goldTargetAfterSlice,
            float startDelaySeconds,
            GoldGainFxManagerSingleton goldFx,
            CancellationToken cancellationToken)
        {
            if (startDelaySeconds > 0f)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(startDelaySeconds),
                    cancellationToken: cancellationToken);
            }

            if (goldSlice > 0)
            {
                goldFx?.PlayGain(goldSlice, goldTargetAfterSlice, origin);
            }

            await VanishAndReleaseHelpCardAsync(card, cancellationToken);
        }

        private async UniTask SweepRemainingHelpCardViewsAsync(
            HashSet<int> restrictToUids,
            CancellationToken cancellationToken)
        {
            CardHandManagerSingleton.Instance?.ClearHand();
            if (cardManager == null)
            {
                return;
            }

            var leftovers = new List<ManagedCard>();
            foreach (var card in cardManager.EnumerateCards())
            {
                if (card == null || !IsPresentationHelpCard(card))
                {
                    continue;
                }

                if (card.DisplayMode == CardDisplayMode.RemovedMode || card.Transform == null)
                {
                    continue;
                }

                // 只清结算快照内的卡：奖励三选一新获得的帮助卡视图不受影响。
                if (restrictToUids != null && !restrictToUids.Contains(card.Uid))
                {
                    continue;
                }

                leftovers.Add(card);
            }

            for (var i = 0; i < leftovers.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var card = leftovers[i];
                fieldManager?.TryClearOccupancyForUid(card.Uid, skipBusyGuard: true);
                deckManager?.TryDetachByUid(card.Uid, out _);
                await VanishAndReleaseHelpCardAsync(card, cancellationToken);
            }
        }

        private async UniTask VanishAndReleaseHelpCardAsync(
            ManagedCard card,
            CancellationToken cancellationToken)
        {
            if (card == null)
            {
                return;
            }

            var uid = card.Uid;
            if (cardManager == null || !cardManager.TryGet(uid, out card))
            {
                return;
            }

            if (card.Transform == null)
            {
                cardManager.Release(uid, "HelpCard.VanishNoTransform");
                return;
            }

            cardManager?.SetDisplayMode(card, CardDisplayMode.RemovedMode);
            CardDeckTween.KillMotion(card.Transform);

            var transform = card.Transform;
            Tween tween = null;
            try
            {
                var completed = false;
                tween = transform
                    .DOScale(Vector3.zero, UnusedHelpCardVanishDuration)
                    .SetEase(Ease.InBack)
                    .SetLink(transform.gameObject, LinkBehaviour.KillOnDestroy)
                    .OnComplete(() => completed = true)
                    .OnKill(() => completed = true);

                while (!completed)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // 取消路径仍 Release，避免幽灵视图。
            }
            finally
            {
                if (tween != null && tween.IsActive())
                {
                    tween.Kill();
                }

                CardOpacityUtility.ClearCache(uid);
                cardManager?.Release(uid, "HelpCard.VanishComplete");
            }
        }

        private static List<int> CollectUnusedHelpCardUidsFromCore()
        {
            var result = new List<int>();
            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                return result;
            }

            var registry = arch.GetModel<CardRegistry>();
            var board = arch.GetModel<BoardModel>();
            var deck = arch.GetModel<DeckModel>();
            var seen = new HashSet<int>();

            AddHelpCardUids(registry, deck.DrawPileUids, seen, result);
            AddHelpCardUids(registry, deck.PlayerCardPoolUids, seen, result);
            AddHelpCardUids(registry, deck.ItemSlotUids, seen, result);
            foreach (var uid in board.BoardCardUids())
            {
                AddHelpCardUid(registry, uid, seen, result);
            }

            return result;
        }

        private void AppendPresentationHelpCardUids(List<int> uids)
        {
            if (uids == null || cardManager == null)
            {
                return;
            }

            var seen = new HashSet<int>(uids);
            foreach (var card in cardManager.EnumerateCards())
            {
                if (card == null || !IsPresentationHelpCard(card) || !seen.Add(card.Uid))
                {
                    continue;
                }

                if (card.DisplayMode == CardDisplayMode.HandCardMode
                    || card.DisplayMode == CardDisplayMode.GroundCardMode
                    || card.DisplayMode == CardDisplayMode.CardDeckMode
                    || card.DisplayMode == CardDisplayMode.DragCardMode)
                {
                    uids.Add(card.Uid);
                }
            }
        }

        private static void AddHelpCardUids(
            CardRegistry registry,
            IReadOnlyList<int> source,
            HashSet<int> seen,
            List<int> result)
        {
            if (source == null)
            {
                return;
            }

            for (var i = 0; i < source.Count; i++)
            {
                AddHelpCardUid(registry, source[i], seen, result);
            }
        }

        private static void AddHelpCardUid(
            CardRegistry registry,
            int uid,
            HashSet<int> seen,
            List<int> result)
        {
            if (uid == 0 || registry == null || !seen.Add(uid))
            {
                return;
            }

            if (!registry.TryGet(uid, out var card) || card.Kind != CardKind.HelpCard)
            {
                seen.Remove(uid);
                return;
            }

            result.Add(uid);
        }

        private static bool IsPresentationHelpCard(ManagedCard card)
        {
            if (card == null)
            {
                return false;
            }

            if (card.CoreKind == CardPresentationKind.HelpCard)
            {
                return true;
            }

            return !string.IsNullOrEmpty(card.DefId)
                && card.DefId.StartsWith("help.", StringComparison.OrdinalIgnoreCase);
        }

        private static int ResolveUnusedHelpCardGoldPerCard()
        {
            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                return 0;
            }

            var content = arch.GetSystem<IContentSystem>();
            content?.TryReloadFromConfig();
            if (content == null || !content.HasCatalog)
            {
                return 0;
            }

            return Math.Max(0, content.Catalog.Economy.UnusedHelpCardGold);
        }

        private Vector3? ResolveDeckGoldOriginWorld()
        {
            if (cardManager == null)
            {
                return null;
            }

            foreach (var card in cardManager.EnumerateCards())
            {
                if (card?.Transform != null && card.DisplayMode == CardDisplayMode.CardDeckMode)
                {
                    return card.Transform.position;
                }
            }

            return null;
        }

        private static void RecordGoldChangedFlow(
            string eventName,
            int delta,
            int amountAfter,
            string reason,
            string sourceDefId,
            string actionName)
        {
            try
            {
                if (!FlowTraceRecorder.Enabled)
                {
                    return;
                }

                FlowTraceRecorder.BeginSessionIfNeeded();
                FlowTraceRecorder.Record(
                    FlowTraceCategory.Economy,
                    eventName,
                    new Dictionary<string, string>
                    {
                        { "delta", delta.ToString() },
                        { "amountAfter", amountAfter.ToString() },
                        { "reason", reason ?? string.Empty },
                        { "sourceDefId", sourceDefId ?? string.Empty },
                        { "action", actionName ?? string.Empty },
                    },
                    refBattleOpIndex: BattleTraceRecorder.LastOpIndex);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[InBattleManager] FlowTrace " + eventName + ": " + ex.Message);
            }
        }

        private static Vector3? ResolveCardWorldPosition(int cardUid)
        {
            if (cardUid <= 0)
            {
                return null;
            }

            var cards = CardManagerSingleton.TryGetInstance();
            if (cards != null
                && cards.TryGet(cardUid, out var view)
                && view?.Transform != null)
            {
                return view.Transform.position;
            }

            return null;
        }

        private static Vector3? ResolveBoardSlotWorldPosition(int groundSlot)
        {
            var field = GroundFieldManagerSingleton.Instance;
            if (field == null)
            {
                return null;
            }

            if (field.TryGetCardAt(groundSlot, out var card) && card?.Transform != null)
            {
                return card.Transform.position;
            }

            // 空格探求：尽量用格锚点；无则交给 GoldFx 默认屏幕中心。
            var anchor = field.GetGroundAnchor(groundSlot);
            if (anchor != null)
            {
                return anchor.position;
            }

            return null;
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

            var previousBatch = FieldTraceHelper.CurrentBatchTag;
            var openedSyncBeat = false;
            if (string.IsNullOrEmpty(previousBatch))
            {
                FieldTraceHelper.SetBatchTag(FlowTraceBatchTags.Sync);
                var syncNode = 0;
                int.TryParse(FieldTraceHelper.ResolveNodeIndex(), out syncNode);
                PerfTraceRecorder.OpenBeat(DiagBeatKinds.SyncBoard, syncNode);
                openedSyncBeat = true;
            }

            FieldTraceHelper.RecordOccupancySnapshot("syncBefore");
            cardManager?.AuditRegistryIntegrity("Sync.Before");

            var vacated = 0;
            var placed = 0;
            var spawned = 0;
            var vacatedUids = new List<int>(4);
            var placedUids = new List<int>(4);
            var spawnedUids = new List<int>(4);
            var sweptUids = new List<int>(4);

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
                    vacated++;
                    vacatedUids.Add(occ.Uid);
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
                    vacated++;
                    vacatedUids.Add(occ.Uid);
                }
                else
                {
                    fieldManager.RequestRemoveFromField(
                        occ.Uid,
                        animate: false,
                        skipBusyGuard: true,
                        startExplore: false);
                    vacated++;
                    vacatedUids.Add(occ.Uid);
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

                if (!registry.TryGet(uid, out var coreCard))
                {
                    continue;
                }

                // Core 已离场：禁止 Spawn 幽灵视图。
                if (coreCard.Zone.Value == ZoneId.Graveyard
                    || coreCard.Zone.Value == ZoneId.Removed)
                {
                    Debug.LogWarning(
                        $"[InBattleManager] Sync 跳过 Spawn：uid={uid} zone={coreCard.Zone.Value}（Board 占格与 Zone 不一致）");
                    continue;
                }

                if (fieldManager.TryGetCardAt(slot, out var existing) && existing != null && existing.Uid == uid)
                {
                    if (existing.IsFieldDead)
                    {
                        fieldManager.ClearSlotOccupancy(slot, skipBusyGuard: true);
                        cardManager.Release(existing, "Sync.FieldDeadOnSlot");
                        vacated++;
                        vacatedUids.Add(uid);
                    }
                    else
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
                }

                if (cardManager.TryGet(uid, out var view) && view != null)
                {
                    // 已标死的视图禁止复用落锚，先 Release 再走下方 Spawn。
                    if (view.IsFieldDead)
                    {
                        if (fieldManager.TryGetSlotOf(view.Uid, out var deadSlot))
                        {
                            fieldManager.ClearSlotOccupancy(deadSlot, skipBusyGuard: true);
                        }

                        cardManager.Release(view, "Sync.FieldDeadReuse");
                    }
                    else
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
                                placed++;
                                placedUids.Add(uid);
                            }
                            else
                            {
                                var anchor = fieldManager.GetGroundAnchor(slot);
                                if (view.Transform != null
                                    && anchor != null
                                    && (view.Transform.position - anchor.position).sqrMagnitude > 0.0001f)
                                {
                                    CardDeckTween.KillMotion(view.Transform);
                                    view.Transform.position = anchor.position;
                                    cardManager.RefreshDisplayMode(view);
                                }
                            }
                        }
                        else
                        {
                            fieldManager.RequestPlaceCardAtAnchor(
                                slot,
                                view,
                                skipBusyGuard: true,
                                snapToAnchor: true);
                            placed++;
                            placedUids.Add(uid);
                        }

                        continue;
                    }
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
                spawned++;
                spawnedUids.Add(uid);
                placed++;
                placedUids.Add(uid);
            }

            var swept = SweepOrphanCardViews(avatarUid, hand, sweptUids);
            CoreCardPresentationMapper.SyncAllSpawnedCards();
            UpdateAvatarDebugText();

            FieldTraceHelper.RecordSyncDiff(
                vacated,
                placed,
                spawned,
                swept,
                string.Join(",", vacatedUids),
                string.Join(",", placedUids),
                string.Join(",", spawnedUids),
                string.Join(",", sweptUids));
            FieldTraceHelper.RecordOccupancySnapshot("syncAfter");
            cardManager?.AuditRegistryIntegrity("Sync.After");

            if (openedSyncBeat)
            {
                PerfTraceRecorder.CloseBeat();
            }

            if (string.IsNullOrEmpty(previousBatch))
            {
                FieldTraceHelper.ClearBatchTag();
            }
        }

        /// <summary>
        /// 清扫已不在手牌/卡组/场地占格、且 Core 为 Graveyard/Removed（或 registry 无）的游离视图。
        /// 堵住「Register 挤占后 Sync 只扫占格表」漏掉的尸体钉住。
        /// </summary>
        private int SweepOrphanCardViews(
            int avatarUid,
            CardHandManagerSingleton hand,
            List<int> sweptUids = null)
        {
            ResolveManagers();
            if (cardManager == null || fieldManager == null)
            {
                return 0;
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

                // Pickup 进行中：手牌尚未 ContainsUid 的窗口，勿误 Sweep。
                if (hand != null
                    && hand.IsBusy
                    && view.DisplayMode == CardDisplayMode.HandCardMode)
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
                cardManager.Release(uid, "Sync.SweepOrphan");
                sweptUids?.Add(uid);
            }

            return toRelease.Count;
        }

        private static void OnNodeSettlementFromCombat()
        {
            Instance?.TryEnterNodeSettlement();
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
