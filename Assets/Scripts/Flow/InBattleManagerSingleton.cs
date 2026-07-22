using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using NineGrid.Cards;
using NineGrid.Cards.Convergence;
using NineGrid.Core;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 游戏局内管理器单例：Core 桥注册 + 薄 Present 适配（入场/结算钩子、导演宿主）。
    /// #11 硬切后局内表演编排唯一出口为 <see cref="PresentationDirector"/>；本类不再承载进攻编排剧本。
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

        [Tooltip("面板路由；留空则运行时在同物体或场景中查找 UiPanelRouter。")]
        [SerializeField] private UiPanelRouter panelRouter;

        private const string StatBoostCardDefId = "help.stat_boost_card";
        private const int BoardSelectLockWaitMs = 3000;
        private static readonly string[] StatBoostOptions = { "Attack", "Armor", "Hp" };

        private bool _isBusy;
        private bool _settlementRaised;
        private bool _fieldSignalSubscribed;
        private bool _drainInFlight;
        private bool _pendingSyncFromCore;
        private int _boardPresentationRequestId;
        private CancellationTokenSource _presentationCts;
        private int _nodeEventLogStart;
        private readonly ShuffleIntoDeckPresentSink _shuffleIntoSink = new ShuffleIntoDeckPresentSink();
        private Transform _shuffleOriginScratch;
        private readonly Dictionary<int, HashSet<int>> _pendingFusionRemoves = new();
        private readonly HashSet<int> _completedFusionActionIds = new();
        private PresentationDirector _presentationDirector;
        private DirectorIntentRuntime _directorIntentRuntime;
        private IUnRegister _exploreRejectedUnRegister;
        private IUnRegister _ensureDirectorUnRegister;
        private bool _recoveringRewardUi;
        private QueuedBoardPresentChannel _explorePresentChannel;
        private CombatAttackPresentChannel _attackHitPresentChannel;
        private CombatCounterPresentChannel _attackCounterPresentChannel;
        private QueuedBoardPresentChannel _attackBoardPresentChannel;
        private UseItemPresentChannel _useItemPresentChannel;
        private QueuedBoardPresentChannel _useItemBoardPresentChannel;
        private CoreCommandDispatcher _coreCommandDispatcher;
        private int _pendingAttackSlot;
        private UseItemPresentationResult _pendingUseItemPresent;

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
        /// DevTest：将 Avatar Attack 设为指定值并刷新表现。
        /// </summary>
        public bool TryCheatSetAvatarAttack(int attack)
        {
            if (attack < 0)
            {
                return false;
            }

            var arch = NineGridArchitecture.Current;
            var board = arch.GetModel<BoardModel>();
            var avatarUid = board.AvatarUid.Value;
            if (avatarUid <= 0
                || !arch.GetModel<CardRegistry>().TryGet(avatarUid, out var avatar))
            {
                Debug.LogWarning("[InBattleManager] Avatar 不存在，无法改攻。");
                return false;
            }

            avatar.Stats.SetBase(StatId.Attack, attack);

            ResolveManagers();
            if (cardManager != null
                && cardManager.TryGet(avatarUid, out var view)
                && view != null)
            {
                CoreCardPresentationMapper.ApplyToManagedCard(view);
            }

            UpdateAvatarDebugText();
            PlayerInfoHudPresenter.TryGetInstance()?.SyncFromCore(animate: false);
            Debug.Log($"[InBattleManager] Avatar#{avatarUid} Attack → {attack}");
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
        /// [DeckProbe] 记录 StartNode 前 NodeDeckOptions 组成。
        /// </summary>
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
                // 失败重开/清场竞态可能留下粘连忙碌；强制复位后继续，避免空场干等结算。
                Debug.LogWarning("[InBattleManager] StartBattleNode：检测到粘连忙碌，强制复位后继续。");
                CancelPresentationWork();
                _drainInFlight = false;
                _isBusy = false;
                CombatHitSink.ForceEndPresentationLock("StartBattleNode.staleBusy");
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
                EnsurePresentationDirector();

                var arch = NineGridArchitecture.Current;
                var phase = arch.GetSystem<IPhaseSystem>();
                if (!phase.CanExecute(GameCommandKind.StartNode))
                {
                    Debug.LogWarning(
                        $"[InBattleManager] StartNode 非法 phase={phase.CurrentPhase}，先 BootstrapRun。");
                    BootstrapRun();
                }

                options ??= NodeDeckOptions.CreateDefaultBattle();
                LogDeckOptionsProbe(options);

                // 记录本节点 EventLog 起点：结算演出据此定位 unusedHelpCards 金币事件。
                _nodeEventLogStart = arch.GetSystem<IActionPipelineSystem>().EventLog.Entries.Count;
                var result = phase.StartNode(options);
                if (!result.Accepted)
                {
                    Debug.LogError($"[InBattleManager] StartNode 被拒: {result.Reason}");
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

                var view = cardManager.SpawnView(
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
                if (cardManager.TryGet(placement.Uid, out _))
                {
                    continue;
                }

                var view = cardManager.SpawnView(
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

                var view = cardManager.SpawnView(
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

        private bool TryResolveHandDealOrigin(string sourceDefId, out Transform origin)
        {
            origin = null;
            if (string.IsNullOrEmpty(sourceDefId))
            {
                return false;
            }

            if (sourceDefId.StartsWith("relic.", StringComparison.Ordinal))
            {
                return relicManager != null && relicManager.TryGetDealOrigin(sourceDefId, out origin);
            }

            return false;
        }

        private async UniTask PresentOpeningAsync(
            OpeningPresentationPlan plan,
            CancellationToken cancellationToken)
        {
            CombatHitSink.OpeningPresentationActive = true;
            try
            {
            if (plan.DeckCards.Count > 0)
            {
                deckManager.ResetToStandby();
                deckManager.InjectDeck(plan.DeckCards);
                await deckManager.BeginEntryAsync(cancellationToken);
            }

            if (plan.AvatarUid > 0)
            {
                var avatarDefId = string.IsNullOrEmpty(plan.AvatarDefId)
                    ? CardManagerSingleton.StandardDefId
                    : plan.AvatarDefId;
                var avatar = cardManager.SpawnView(
                    plan.AvatarUid,
                    avatarDefId,
                    initialMode: CardDisplayMode.GroundCardMode,
                    kind: CardPresentationKind.Avatar);

                if (avatar != null)
                {
                    CoreCardPresentationMapper.ApplyToManagedCard(avatar);
                    await fieldManager.RequestRevealAvatarAsync(avatar, cancellationToken);

                    // 揭示被场地自身 busy 拒掉时：强制 skipBusyGuard 落格 5，避免孤儿视图卡在牌堆坐标。
                    if (!fieldManager.TryGetSlotOf(avatar.Uid, out var avatarSlot)
                        || avatarSlot != GroundSlotTopology.AvatarReservedSlot)
                    {
                        Debug.LogWarning(
                            $"[InBattleManager] Opening Avatar 未占格5，skipBusyGuard 兜底落位 uid={avatar.Uid}。");
                        fieldManager.RequestPlaceCardAtAnchor(
                            GroundSlotTopology.AvatarReservedSlot,
                            avatar,
                            skipBusyGuard: true,
                            snapToAnchor: true);
                    }
                }
            }

            // 按内核盘面 uid→slot 就位：走卡组管理器完整发牌缓动（与 DealOpeningRing 同轨迹）。
            var ring = GroundSlotTopology.ClockwiseRing;
            var dealInterval = deckManager.LayoutSettings != null
                ? deckManager.LayoutSettings.dealInterval
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

                    cardManager.TryGet(placement.Uid, out var ensureCard);
                    var flightContext = new DealFlightContext(
                        fieldManager.IsFieldBusy,
                        fieldManager.ActiveDealFlightCount + flightHandles.Count + 1,
                        pendingRotateSteps: 0);
                    var (ok, handle) = await deckManager.DealCardByUidWithFlightAsync(
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
                            $"[InBattleManager] 就位失败 uid={placement.Uid} slot={placement.GroundSlot}，继续其余格并由 Sync 兜底。");
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
                    await GroundFieldManagerSingleton.WaitDealFlightsSettledAsync(
                        flightHandles,
                        cancellationToken);
                }

                if (plan.DeckAdds.Count > 0)
                {
                    ResolveManagers();
                    for (var d = 0; d < plan.DeckAdds.Count; d++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var deckDeal = plan.DeckAdds[d];
                        if (!TryResolveHandDealOrigin(deckDeal.SourceDefId, out var origin))
                        {
                            if (deckManager == null || !deckManager.TryGetDefaultDealOrigin(out origin))
                            {
                                Debug.LogWarning(
                                    $"[InBattleManager] Opening DeckAdd uid={deckDeal.Uid} source={deckDeal.SourceDefId} 无锚点，跳过。");
                                continue;
                            }

                            Debug.LogWarning(
                                $"[InBattleManager] Opening DeckAdd uid={deckDeal.Uid} source={deckDeal.SourceDefId} 锚点未找到，fallback 卡组左侧。");
                        }

                        cardManager.TryGet(deckDeal.Uid, out var ensureCard);
                        if (ensureCard == null)
                        {
                            ensureCard = cardManager.SpawnView(
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

                        var deckOk = await deckManager.AddCardAtFromOriginAsync(
                            deckManager.DeckCount,
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
                                $"[InBattleManager] Opening DeckAdd 失败 uid={deckDeal.Uid} source={deckDeal.SourceDefId}。");
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
                    ResolveManagers();
                    for (var h = 0; h < plan.HandDeals.Count; h++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var handDeal = plan.HandDeals[h];
                        if (!TryResolveHandDealOrigin(handDeal.SourceDefId, out var origin))
                        {
                            if (deckManager == null || !deckManager.TryGetDefaultDealOrigin(out origin))
                            {
                                Debug.LogWarning(
                                    $"[InBattleManager] Opening HandDeal uid={handDeal.Uid} source={handDeal.SourceDefId} 无锚点，跳过。");
                                continue;
                            }

                            Debug.LogWarning(
                                $"[InBattleManager] Opening HandDeal uid={handDeal.Uid} source={handDeal.SourceDefId} 锚点未找到，fallback 卡组左侧。");
                        }

                        cardManager.TryGet(handDeal.Uid, out var ensureCard);
                        if (ensureCard == null)
                        {
                            ensureCard = cardManager.SpawnView(
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

                        var handOk = await deckManager.DealCardToHandAsync(
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
                                $"[InBattleManager] Opening HandDeal 失败 uid={handDeal.Uid} source={handDeal.SourceDefId}。");
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
                CombatHitSink.OpeningPresentationActive = false;
                // #10：开局不再靠 Sync 自愈占格镜像；几何登记由发牌表演维护，合法性由 Flow idle 裁决。
                // 开局表演收束：编排主线全场 Commit 投影（非旁路 Set*）。
                CoreCardPresentationMapper.CommitAllSpawnedCards();
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
            // Bounce 退场 DelayedCall 可能跨节点；清场前必须先终止选择会话，避免陈旧句柄误删新视图。
            SelectorManagerSingleton.TryGetInstance()?.HideChoice();
            // 先取消交战/手牌异步，再强制清占格与手牌槽，最后统一 Release 视图。
            FieldBattleManagerSingleton.Instance?.CancelBattleWork();
            CombatHitSink.ResetInputGates("ResetCardPresentationSurface");
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

                // 勿用 hand.IsBusy / field.IsBusy 聚合（含 DirectorMainlineBusy），避免静态镜像粘连假忙。
                var fieldBusy = fieldManager != null && fieldManager.IsFieldBusy;
                var hand = CardHandManagerSingleton.Instance;
                var handSelfBusy = hand != null && hand.IsSelfBusy;
                var deckBusy = deckManager != null && deckManager.IsBusy;
                var battle = FieldBattleManagerSingleton.Instance;
                var battleBusy = battle != null && battle.IsBusy;

                if (!_drainInFlight
                    && !fieldBusy
                    && !handSelfBusy
                    && !deckBusy
                    && !battleBusy
                    && (_presentationDirector == null || !_presentationDirector.IsMainlineBusy))
                {
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            Debug.LogWarning(
                $"[InBattleManager] WaitPresentationIdle 超时({timeoutSeconds:0.##}s)：drain={_drainInFlight} " +
                $"fieldBusy={fieldManager != null && fieldManager.IsFieldBusy} " +
                $"directorBusy={_presentationDirector != null && _presentationDirector.IsMainlineBusy}，继续清场。");
        }

        private void CancelPresentationWork()
        {
            if (_presentationCts != null)
            {
                _presentationCts.Cancel();
                _presentationCts.Dispose();
                _presentationCts = null;
            }

            _shuffleIntoSink.Clear();

            CombatHitSink.ForceEndPresentationLock("CancelPresentationWork");
            TeardownPresentationDirector(IntentClearReason.LayerChange);
            // 导演硬清不会 FinishBatch；必须清核心 PresentationSync，否则 IsInputLocked
            // 粘连会使 BuildEnemyPool 下 StartNode 非法。
            try
            {
                NineGridArchitecture.Current.GetSystem<IPresentationSyncSystem>().Clear();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[InBattleManager] Clear PresentationSync: " + ex.Message);
            }

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
            CombatHitSink.ResolvePlayerAttackTarget = ResolvePlayerAttackTargetFromCore;
            CombatHitSink.SyncCardPresentation = SyncManagedCardPresentation;
            CombatHitSink.SpawnDamageNumber = SpawnDamageNumberAt;
            CombatHitSink.SyncBoardFromCore = RequestSyncBoardFromCore;
            CombatHitSink.DrainPostKillBoard = DrainPostKillBoardAsync;
            CombatHitSink.FlushPendingShuffleIntoPresentation = FlushPendingShuffleIntoPresentationAsync;
            CombatHitSink.ApplyPickupItem = ApplyPickupItemFromCore;
            CombatHitSink.TrySubmitAttackIntent = TrySubmitAttackIntentFromCards;
            CombatHitSink.TrySubmitUseItemIntent = TrySubmitUseItemIntentFromCards;
            CombatHitSink.NotifyBattleEnded = OnBattleEndedFromCombat;
            CombatHitSink.NotifyNodeSettlementReady = OnNodeSettlementFromCombat;
            FieldTraceHelper.RegisterSinkHandlers();
            PerfTraceRecorder.RegisterSinkHandlers();
            RegistryTraceRecorder.RegisterSinkHandlers();
            RegisterCardZoneOwnershipSink();
            RegisterHandBridge();
            RegisterExplorePresentationHandlers();
        }

        private void RegisterCardZoneOwnershipSink()
        {
            CardZoneOwnershipSink.IsCoreItemSlots = IsCoreItemSlotsZone;
            CardZoneOwnershipSink.IsCoreDrawPile = IsCoreDrawPileZone;
        }

        private static bool IsCoreItemSlotsZone(int uid)
        {
            if (uid <= 0)
            {
                return false;
            }

            var arch = NineGridArchitecture.Current;
            return arch.GetModel<CardRegistry>().TryGet(uid, out var card)
                   && card.Zone.Value == ZoneId.ItemSlots;
        }

        private static bool IsCoreDrawPileZone(int uid)
        {
            if (uid <= 0)
            {
                return false;
            }

            var arch = NineGridArchitecture.Current;
            return arch.GetModel<CardRegistry>().TryGet(uid, out var card)
                   && card.Zone.Value == ZoneId.DrawPile;
        }

        private void UnregisterCombatHitSink()
        {
            FieldTraceHelper.UnregisterSinkHandlers();
            PerfTraceRecorder.UnregisterSinkHandlers();
            RegistryTraceRecorder.UnregisterSinkHandlers();
            CardZoneOwnershipSink.Reset();
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

            if (CombatHitSink.ResolvePlayerAttackTarget == ResolvePlayerAttackTargetFromCore)
            {
                CombatHitSink.ResolvePlayerAttackTarget = null;
            }

            if (CombatHitSink.SyncCardPresentation == SyncManagedCardPresentation)
            {
                CombatHitSink.SyncCardPresentation = null;
            }

            if (CombatHitSink.SpawnDamageNumber == SpawnDamageNumberAt)
            {
                CombatHitSink.SpawnDamageNumber = null;
            }

            if (CombatHitSink.SyncBoardFromCore == RequestSyncBoardFromCore)
            {
                CombatHitSink.SyncBoardFromCore = null;
            }

            if (CombatHitSink.DrainPostKillBoard == DrainPostKillBoardAsync)
            {
                CombatHitSink.DrainPostKillBoard = null;
            }

            if (CombatHitSink.FlushPendingShuffleIntoPresentation == FlushPendingShuffleIntoPresentationAsync)
            {
                CombatHitSink.FlushPendingShuffleIntoPresentation = null;
            }

            CombatHitSink.ForceEndPresentationLock("UnregisterCombatHitSink");
            _drainInFlight = false;

            if (CombatHitSink.ApplyPickupItem == ApplyPickupItemFromCore)
            {
                CombatHitSink.ApplyPickupItem = null;
            }

            UnregisterExplorePresentationHandlers();

            if (CombatHitSink.TrySubmitAttackIntent == TrySubmitAttackIntentFromCards)
            {
                CombatHitSink.TrySubmitAttackIntent = null;
            }

            if (CombatHitSink.TrySubmitUseItemIntent == TrySubmitUseItemIntentFromCards)
            {
                CombatHitSink.TrySubmitUseItemIntent = null;
            }

            CombatHitSink.DirectorMainlineBusy = false;
            TeardownPresentationDirector(IntentClearReason.LayerChange);

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
            AbortBoardSelectIfActive("unregister-hand-bridge");
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
                            presentation = new BattleTracePresentation
                            {
                                accepted = false,
                                rejectReason = result.Reason ?? string.Empty,
                            },
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
                            avatarHpAfter: targetSnap != null ? targetSnap.hp : -1,
                            rejectReason: result.Reason);
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
            Instance?.PresentShuffleIntoDeckFromEventLog(startIndex);

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

            FillBoardDeltaFromEventLog(pipeline, startIndex, out var moves, out var deals, out _, out var removedUids, out var steps);
            summary.Steps = steps;
            summary.Moves = moves;
            summary.Deals = deals;
            summary.RemovedUids = removedUids;

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
            int avatarHpAfter,
            string rejectReason = null)
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
                        { "rejectReason", rejectReason ?? string.Empty },
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

            FillBoardDeltaFromEventLog(pipeline, startIndex, out var moves, out var deals, out _, out var removedUids, out var steps);
            summary.Steps = steps;
            summary.Moves = moves;
            summary.Deals = deals;
            summary.RemovedUids = removedUids;
            summary.DamagePopups = CollectDamagePopups(pipeline.EventLog.Entries, startIndex);
            PresentGoldGainsFromEventLog(startIndex);
            PresentEffectTriggersFromEventLog(startIndex);
            Instance?.PresentShuffleIntoDeckFromEventLog(startIndex);

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
        /// #11 硬切后的 Present 薄适配：直接播盘面 delta，不再经 BoardPresentationQueue 泵。
        /// 导演主线已串行 Present；外部薄适配经主线租约持忙。
        /// </summary>
        private async UniTask DrainPostKillBoardAsync(
            PostKillBoardPresentationResult result,
            CancellationToken cancellationToken)
        {
            if (!result.Accepted)
            {
                return;
            }

            while (_drainInFlight)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            var acquiredHere = false;
            if (!CombatHitSink.PresentationLocked)
            {
                if (!CombatHitSink.TryBeginPresentationLock("BoardPresentDrain"))
                {
                    Debug.LogWarning("[InBattleManager] 盘面 Present 无法获取表现锁，跳过。");
                    return;
                }

                acquiredHere = true;
            }

            _drainInFlight = true;
            ChoreoTraceContext.DrainInFlight = true;
            ChoreoTraceContext.PumpRunning = false;
            ChoreoTraceContext.BoardQueueDepth = 0;
            try
            {
                FieldTraceHelper.SetBatchTag(FlowTraceBatchTags.BoardPresentationQueue);
                await DrainPostKillBoardCoreAsync(result, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[InBattleManager] 盘面 Present 失败: " + ex.Message);
                _pendingSyncFromCore = true;
            }
            finally
            {
                FieldTraceHelper.ClearBatchTag();
                _drainInFlight = false;
                ChoreoTraceContext.DrainInFlight = false;
                if (acquiredHere)
                {
                    CombatHitSink.EndPresentationLock("BoardPresentDrain");
                }

                FlushDeferredBoardSync(force: true);
            }
        }

        /// <summary>
        /// 单条盘面 delta 缓释：保序步骤流逐步 await，或回退扁平 Deals/Moves。
        /// 位移类 Step 经 <see cref="BoardMotionStepScheduler"/> 供给执行层；就位由五次收敛 + 栅栏保证，无硬 snap。
        /// 锁由队列泵或外层交战流程持有，本方法不再重复加解锁。
        /// </summary>
        private async UniTask DrainPostKillBoardCoreAsync(
            PostKillBoardPresentationResult result,
            CancellationToken cancellationToken)
        {
            if (!result.Accepted)
            {
                return;
            }

            var stepCount = result.Steps?.Length ?? 0;
            var moveCount = stepCount > 0 ? stepCount : (result.Moves?.Length ?? 0);
            var dealCount = result.Deals?.Length ?? 0;
            var requestId = ++_boardPresentationRequestId;
            FieldTraceHelper.SetBatchTag(FlowTraceBatchTags.BoardPresentationQueue);
            var drainNode = 0;
            int.TryParse(FieldTraceHelper.ResolveNodeIndex(), out drainNode);
            PerfTraceRecorder.OpenBeat(DiagBeatKinds.PostKillDrain, drainNode);
            OperationCanceledException pendingCancel = null;
            var ranDrainBody = false;
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
                }
                else
                {
                    ranDrainBody = true;
                    try
                    {
                        var fusionState = BuildFusionDrainState(result, _nodeEventLogStart);
                        PurgeFusionResultsFromShuffleQueue(fusionState);
                        await FlushPendingShuffleIntoPresentationAsync(ct);
                        FieldTraceHelper.RecordDrainBegin(
                            moveCount,
                            dealCount,
                            drainInFlight: true,
                            fieldBusy: fieldManager.IsBusy,
                            presentationLocked: CombatHitSink.PresentationLocked,
                            stepCount: stepCount,
                            requestId: requestId);
                        FieldTraceHelper.RecordOccupancySnapshot("drainBefore");
                        fieldManager.ClearOccupancyConflictFlag();

                        if (stepCount > 0)
                        {
                            await DrainBoardStepsAsync(result.Steps, requestId, fusionState, ct);
                        }
                        else
                        {
                            FieldTraceHelper.RecordDrainLegacyFallback(
                                requestId,
                                moveCount,
                                dealCount,
                                result.RemovedUids != null ? result.RemovedUids.Length : 0);
                            await DrainLegacyBoardDeltaAsync(result, ct);
                        }

                        // #7：融合补牌不再挂在 PresentFusion onFusionStarted。
                        // 导演主线忙时由 FusionRefillLockstep 在 Rotate Present 后解算；
                        // 非导演 drain（反击等）在步骤播完后补一次离散 Fill。
                        if (_completedFusionActionIds.Count > 0 && !CombatHitSink.DirectorMainlineBusy)
                        {
                            await DrainFusionRefillAfterPresentAsync(fusionState, ct);
                        }

                        // #9：导演主线忙时由 DrainRefillLockstep 离散批次解算；
                        // 旧 in-flight FillEmptySlots 仅非导演 drain（反击等）可达。
                        if (result.RemovedUids != null && result.RemovedUids.Length > 0
                            && _completedFusionActionIds.Count == 0
                            && !CombatHitSink.DirectorMainlineBusy)
                        {
                            await DrainPostRemoveRefillAsync(ct);
                        }

                        SpawnDamagePopups(result.DamagePopups, fallbackVictim: null, fallbackAmount: 0);
                        UpdateAvatarDebugText();

                        if (result.NodeClearedOrRewardPhase)
                        {
                            TryEnterNodeSettlement();
                        }
                    }
                    catch (OperationCanceledException ex)
                    {
                        ChoreoTraceContext.ForceCloseOpenChoreos("boardDrainCancelled");
                        pendingCancel = ex;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[InBattleManager] DrainPostKillBoard 主体失败: " + ex.Message);
                    }
                }
            }
            finally
            {
                PerfTraceRecorder.CloseBeat();
                FieldTraceHelper.ClearBatchTag();
            }

            // #10：Drain 尾部不再 force Sync 自愈；冲突只断言+诊断。就位栅栏仍等齐飞牌。
            if (ranDrainBody)
            {
                try
                {
                    ResolveManagers();
                    if (fieldManager != null)
                    {
                        await fieldManager.WaitAllActiveDealFlightsAsync(CancellationToken.None);
                        if (fieldManager.HasOccupancyConflictSinceClear)
                        {
                            AssertOccupancySyncForbidden("drainOccupancyConflict", "force");
                            await fieldManager.WaitAllActiveDealFlightsAsync(CancellationToken.None);
                        }

                        fieldManager.RefreshSlotHitColliders();
                        FieldTraceHelper.RecordOccupancySnapshot("drainAfter");
                        FieldTraceHelper.RecordDrainEnd(
                            moveCount,
                            dealCount,
                            drainInFlight: true,
                            fieldBusy: fieldManager.IsBusy,
                            presentationLocked: CombatHitSink.PresentationLocked,
                            stepCount: stepCount,
                            requestId: requestId);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[InBattleManager] Drain 尾部 DrainEnd 失败: " + ex.Message);
                    AssertOccupancySyncForbidden("drainTailFailure", ex.Message);
                }
            }

            if (pendingCancel != null)
            {
                throw pendingCancel;
            }
        }

        private async UniTask DrainLegacyBoardDeltaAsync(
            PostKillBoardPresentationResult result,
            CancellationToken ct)
        {
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
        }

        private async UniTask DrainBoardStepsAsync(
            BoardPresentationStep[] steps,
            int requestId,
            FusionDrainState fusionState,
            CancellationToken ct)
        {
            _pendingFusionRemoves.Clear();
            _completedFusionActionIds.Clear();

            var rotateCount = 0;
            var choreoRotateCount = 0;
            for (var i = 0; i < steps.Length; i++)
            {
                var step = steps[i];
                if (step.Kind == BoardPresentationStepKind.Rotate)
                {
                    rotateCount++;
                }

                FieldTraceHelper.RecordBoardStepBegin(
                    requestId,
                    i,
                    steps.Length,
                    step);

                try
                {
                    ct.ThrowIfCancellationRequested();
                    switch (step.Kind)
                    {
                        case BoardPresentationStepKind.Rotate:
                            var choreoBefore = ChoreoTraceContext.LastChoreoSeqId;
                            await BoardMotionStepScheduler.ExecuteMotionStepAsync(fieldManager, step, ct);
                            if (ChoreoTraceContext.LastChoreoSeqId != choreoBefore)
                            {
                                choreoRotateCount++;
                            }

                            break;

                        case BoardPresentationStepKind.Swap:
                        case BoardPresentationStepKind.Move:
                            if (step.Moves != null && step.Moves.Length > 0)
                            {
                                if (step.Kind == BoardPresentationStepKind.Move
                                    && step.Moves.Length >= GroundSlotTopology.ClockwiseRing.Count)
                                {
                                    FieldTraceHelper.RecordBoardStepFallbackGeneralHop(
                                        requestId,
                                        i,
                                        step.Moves.Length);
                                }

                                await BoardMotionStepScheduler.ExecuteMotionStepAsync(fieldManager, step, ct);
                            }

                            break;

                        case BoardPresentationStepKind.Deal:
                            if (step.Deals != null && step.Deals.Length > 0)
                            {
                                await DrainDealsAsync(step.Deals, ct, steps, i + 1);
                            }

                            break;

                        case BoardPresentationStepKind.Remove:
                            if (step.RemovedUids != null && step.RemovedUids.Length > 0)
                            {
                                if (!await TryHandleFusionRemoveStepAsync(fusionState, step, ct))
                                {
                                    await PresentSkillRemovedCardsAsync(step.RemovedUids, ct);
                                }
                            }

                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        $"[InBattleManager] BoardStep 失败 request={requestId} i={i} kind={step.Kind}: {ex.Message}");
                    FieldTraceHelper.RecordBoardStepFail(requestId, i, step, ex);
                }
                finally
                {
                    FieldTraceHelper.RecordBoardStepEnd(
                        requestId,
                        i,
                        steps.Length,
                        step,
                        ChoreoTraceContext.CurrentSeqId);
                }
            }

            if (rotateCount > 0 && rotateCount != choreoRotateCount)
            {
                FieldTraceHelper.RecordBoardRotateChoreoMismatch(
                    requestId,
                    rotateCount,
                    choreoRotateCount);
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

        private async UniTask DrainDealsAsync(
            PostKillCardDeal[] deals,
            CancellationToken ct,
            BoardPresentationStep[] steps = null,
            int rotateScanStart = 0)
        {
            ResolveManagers();
            await FlushPendingShuffleIntoPresentationAsync(ct);

            var dealInterval = deckManager != null && deckManager.LayoutSettings != null
                ? deckManager.LayoutSettings.dealInterval
                : 0.05f;
            var flightHandles = new List<DealFlightHandle>();
            var rotateDirs = new List<bool>(4);
            DealVisualTargetResolver.CollectPendingRotateDirections(steps, rotateScanStart, rotateDirs);
            var pendingRotateSteps = rotateDirs.Count;

            for (var i = 0; i < deals.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                var deal = deals[i];
                if (deal.Uid <= 0 || deal.Slot <= 0)
                {
                    continue;
                }

                var visualTargetSlot = DealVisualTargetResolver.ApplyRingSteps(deal.Slot, rotateDirs);

                if (fieldManager.TryGetCardAt(deal.Slot, out var already)
                    && already != null
                    && already.Uid == deal.Uid)
                {
                    CoreCardPresentationMapper.ApplyToManagedCard(already);
                    continue;
                }

                if (visualTargetSlot != deal.Slot
                    && fieldManager.TryGetCardAt(visualTargetSlot, out already)
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

                var ensureCard = ResolveOrSpawnDeckCardForDeal(deal);
                var flightContext = new DealFlightContext(
                    fieldManager.IsFieldBusy,
                    fieldManager.ActiveDealFlightCount + flightHandles.Count + 1,
                    pendingRotateSteps,
                    visualTargetSlot);
                var (ok, handle) = await deckManager.DealCardByUidWithFlightAsync(
                    deal.Uid,
                    deal.Slot,
                    ensureCard: ensureCard,
                    skipBusyGuard: true,
                    flightContext: flightContext,
                    cancellationToken: ct);
                if (ok)
                {
                    if (handle != null)
                    {
                        flightHandles.Add(handle);
                    }

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

            var hand = CardHandManagerSingleton.Instance;
            if (hand != null && hand.ContainsUid(deal.Uid))
            {
                return null;
            }

            if (cardManager != null && cardManager.TryGet(deal.Uid, out var existing) && existing != null)
            {
                if (existing.DisplayMode == CardDisplayMode.HandCardMode
                    || existing.DisplayMode == CardDisplayMode.DragCardMode)
                {
                    return null;
                }

                // 僵尸句柄（无 View）不可入组发牌；Release 后强制 SpawnView。
                if (existing.View == null || existing.Transform == null)
                {
                    Debug.LogWarning(
                        $"[InBattleManager] ResolveOrSpawnDeckCardForDeal uid={deal.Uid} View 为空，Release 后重 Spawn。");
                    cardManager.Release(existing, "Deal.NullViewRespawn");
                }
                else
                {
                    return existing;
                }
            }

            if (cardManager == null)
            {
                return null;
            }

            var defId = string.IsNullOrEmpty(deal.DefId)
                ? CardManagerSingleton.StandardDefId
                : deal.DefId;
            var view = cardManager.SpawnView(
                deal.Uid,
                defId,
                initialMode: CardDisplayMode.CardDeckMode,
                kind: CoreCardPresentationMapper.ResolvePresentationKind(deal.Uid, defId));
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

            FillBoardDeltaFromEventLog(pipeline, startIndex, out var moves, out var deals, out var pickedUid, out var removedUids, out var steps);
            summary.CardUid = pickedUid;
            summary.Steps = steps;
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
            Instance?.PresentShuffleIntoDeckFromEventLog(startIndex);
            PlayerInfoHudPresenter.TryGetInstance()?.SyncFromCore(animate: false);
            return summary;
        }

        private static void FillBoardDeltaFromEventLog(
            IActionPipelineSystem pipeline,
            int startIndex,
            out PostKillCardMove[] moves,
            out PostKillCardDeal[] deals,
            out int pickedUid)
        {
            FillBoardDeltaFromEventLog(pipeline, startIndex, out moves, out deals, out pickedUid, out _, out _);
        }

        private static void FillBoardDeltaFromEventLog(
            IActionPipelineSystem pipeline,
            int startIndex,
            out PostKillCardMove[] moves,
            out PostKillCardDeal[] deals,
            out int pickedUid,
            out int[] removedUids)
        {
            FillBoardDeltaFromEventLog(
                pipeline,
                startIndex,
                out moves,
                out deals,
                out pickedUid,
                out removedUids,
                out _);
        }

        private static void FillBoardDeltaFromEventLog(
            IActionPipelineSystem pipeline,
            int startIndex,
            out PostKillCardMove[] moves,
            out PostKillCardDeal[] deals,
            out int pickedUid,
            out int[] removedUids,
            out BoardPresentationStep[] steps)
        {
            pickedUid = 0;
            moves = Array.Empty<PostKillCardMove>();
            deals = Array.Empty<PostKillCardDeal>();
            removedUids = Array.Empty<int>();
            steps = Array.Empty<BoardPresentationStep>();
            if (pipeline?.EventLog?.Entries == null)
            {
                return;
            }

            var registry = NineGridArchitecture.Current.GetModel<CardRegistry>();
            var projection = BoardPresentationStepProjector.Project(
                pipeline.EventLog.Entries,
                startIndex,
                registry);
            pickedUid = projection.PickedUid;
            moves = projection.LegacyMoves ?? Array.Empty<PostKillCardMove>();
            deals = projection.LegacyDeals ?? Array.Empty<PostKillCardDeal>();
            removedUids = projection.LegacyRemovedUids ?? Array.Empty<int>();
            steps = projection.Steps ?? Array.Empty<BoardPresentationStep>();
        }

        private const string StoneLoverSkillDefId = "skill.stone_lover";
        private const string StoneLoverArmorLostCause = "skill.stone_lover.armor_lost";

        /// <summary>
        /// 扫描 EffectTriggered：经 TriggerPulseHub 发 FX/音效脉冲（发即完成、可降级）。
        /// 仅九宫格在场卡；卡组 / 手牌 / 已移除不播。不占主时间线控制权。
        /// 石头爱好者：额外同拍刷新卡面攻（观察型加攻不走受击 Sync）。
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

            var seen = new HashSet<int>();
            var stoneLoverSynced = new HashSet<int>();
            for (var i = startIndex; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type != CoreEventType.EffectTriggered || e.CardUid <= 0)
                {
                    continue;
                }

                if (!IsCoreCardOnBoardForEffectPresentation(e.CardUid))
                {
                    continue;
                }

                if (IsStoneLoverArmorLostTrigger(e) && stoneLoverSynced.Add(e.CardUid))
                {
                    TrySyncStoneLoverCardPresentation(e.CardUid);
                }

                if (!seen.Add(e.CardUid))
                {
                    continue;
                }

                var fxId = CardEffectTriggerPulseSink.IdForCard(e.CardUid);
                TriggerPulseHub.PulseFx(fxId);
                TriggerPulseHub.PulseAudio("sfx.effect." + e.CardUid.ToString());
            }
        }

        private static bool IsStoneLoverArmorLostTrigger(CoreGameEvent gameEvent)
        {
            if (string.Equals(gameEvent.SourceDefId, StoneLoverSkillDefId, StringComparison.Ordinal))
            {
                return true;
            }

            return !string.IsNullOrEmpty(gameEvent.Cause)
                   && gameEvent.Cause.IndexOf(StoneLoverArmorLostCause, StringComparison.Ordinal) >= 0;
        }

        private static void TrySyncStoneLoverCardPresentation(int cardUid)
        {
            var manager = CardManagerSingleton.TryGetInstance();
            if (manager == null || !manager.TryGet(cardUid, out var card) || card == null)
            {
                return;
            }

            CoreCardPresentationMapper.ApplyToManagedCard(card, animate: true);
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

        /// <summary>
        /// 扫描局内洗入事件并入导演 sink；仅由 Present 前缀 / Drain Flush 播出，禁止 Forget 旁路泵。
        /// </summary>
        private void PresentShuffleIntoDeckFromEventLog(int startIndex)
        {
            if (startIndex < 0)
            {
                return;
            }

            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                return;
            }

            ResolveManagers();
            if (deckManager == null || cardManager == null || deckManager.CurrentMode != CardDeckMode.InGame)
            {
                return;
            }

            ShuffleIntoDeckLockstep.EnqueueFromEventLog(
                _shuffleIntoSink,
                arch,
                startIndex,
                uid => deckManager.ContainsUid(uid));
        }

        private async UniTask FlushPendingShuffleIntoPresentationAsync(CancellationToken ct)
        {
            if (!_shuffleIntoSink.HasPending)
            {
                return;
            }

            await DrainPendingShuffleIntoPresentationCoreAsync(ct);
        }

        private async UniTask DrainPendingShuffleIntoPresentationCoreAsync(CancellationToken ct)
        {
            ResolveManagers();
            if (deckManager == null || cardManager == null)
            {
                _shuffleIntoSink.Clear();
                return;
            }

            var pending = new List<ShuffleIntoDeckPresentationEntry>(_shuffleIntoSink.PendingCount);
            while (_shuffleIntoSink.TryDequeue(out var queued))
            {
                pending.Add(queued);
            }

            if (pending.Count == 0)
            {
                return;
            }

            var burstGroups = new List<ShuffleBurstGroup>();
            var leftovers = new List<ShuffleIntoDeckPresentationEntry>();
            ShuffleBurstGrouper.Partition(pending, burstGroups, leftovers);

            var dealInterval = deckManager.LayoutSettings != null
                ? deckManager.LayoutSettings.dealInterval
                : 0.05f;
            var startedCount = pending.Count;
            ShuffleIntoDeckLockstep.RecordPresentBegin(startedCount);

            try
            {
                for (var i = 0; i < burstGroups.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    while (deckManager.IsBusy)
                    {
                        ct.ThrowIfCancellationRequested();
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    }

                    await PresentBurstScatterShuffleGroupAsync(burstGroups[i], ct);
                }

                for (var i = 0; i < leftovers.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    while (deckManager.IsBusy)
                    {
                        ct.ThrowIfCancellationRequested();
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    }

                    await PresentOneShuffleIntoDeckAsync(leftovers[i], ct);

                    if (i + 1 < leftovers.Count && dealInterval > 0f)
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(dealInterval), cancellationToken: ct);
                    }
                }
            }
            finally
            {
                ShuffleIntoDeckLockstep.RecordPresentEnd(startedCount);
            }
        }

        private async UniTask PresentBurstScatterShuffleGroupAsync(
            ShuffleBurstGroup group,
            CancellationToken ct)
        {
            if (group.Entries == null || group.Entries.Count == 0)
            {
                return;
            }

            ShuffleIntoDeckLockstep.RecordBurstScatterBegin(group.ActionId, group.Entries.Count);
            try
            {
                ResolveManagers();
                if (deckManager == null || cardManager == null)
                {
                    return;
                }

                var originEntry = group.Entries[0];
                if (!TryResolveShuffleIntoOrigin(originEntry, out var originTransform)
                    || originTransform == null)
                {
                    if (!deckManager.TryGetDefaultDealOrigin(out originTransform)
                        || originTransform == null)
                    {
                        Debug.LogWarning(
                            $"[InBattleManager] BurstScatter action={group.ActionId} 无炸开原点，回退单条洗回。");
                        for (var i = 0; i < group.Entries.Count; i++)
                        {
                            await PresentOneShuffleIntoDeckAsync(group.Entries[i], ct);
                        }

                        return;
                    }
                }

                var origin = originTransform.position;
                var cards = new List<ManagedCard>(group.Entries.Count);
                for (var i = 0; i < group.Entries.Count; i++)
                {
                    var entry = group.Entries[i];
                    if (deckManager.ContainsUid(entry.Uid))
                    {
                        continue;
                    }

                    var card = EnsureShuffleIntoCardView(entry, CardDisplayMode.GroundCardMode);
                    if (card != null)
                    {
                        cards.Add(card);
                    }
                }

                if (cards.Count == 0)
                {
                    return;
                }

                var fieldLayout = fieldManager != null ? fieldManager.LayoutSettings : null;
                var radius = fieldLayout != null ? fieldLayout.burstScatterRadius : 1.1f;
                var burstDuration = fieldLayout != null ? fieldLayout.burstScatterDuration : 0.28f;
                var holdDuration = fieldLayout != null ? fieldLayout.burstScatterHoldDuration : 0.06f;
                var exitDuration = fieldLayout != null ? fieldLayout.fieldExitDuration : 0.35f;

                await CardBurstScatterIntoDeckPresenter.PresentAsync(
                    cards,
                    origin,
                    radius,
                    burstDuration,
                    holdDuration,
                    exitDuration,
                    deckManager,
                    ct);
            }
            finally
            {
                ShuffleIntoDeckLockstep.RecordBurstScatterEnd(group.ActionId, group.Entries.Count);
            }
        }

        private ManagedCard EnsureShuffleIntoCardView(
            ShuffleIntoDeckPresentationEntry entry,
            CardDisplayMode initialMode)
        {
            ResolveManagers();
            if (cardManager == null || entry.Uid <= 0)
            {
                return null;
            }

            var arch = NineGridArchitecture.Current;
            var defId = entry.DefId;
            if (string.IsNullOrEmpty(defId)
                && arch != null
                && arch.GetModel<CardRegistry>().TryGet(entry.Uid, out var coreCard))
            {
                defId = coreCard.DefId;
            }

            if (string.IsNullOrEmpty(defId))
            {
                defId = CardManagerSingleton.StandardDefId;
            }

            cardManager.TryGet(entry.Uid, out var ensureCard);
            if (ensureCard != null
                && !string.IsNullOrEmpty(defId)
                && !string.Equals(ensureCard.DefId, defId, StringComparison.Ordinal))
            {
                Debug.LogWarning(
                    $"[InBattleManager] ShuffleInto uid={entry.Uid} 视图 def 不符 expect={defId} actual={ensureCard.DefId}，重建。");
                cardManager.Release(ensureCard, "ShuffleInto.DefIdMismatch");
                ensureCard = null;
            }

            if (ensureCard == null)
            {
                ensureCard = cardManager.SpawnView(
                    entry.Uid,
                    defId,
                    initialMode: initialMode,
                    kind: CoreCardPresentationMapper.ResolvePresentationKind(entry.Uid, defId));
                if (ensureCard != null)
                {
                    CoreCardPresentationMapper.ApplyToManagedCard(ensureCard);
                }
            }

            return ensureCard;
        }

        private async UniTask PresentOneShuffleIntoDeckAsync(
            ShuffleIntoDeckPresentationEntry entry,
            CancellationToken ct)
        {
            ResolveManagers();
            if (deckManager == null || cardManager == null || entry.Uid <= 0)
            {
                return;
            }

            if (deckManager.ContainsUid(entry.Uid))
            {
                return;
            }

            if (entry.Kind == ShuffleIntoDeckEventKind.ExistingCard)
            {
                if (cardManager.TryGet(entry.Uid, out var existing) && existing != null)
                {
                    deckManager.LaunchReturnFieldCardToDeck(existing);
                }

                return;
            }

            var ensureCard = EnsureShuffleIntoCardView(entry, CardDisplayMode.CardDeckMode);
            if (ensureCard == null)
            {
                Debug.LogWarning($"[InBattleManager] ShuffleInto 无法生成视图 uid={entry.Uid} def={entry.DefId}。");
                return;
            }

            if (!TryResolveShuffleIntoOrigin(entry, out var origin))
            {
                if (!deckManager.TryGetDefaultDealOrigin(out origin))
                {
                    Debug.LogWarning(
                        $"[InBattleManager] ShuffleInto uid={entry.Uid} 无飞入起点，fallback 直接入组。");
                }
            }

            var deckOk = await deckManager.AddCardAtFromOriginAsync(
                CardDeckManagerSingleton.RandomInsertIndex,
                ensureCard,
                origin,
                ct);
            if (!deckOk)
            {
                Debug.LogWarning($"[InBattleManager] ShuffleInto 入组失败 uid={entry.Uid} def={entry.DefId}。");
            }
        }

        private bool TryResolveShuffleIntoOrigin(
            ShuffleIntoDeckPresentationEntry entry,
            out Transform origin)
        {
            origin = null;
            ResolveManagers();

            if (entry.TriggerCardUid > 0
                && cardManager != null
                && cardManager.TryGet(entry.TriggerCardUid, out var triggerCard)
                && triggerCard?.Transform != null)
            {
                origin = triggerCard.Transform;
                return true;
            }

            if (entry.FromBoardSlot > 0)
            {
                var slotPos = ResolveBoardSlotWorldPosition(entry.FromBoardSlot);
                if (slotPos.HasValue && TryGetShuffleOriginScratch(slotPos.Value, out origin))
                {
                    return true;
                }
            }

            if (entry.TriggerCardUid > 0)
            {
                var cardPos = ResolveCardWorldPosition(entry.TriggerCardUid);
                if (cardPos.HasValue && TryGetShuffleOriginScratch(cardPos.Value, out origin))
                {
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(entry.Cause) && TryResolveHandDealOrigin(entry.Cause, out origin))
            {
                return true;
            }

            return deckManager != null && deckManager.TryGetDefaultDealOrigin(out origin);
        }

        private bool TryGetShuffleOriginScratch(Vector3 worldPosition, out Transform origin)
        {
            origin = EnsureShuffleOriginScratch();
            if (origin == null)
            {
                return false;
            }

            origin.position = worldPosition;
            return true;
        }

        private Transform EnsureShuffleOriginScratch()
        {
            if (_shuffleOriginScratch == null)
            {
                var scratchGo = new GameObject("ShuffleIntoOriginScratch");
                scratchGo.hideFlags = HideFlags.HideAndDontSave;
                _shuffleOriginScratch = scratchGo.transform;
            }

            return _shuffleOriginScratch;
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
        /// 技能移除退场后：Core FillEmptySlots + 播补牌（非导演路径）。
        /// 导演路径请走 <see cref="DrainRefillLockstep"/>。
        /// </summary>
        private async UniTask DrainPostRemoveRefillAsync(CancellationToken ct)
        {
            var arch = NineGridArchitecture.Current;
            if (!DrainRefillLockstep.ShouldRefill(arch))
            {
                return;
            }

            var pipeline = arch.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            PerfTraceRecorder.Record(
                "DrainRefill",
                -1,
                "RefillBatchBegin",
                new Dictionary<string, string> { ["path"] = "presentAdapter" });
            arch.GetSystem<IBoardSystem>().FillEmptySlots();
            FillBoardDeltaFromEventLog(
                pipeline,
                startIndex,
                out _,
                out var refillDeals,
                out _,
                out _,
                out var refillSteps);
            PresentEffectTriggersFromEventLog(startIndex);
            PresentShuffleIntoDeckFromEventLog(startIndex);
            await FlushPendingShuffleIntoPresentationAsync(ct);
            if (refillDeals != null && refillDeals.Length > 0)
            {
                // 传入 steps 使多张 Deal 能前瞻后续 Rotate（若有），避免 Aim=birth。
                await DrainDealsAsync(refillDeals, ct, refillSteps, 0);
            }

            PerfTraceRecorder.Record(
                "DrainRefill",
                -1,
                "RefillBatchEnd",
                new Dictionary<string, string> { ["path"] = "presentAdapter" });
        }

        private sealed class FusionDrainState
        {
            public Dictionary<int, SkeletonFusionPresentationEntry> ParticipantIndex = new();
            public HashSet<int> ResultUids = new();
        }

        private static FusionDrainState BuildFusionDrainState(
            PostKillBoardPresentationResult result,
            int eventLogStartIndex)
        {
            var state = new FusionDrainState();
            var removedInBatch = CollectRemovedUidsFromSteps(result.Steps);
            if (removedInBatch.Count == 0 && result.RemovedUids != null)
            {
                for (var i = 0; i < result.RemovedUids.Length; i++)
                {
                    var uid = result.RemovedUids[i];
                    if (uid > 0)
                    {
                        removedInBatch.Add(uid);
                    }
                }
            }

            if (removedInBatch.Count == 0)
            {
                return state;
            }

            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                return state;
            }

            var entries = arch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            // 仅扫本节点 EventLog 窗口，禁止全历史 Collect(0) 把旧融合 ResultUid 误 purge 进 sink。
            var startIndex = eventLogStartIndex < 0 ? 0 : eventLogStartIndex;
            var fusions = SkeletonFusionPresentationScanner.Collect(entries, startIndex);
            for (var i = 0; i < fusions.Count; i++)
            {
                var fusion = fusions[i];
                if (!FusionIntersectsBatch(fusion, removedInBatch))
                {
                    continue;
                }

                // 只 purge 与本批移除相交的融合结果；ResultUid 须为正。
                if (fusion.ResultUid > 0)
                {
                    state.ResultUids.Add(fusion.ResultUid);
                }

                var participants = fusion.ParticipantUids;
                for (var j = 0; j < participants.Length; j++)
                {
                    var uid = participants[j];
                    if (uid > 0 && removedInBatch.Contains(uid))
                    {
                        state.ParticipantIndex[uid] = fusion;
                    }
                }
            }

            RecordSkeletonFusionTrace(
                "DrainStateBuilt",
                -1,
                new Dictionary<string, string>
                {
                    ["removedInBatch"] = removedInBatch.Count.ToString(),
                    ["eventLogStart"] = startIndex.ToString(),
                    ["fusionCandidates"] = fusions.Count.ToString(),
                    ["participantMapped"] = state.ParticipantIndex.Count.ToString(),
                    ["resultUids"] = state.ResultUids.Count.ToString(),
                });

            return state;
        }

        private static HashSet<int> CollectRemovedUidsFromSteps(BoardPresentationStep[] steps)
        {
            var removed = new HashSet<int>();
            if (steps == null)
            {
                return removed;
            }

            for (var i = 0; i < steps.Length; i++)
            {
                var step = steps[i];
                if (step.Kind != BoardPresentationStepKind.Remove || step.RemovedUids == null)
                {
                    continue;
                }

                for (var j = 0; j < step.RemovedUids.Length; j++)
                {
                    var uid = step.RemovedUids[j];
                    if (uid > 0)
                    {
                        removed.Add(uid);
                    }
                }
            }

            return removed;
        }

        private static bool FusionIntersectsBatch(
            SkeletonFusionPresentationEntry fusion,
            HashSet<int> removedInBatch)
        {
            var participants = fusion.ParticipantUids;
            for (var i = 0; i < participants.Length; i++)
            {
                if (removedInBatch.Contains(participants[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static void RecordSkeletonFusionTrace(
            string site,
            int uid,
            Dictionary<string, string> payload = null)
        {
            PerfTraceRecorder.Record("SkeletonFusion", uid, site, payload);
            if (payload == null || payload.Count == 0)
            {
                Debug.Log($"[SkeletonFusion] {site} uid={uid}");
                return;
            }

            var summary = new StringBuilder(site);
            summary.Append(" uid=").Append(uid);
            foreach (var pair in payload)
            {
                summary.Append(' ').Append(pair.Key).Append('=').Append(pair.Value);
            }

            Debug.Log("[SkeletonFusion] " + summary);
        }

        private void PurgeFusionResultsFromShuffleQueue(FusionDrainState fusionState)
        {
            if (fusionState == null || fusionState.ResultUids.Count == 0)
            {
                return;
            }

            _shuffleIntoSink.PurgeUids(fusionState.ResultUids);
        }

        private async UniTask<bool> TryHandleFusionRemoveStepAsync(
            FusionDrainState fusionState,
            BoardPresentationStep step,
            CancellationToken ct)
        {
            if (fusionState == null
                || step.RemovedUids == null
                || step.RemovedUids.Length != 1)
            {
                return false;
            }

            var uid = step.RemovedUids[0];
            if (!fusionState.ParticipantIndex.TryGetValue(uid, out var fusion))
            {
                RecordSkeletonFusionTrace(
                    "RemoveStepMiss",
                    uid,
                    new Dictionary<string, string>
                    {
                        ["stepActionId"] = step.ActionId.ToString(),
                        ["participantMapped"] = fusionState.ParticipantIndex.Count.ToString(),
                    });
                return false;
            }

            if (!_pendingFusionRemoves.TryGetValue(fusion.ActionId, out var pending))
            {
                pending = new HashSet<int>();
                _pendingFusionRemoves[fusion.ActionId] = pending;
            }

            pending.Add(uid);
            if (pending.Count < fusion.ParticipantUids.Length)
            {
                RecordSkeletonFusionTrace(
                    "RemoveStepPending",
                    uid,
                    new Dictionary<string, string>
                    {
                        ["skillId"] = fusion.SkillId,
                        ["pending"] = pending.Count.ToString(),
                        ["required"] = fusion.ParticipantUids.Length.ToString(),
                    });
                return true;
            }

            if (_completedFusionActionIds.Contains(fusion.ActionId))
            {
                RecordSkeletonFusionTrace(
                    "RemoveStepAlreadyCompleted",
                    uid,
                    new Dictionary<string, string> { ["skillId"] = fusion.SkillId });
                return true;
            }

            _completedFusionActionIds.Add(fusion.ActionId);
            _pendingFusionRemoves.Remove(fusion.ActionId);

            ResolveManagers();
            if (fieldManager == null)
            {
                RecordSkeletonFusionTrace("PresentBlockedNoFieldManager", uid);
                return false;
            }

            var request = ToFusionRequest(fusion);
            EnsureFusionResultView(fusion);
            RecordSkeletonFusionTrace(
                "PresentBegin",
                uid,
                new Dictionary<string, string>
                {
                    ["skillId"] = fusion.SkillId,
                    ["resultUid"] = fusion.ResultUid.ToString(),
                    ["resultDefId"] = fusion.ResultDefId,
                    ["participants"] = string.Join(",", fusion.ParticipantUids),
                });
            await fieldManager.PresentSkeletonFusionAsync(
                request,
                onFusionStarted: null,
                ct);
            RecordSkeletonFusionTrace(
                "PresentEnd",
                uid,
                new Dictionary<string, string>
                {
                    ["skillId"] = fusion.SkillId,
                    ["refillDeferredToDirector"] = CombatHitSink.DirectorMainlineBusy ? "1" : "0",
                });
            return true;
        }

        private static SkeletonFusionPresentationRequest ToFusionRequest(SkeletonFusionPresentationEntry entry)
        {
            return new SkeletonFusionPresentationRequest(
                entry.ActionId,
                entry.SkillId,
                entry.TriggerCardUid,
                entry.ParticipantUids,
                entry.ResultUid,
                entry.ResultDefId);
        }

        private void EnsureFusionResultView(SkeletonFusionPresentationEntry fusion)
        {
            ResolveManagers();
            if (cardManager == null || fusion.ResultUid <= 0)
            {
                return;
            }

            if (!cardManager.TryGet(fusion.ResultUid, out var resultCard) || resultCard == null)
            {
                resultCard = cardManager.SpawnView(
                    fusion.ResultUid,
                    fusion.ResultDefId,
                    initialMode: CardDisplayMode.GroundCardMode,
                    kind: CoreCardPresentationMapper.ResolvePresentationKind(
                        fusion.ResultUid, fusion.ResultDefId));
            }

            if (resultCard != null)
            {
                CoreCardPresentationMapper.ApplyToManagedCard(resultCard);
            }
        }

        /// <summary>
        /// 合体表演结束后的离散补牌（非导演路径）。排除刚洗入的合体结果 uid。
        /// 导演路径请走 FusionRefillLockstep。
        /// </summary>
        private async UniTask DrainFusionRefillAfterPresentAsync(
            FusionDrainState fusionState,
            CancellationToken ct)
        {
            var arch = NineGridArchitecture.Current;
            var phaseSystem = arch.GetSystem<IPhaseSystem>();
            if (phaseSystem.CurrentPhase != GamePhase.InteractionLoop)
            {
                return;
            }

            var exclude = new List<int>(fusionState != null ? fusionState.ResultUids.Count : 0);
            if (fusionState != null)
            {
                foreach (var uid in fusionState.ResultUids)
                {
                    if (uid > 0)
                    {
                        exclude.Add(uid);
                    }
                }
            }

            if (exclude.Count == 0)
            {
                return;
            }

            var deck = arch.GetModel<DeckModel>();
            var board = arch.GetModel<BoardModel>();
            if (deck == null
                || deck.DrawPileUids == null
                || deck.DrawPileUids.Count <= 0
                || !FusionRefillPlanner.HasRefillCandidateExcluding(deck, exclude)
                || !FusionRefillPlanner.HasEmptyBoardSlot(board))
            {
                RecordSkeletonFusionTrace("RefillSkipped", -1);
                return;
            }

            var originalOrder = new List<int>(deck.DrawPileUids);
            var refillOrder = FusionRefillPlanner.BuildRefillDrawOrder(originalOrder, exclude);
            if (!FusionRefillPlanner.HasRefillCandidateExcludingOrder(refillOrder, exclude))
            {
                RecordSkeletonFusionTrace("RefillSkippedNoCandidate", -1);
                return;
            }

            RecordSkeletonFusionTrace(
                "RefillBatchBegin",
                -1,
                new Dictionary<string, string>
                {
                    ["excludeCount"] = exclude.Count.ToString(),
                    ["path"] = "presentAdapter",
                });

            deck.ReorderDrawPile(refillOrder);
            try
            {
                var pipeline = arch.GetSystem<IActionPipelineSystem>();
                var startIndex = pipeline.EventLog.Entries.Count;
                arch.GetSystem<IBoardSystem>().FillEmptySlots();
                FillBoardDeltaFromEventLog(
                    pipeline,
                    startIndex,
                    out _,
                    out var refillDeals,
                    out _,
                    out _,
                    out var refillSteps);
                PresentEffectTriggersFromEventLog(startIndex);
                var filtered = FusionRefillPlanner.FilterDealsExcluding(refillDeals, exclude);
                if (filtered.Length > 0)
                {
                    await DrainDealsAsync(filtered, ct, refillSteps, 0);
                }
            }
            finally
            {
                var restored = FusionRefillPlanner.RestoreRemainingOrder(originalOrder, deck.DrawPileUids);
                deck.ReorderDrawPile(restored);
            }

            RecordSkeletonFusionTrace("RefillBatchEnd", -1);
        }

        private async UniTask<bool> ValidateHandDragApplyAsync(ManagedCard card, int? targetGroundSlot)
        {
            if (card == null)
            {
                return false;
            }

            if (CombatHitSink.ChoiceOverlayActive
                || CombatHitSink.DirectorMainlineBusy)
            {
                return false;
            }

            ResolveManagers();

            string selectedOption = null;
            if (IsStatBoostCard(card.DefId))
            {
                HideHandCardForChoice(card);
                selectedOption = await PresentStatBoostChoiceAsync();
                if (string.IsNullOrEmpty(selectedOption))
                {
                    RestoreHandCardAfterChoiceCancel(card);
                    return false;
                }
            }

            HelpCardBoardSelectResolver.TryGetPlayKind(
                card.DefId,
                out var playKind,
                out var selectedCardsSpec);

            if (playKind == HelpCardPlayKind.MultiBoardSelect)
            {
                if (!HelpCardBoardSelectResolver.TryGetRequiredBoardSelectCount(card.DefId, out var boardSelectCount)
                    || !BoardCardSelectModeController.Begin(card.Uid, card.DefId, boardSelectCount))
                {
                    if (IsStatBoostCard(card.DefId))
                    {
                        RestoreHandCardAfterChoiceCancel(card);
                    }

                    return false;
                }

                if (HelpCardBoardSelectResolver.TryGetBoardSelectPrompt(card.DefId, out var prompt))
                {
                    DescriptionHoverSink.RequestShowText(prompt, DescriptionShowRoute.BoardSelect);
                }

                return true;
            }

            int[] selectedUids = null;
            if (playKind == HelpCardPlayKind.SingleDragTarget)
            {
                if (!TryResolveSingleDragTarget(targetGroundSlot, selectedCardsSpec, out var targetUid))
                {
                    return false;
                }

                selectedUids = new[] { targetUid };
            }

            if (!TrySubmitUseItemIntentFromCards(card.Uid, selectedUids, selectedOption))
            {
                if (IsStatBoostCard(card.DefId))
                {
                    RestoreHandCardAfterChoiceCancel(card);
                }

                return false;
            }

            return true;
        }

        private bool TryResolveSingleDragTarget(
            int? targetGroundSlot,
            HelpCardSelectedCardsSpec spec,
            out int targetUid)
        {
            targetUid = 0;
            if (!targetGroundSlot.HasValue || fieldManager == null)
            {
                return false;
            }

            if (!fieldManager.TryGetCardAt(targetGroundSlot.Value, out var targetCard)
                || targetCard == null)
            {
                return false;
            }

            if (targetCard.CoreKind == CardPresentationKind.Avatar)
            {
                return false;
            }

            if (spec.RequiresMonster && targetCard.CoreKind != CardPresentationKind.Monster)
            {
                return false;
            }

            targetUid = targetCard.Uid;
            return targetUid > 0;
        }

        private void AbortBoardSelectIfActive(string reason)
        {
            if (!BoardCardSelectModeController.IsActive)
            {
                return;
            }

            BoardCardSelectModeController.RequestAbort(reason);
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

            // #10：BoardSelect 合法性读 BoardModel，不再前置 Sync 自愈占格镜像。
            if (!TryValidateBoardSelectTargets(selectedUids, requiredCount, out var validateReason))
            {
                Debug.LogWarning(
                    $"[InBattleManager] BoardSelect 目标校验失败 itemUid={itemUid} reason={validateReason} uids={string.Join(",", selectedUids)}");
                await RestoreBoardSelectItemToHandAsync(itemUid, defId, $"invalid-targets:{validateReason}");
                return;
            }

            if (!await TryAwaitDirectorIdleForBoardSelectAsync())
            {
                await RestoreBoardSelectItemToHandAsync(itemUid, defId, "director-busy-timeout");
                return;
            }

            if (!TrySubmitUseItemIntentFromCards(itemUid, selectedUids, null))
            {
                await RestoreBoardSelectItemToHandAsync(itemUid, defId, "use-intent-rejected");
                return;
            }

            RegistryTraceSink.NotifyUserInteraction?.Invoke("BoardSelectUseItemAccepted");
            await VanishParkedBoardSelectItemIfPresentAsync(itemUid);
            await UniTask.CompletedTask;
        }

        private UniTask OnBoardSelectionAbortedAsync(int itemUid, string defId, string reason)
        {
            return RestoreBoardSelectItemToHandAsync(itemUid, defId, reason);
        }

        private static async UniTask<bool> TryAwaitDirectorIdleForBoardSelectAsync()
        {
            const int stepMs = 50;
            var elapsed = 0;
            while (elapsed < BoardSelectLockWaitMs)
            {
                if (!CombatHitSink.ChoiceOverlayActive
                    && !CombatHitSink.DirectorMainlineBusy)
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
                if (!hand.IsDragging
                    && !CombatHitSink.BoardSelectModeActive
                    && (hand.CanAcceptCard || !hand.IsBusy))
                {
                    return true;
                }

                await UniTask.Delay(stepMs);
                elapsed += stepMs;
            }

            return !hand.IsDragging
                   && !CombatHitSink.BoardSelectModeActive
                   && (hand.CanAcceptCard || !hand.IsBusy);
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
                card = cardManager.SpawnView(
                    itemUid,
                    defId,
                    initialMode: CardDisplayMode.HandCardMode,
                    kind: CoreCardPresentationMapper.ResolvePresentationKind(itemUid, defId));
                if (card != null)
                {
                    CoreCardPresentationMapper.ApplyToManagedCard(card);
                }
            }
            else
            {
                CardHandManagerSingleton.DisarmBoardSelectParkedHitProxy(card);
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

        private async UniTask VanishParkedBoardSelectItemIfPresentAsync(int itemUid)
        {
            if (itemUid <= 0)
            {
                return;
            }

            ResolveManagers();
            if (cardManager == null || !cardManager.TryGet(itemUid, out var card) || card == null)
            {
                return;
            }

            var hand = CardHandManagerSingleton.Instance;
            if (hand == null)
            {
                CardHandManagerSingleton.DisarmBoardSelectParkedHitProxy(card);
                CardManagerSingleton.Instance.Release(card, "BoardSelect.VanishNoHand");
                return;
            }

            await hand.VanishParkedBoardSelectItemAsync(card);
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

        private async UniTask PlayDirectorUseItemPresentAsync(
            PostKillBoardPresentationResult boardResult,
            CancellationToken token)
        {
            // #8：无盘面 delta 的洗回（传送卡）也必须在用牌 Present 主线内 Flush，禁止 Forget 旁路。
            await FlushPendingShuffleIntoPresentationAsync(token);

            var useResult = _pendingUseItemPresent;
            _pendingUseItemPresent = default;
            if (!useResult.Accepted)
            {
                useResult = new UseItemPresentationResult
                {
                    Accepted = boardResult.Accepted,
                    PostKillBoard = boardResult,
                    DamagePopups = boardResult.DamagePopups ?? Array.Empty<CombatDamagePopup>(),
                    KilledTargetUids = boardResult.RemovedUids ?? Array.Empty<int>(),
                    TargetKilled = boardResult.RemovedUids != null && boardResult.RemovedUids.Length > 0,
                    AvatarDefeated = boardResult.AvatarDefeated,
                    NodeClearedOrRewardPhase = boardResult.NodeClearedOrRewardPhase,
                    RewardChoicePending =
                        NineGridArchitecture.Current.GetModel<PendingChoiceModel>().Kind.Value
                        == PendingChoiceKind.Reward,
                };
            }
            else if (!useResult.PostKillBoard.Accepted && boardResult.Accepted)
            {
                useResult.PostKillBoard = boardResult;
            }

            await PlayUseItemPresentCoreAsync(useResult, token);
        }

        private async UniTask PlayUseItemPresentCoreAsync(
            UseItemPresentationResult useResult,
            CancellationToken ct)
        {
            ResolveManagers();
            // UseItem Present 节拍：编排主线 Commit（对齐已结算 Core → 投影），非队列外直刷。
            CoreCardPresentationMapper.CommitAllSpawnedCards();
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
                && ((useResult.PostKillBoard.Steps != null && useResult.PostKillBoard.Steps.Length > 0)
                    || (useResult.PostKillBoard.Moves != null && useResult.PostKillBoard.Moves.Length > 0)
                    || (useResult.PostKillBoard.Deals != null && useResult.PostKillBoard.Deals.Length > 0)
                    || (useResult.PostKillBoard.RemovedUids != null && useResult.PostKillBoard.RemovedUids.Length > 0)))
            {
                await DrainPostKillBoardAsync(useResult.PostKillBoard, ct);
            }
            // #10：无盘面 delta 时不再 soft Sync；几何由导演 Present 维护。

            if (useResult.AvatarDefeated)
            {
                FieldBattleManagerSingleton.Instance?.TryBeginAvatarDefeatPresentation(ct);
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

            // 击杀清场由旋转批投影 ScheduleNodeSettlement；此处仅处理用牌批内已进入结算相位的非击杀路径。
            if (useResult.NodeClearedOrRewardPhase && !useResult.TargetKilled)
            {
                TryEnterNodeSettlement();
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

            AbortBoardSelectIfActive("reward-choice");
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

                // 选完关闭覆盖层；Drain 期间由导演主线租约挡输入（外层已持锁则复用）。
                var acquiredDrainLock = CombatHitSink.TryBeginPresentationLock("RewardDrain");
                CombatHitSink.ChoiceOverlayActive = false;
                try
                {
                    FillBoardDeltaFromEventLog(pipeline, startIndex, out var moves, out var deals, out _, out var removedUids, out var rewardSteps);
                    PresentGoldGainsFromEventLog(startIndex);
                    PresentEffectTriggersFromEventLog(startIndex);
                    PresentShuffleIntoDeckFromEventLog(startIndex);
                    var phase = phaseSystem.CurrentPhase;
                    var boardDelta = new PostKillBoardPresentationResult
                    {
                        Accepted = true,
                        Steps = rewardSteps ?? Array.Empty<BoardPresentationStep>(),
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

                    if ((boardDelta.Steps != null && boardDelta.Steps.Length > 0)
                        || (boardDelta.Moves != null && boardDelta.Moves.Length > 0)
                        || (boardDelta.Deals != null && boardDelta.Deals.Length > 0)
                        || (boardDelta.RemovedUids != null && boardDelta.RemovedUids.Length > 0))
                    {
                        await DrainPostKillBoardAsync(boardDelta, EnsurePresentationToken());
                    }
                    else
                    {
                        // 空 delta：对账已提交投影（不直读最新 Core 抢刷卡面）。
                        CoreCardPresentationMapper.SyncAllSpawnedCards();
                        UpdateAvatarDebugText();
                        // #10：空 delta 不再 soft Sync。
                    }

                    RefreshPersistentInBattleUi(animate: false);

                    if (boardDelta.AvatarDefeated)
                    {
                        FieldBattleManagerSingleton.Instance?.TryBeginAvatarDefeatPresentation(
                            EnsurePresentationToken());
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

            AbortBoardSelectIfActive("stat-boost-choice");
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

        private static bool ShouldDeferBoardTimelineSync()
        {
            return ChoreoTraceContext.PumpRunning || ChoreoTraceContext.DrainInFlight;
        }

        private void FlushDeferredBoardSync(bool force = false)
        {
            if (!force && ShouldDeferBoardTimelineSync())
            {
                return;
            }

            if (_pendingSyncFromCore)
            {
                _pendingSyncFromCore = false;
                // #10：延迟对账降级为断言，禁止静默修补。
                AssertOccupancySyncForbidden("flushDeferredBoardSync", force ? "force" : "soft");
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

        private static int ResolvePlayerAttackTargetFromCore(int intendedTargetUid)
        {
            var arch = NineGridArchitecture.Current;
            return arch.GetSystem<IPhaseSystem>().ResolvePlayerAttackTargetUid(intendedTargetUid);
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

        private void RequestSyncBoardFromCore()
        {
            // #10：Cards 侧 RequestSync 入口改为断言，不再 heal。
            AssertOccupancySyncForbidden("requestSyncBoardFromCore", "soft");
        }

        private static void AssertOccupancySyncForbidden(string reason, string detail = null)
        {
            OccupancyForceSyncGuard.RecordForbiddenSync(reason, detail);
            var message =
                "[InBattleManager] #10 占格强制对账断言触发（正常路径永不应发生）。reason="
                + (reason ?? string.Empty)
                + " detail="
                + (detail ?? string.Empty);
            Debug.LogError(message);
            Debug.Assert(false, message);
        }

        /// <summary>
        /// #10：占格强制/软对账已退场。保留入口仅作断言探针——触发即失败并留诊断，永不静默修补。
        /// </summary>
        private void SyncBoardOccupancyFromCore(bool force = false)
        {
            AssertOccupancySyncForbidden(
                force ? "forceSync" : "softSync",
                "SyncBoardOccupancyFromCore");
        }

        private static void OnNodeSettlementFromCombat()
        {
            Instance?.TryEnterNodeSettlement();
        }

        private static void OnBattleEndedFromCombat(bool victory)
        {
            var instance = Instance;
            if (instance != null && instance._presentationDirector != null)
            {
                instance._presentationDirector.HardClearIntents(
                    victory ? IntentClearReason.PhaseChange : IntentClearReason.Defeat);
                CombatHitSink.DirectorMainlineBusy = false;
            }

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

        private void Update()
        {
            if (_presentationDirector == null)
            {
                CombatHitSink.DirectorMainlineBusy = false;
                return;
            }

            _presentationDirector.Tick(Time.deltaTime);
            CombatHitSink.DirectorMainlineBusy = _presentationDirector.IsMainlineBusy;
        }

        private void EnsurePresentationDirector()
        {
            if (_presentationDirector != null)
            {
                return;
            }

            var arch = NineGridArchitecture.Current;
            _coreCommandDispatcher = new CoreCommandDispatcher(arch);
            _explorePresentChannel = new QueuedBoardPresentChannel(
                DrainPostKillBoardAsync,
                EnsurePresentationToken);
            _attackBoardPresentChannel = new QueuedBoardPresentChannel(
                DrainPostKillBoardAsync,
                EnsurePresentationToken);
            _attackHitPresentChannel = new CombatAttackPresentChannel(
                PlayDirectorAttackHitPresentAsync,
                EnsurePresentationToken);
            _attackCounterPresentChannel = new CombatCounterPresentChannel(
                PlayDirectorCounterPresentAsync,
                EnsurePresentationToken);
            _useItemBoardPresentChannel = new QueuedBoardPresentChannel(
                DrainPostKillBoardAsync,
                EnsurePresentationToken);
            _useItemPresentChannel = new UseItemPresentChannel(
                PlayDirectorUseItemPresentAsync,
                EnsurePresentationToken);

            var exploreFactory = new ExploreIntentScriptFactory(
                arch,
                _coreCommandDispatcher,
                _explorePresentChannel,
                OnExploreBatchProjected);
            var attackFactory = new AttackIntentScriptFactory(
                arch,
                _coreCommandDispatcher,
                _attackHitPresentChannel,
                _attackBoardPresentChannel,
                _attackCounterPresentChannel,
                OnAttackHitBatchProjected,
                OnAttackBoardBatchProjected,
                OnAttackCounterBatchProjected);
            var useItemFactory = new UseItemIntentScriptFactory(
                arch,
                _coreCommandDispatcher,
                _useItemPresentChannel,
                _useItemBoardPresentChannel,
                OnUseItemBatchProjected,
                OnUseItemBoardBatchProjected,
                OnUseItemResolvedWithoutKill);
            // 开关在 TriggerPulseHub.PulseFx/PulseAudio；此处只装配实现 + 音效 debounce。
            TriggerPulseHub.Configure(
                new CardEffectTriggerPulseSink(),
                new DebouncingTriggerPulseSink(
                    new AudioTriggerPulseSink(),
                    TriggerPulseHub.DefaultAudioDebounceSeconds));

            _presentationDirector = new PresentationDirector(
                new RoutingIntentScriptFactory(exploreFactory, attackFactory, useItemFactory),
                uiPickPreview: null,
                timelineDiagnostics: DirectorTrace.TimelineSink);
            BindDirectorIntentRuntime(_presentationDirector);
            CombatHitSink.BeginDirectorExternalHold = reason =>
            {
                // 导演尚未装配时允许仅靠 PresentationLocked 防重入，避免 Drain 整段被跳过。
                if (_presentationDirector == null)
                {
                    return true;
                }

                return _presentationDirector.TryBeginExternalHold(reason);
            };
            CombatHitSink.EndDirectorExternalHold = reason =>
                _presentationDirector?.EndExternalHold(reason);
            CombatHitSink.ForceEndDirectorExternalHold = reason =>
                _presentationDirector?.ForceEndExternalHold(reason);
        }

        private void TeardownPresentationDirector(IntentClearReason reason)
        {
            if (_presentationDirector != null)
            {
                _presentationDirector.ForceEndExternalHold(reason.ToString());
                _presentationDirector.HardClearIntents(reason);
            }

            CombatHitSink.BeginDirectorExternalHold = null;
            CombatHitSink.EndDirectorExternalHold = null;
            CombatHitSink.ForceEndDirectorExternalHold = null;
            TriggerPulseHub.ResetToNull();
            UnbindDirectorIntentRuntime();
            _presentationDirector = null;
            _explorePresentChannel = null;
            _attackHitPresentChannel = null;
            _attackCounterPresentChannel = null;
            _attackBoardPresentChannel = null;
            _useItemPresentChannel = null;
            _useItemBoardPresentChannel = null;
            _coreCommandDispatcher = null;
            _pendingAttackSlot = 0;
            _pendingUseItemPresent = default;
            _shuffleIntoSink.Clear();
            CombatHitSink.DirectorMainlineBusy = false;
        }

        private void RegisterExplorePresentationHandlers()
        {
            UnregisterExplorePresentationHandlers();
            var architecture = NineGridArchitecture.Current;
            if (architecture == null)
            {
                return;
            }

            _exploreRejectedUnRegister = architecture.RegisterEvent<ExploreIntentRejectedEvent>(
                OnExploreIntentRejected);
            _ensureDirectorUnRegister = architecture.RegisterEvent<EnsurePresentationDirectorRequested>(
                _ => EnsurePresentationDirector());
        }

        private void UnregisterExplorePresentationHandlers()
        {
            if (_exploreRejectedUnRegister != null)
            {
                _exploreRejectedUnRegister.UnRegister();
                _exploreRejectedUnRegister = null;
            }

            if (_ensureDirectorUnRegister != null)
            {
                _ensureDirectorUnRegister.UnRegister();
                _ensureDirectorUnRegister = null;
            }
        }

        private void OnExploreIntentRejected(ExploreIntentRejectedEvent e)
        {
            if (HasOrphanMidBattleRewardPending())
            {
                TryRecoverOrphanMidBattleRewardUi("explore:" + (e.Reason ?? string.Empty));
            }
        }

        private void BindDirectorIntentRuntime(PresentationDirector director)
        {
            var architecture = NineGridArchitecture.Current;
            if (architecture == null || director == null)
            {
                return;
            }

            var runtime = architecture.GetSystem<IPresentationIntentRuntime>() as DirectorIntentRuntime;
            if (runtime == null)
            {
                runtime = new DirectorIntentRuntime();
                architecture.RegisterSystem<IPresentationIntentRuntime>(runtime);
            }

            runtime.Bind(director);
            _directorIntentRuntime = runtime;
        }

        private void UnbindDirectorIntentRuntime()
        {
            if (_directorIntentRuntime != null)
            {
                _directorIntentRuntime.Unbind();
                _directorIntentRuntime = null;
            }
        }

        private static bool HasOrphanMidBattleRewardPending()
        {
            if (CombatHitSink.ChoiceOverlayActive)
            {
                return false;
            }

            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                return false;
            }

            var pending = arch.GetModel<PendingChoiceModel>();
            if (pending.Kind.Value != PendingChoiceKind.Reward
                || pending.RewardOptions == null
                || pending.RewardOptions.Count == 0)
            {
                return false;
            }

            var phase = arch.GetSystem<IPhaseSystem>().CurrentPhase;
            return phase == GamePhase.InteractionLoop || phase == GamePhase.RewardItemChoice;
        }

        private static void TryRecoverOrphanMidBattleRewardUi(string context)
        {
            var instance = Instance;
            if (instance == null || instance._recoveringRewardUi)
            {
                return;
            }

            if (!HasOrphanMidBattleRewardPending())
            {
                return;
            }

            Debug.LogWarning(
                "[InBattleManager] 孤儿 PendingReward（无覆盖层），重开 Bounce。context="
                + (context ?? string.Empty));
            instance._recoveringRewardUi = true;
            instance.RecoverPendingRewardUiAsync().Forget();
        }

        private async UniTaskVoid RecoverPendingRewardUiAsync()
        {
            try
            {
                await PresentRewardChoiceFromCoreAsync(hoverOnNotice: false);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _recoveringRewardUi = false;
            }
        }

        private static bool TrySubmitAttackIntentFromCards(int groundSlot)
        {
            var instance = Instance;
            if (instance == null)
            {
                Debug.LogWarning("[InBattleManager] TrySubmitAttackIntent：无局内管理器。");
                return false;
            }

            if (HasOrphanMidBattleRewardPending())
            {
                TryRecoverOrphanMidBattleRewardUi("attack");
                return false;
            }

            string legalityReject;
            if (!BoardIntentLegality.TryExplainAttack(
                    NineGridArchitecture.Current,
                    groundSlot,
                    out legalityReject))
            {
                Debug.LogWarning(
                    $"[InBattleManager] Attack 被 Core 合法性拒绝 slot={groundSlot}: {legalityReject}");
                return false;
            }

            instance.EnsurePresentationDirector();
            instance._pendingAttackSlot = groundSlot;
            bool preview;
            var accepted = instance._presentationDirector.TrySubmitIntent(
                new InputIntent(InputIntentKinds.Attack, groundSlot),
                out preview);
            CombatHitSink.DirectorMainlineBusy = instance._presentationDirector.IsMainlineBusy;
            return accepted;
        }

        private static bool TrySubmitUseItemIntentFromCards(
            int itemUid,
            int[] selectedCardUids,
            string selectedOption)
        {
            var instance = Instance;
            if (instance == null)
            {
                Debug.LogWarning("[InBattleManager] TrySubmitUseItemIntent：无局内管理器。");
                return false;
            }

            if (itemUid <= 0)
            {
                return false;
            }

            string legalityReject;
            if (!BoardIntentLegality.TryExplainUseItem(
                    NineGridArchitecture.Current,
                    itemUid,
                    selectedCardUids,
                    selectedOption,
                    out legalityReject))
            {
                Debug.LogWarning(
                    $"[InBattleManager] UseItem 被 Core 合法性拒绝 itemUid={itemUid}: {legalityReject}");
                return false;
            }

            instance.EnsurePresentationDirector();
            bool preview;
            var accepted = instance._presentationDirector.TrySubmitIntent(
                new InputIntent(InputIntentKinds.UseItem, itemUid, selectedCardUids, selectedOption),
                out preview);
            CombatHitSink.DirectorMainlineBusy = instance._presentationDirector.IsMainlineBusy;
            return accepted;
        }

        private void OnExploreBatchProjected(
            int startIndex,
            int boardSlot,
            PostKillBoardPresentationResult result)
        {
            var pipeline = NineGridArchitecture.Current.GetSystem<IActionPipelineSystem>();
            result.DamagePopups = CollectDamagePopups(pipeline.EventLog.Entries, startIndex);

            if (_explorePresentChannel != null)
            {
                _explorePresentChannel.Enqueue(result);
            }

            PresentGoldGainsFromEventLog(startIndex, ResolveBoardSlotWorldPosition(boardSlot));
            PresentEffectTriggersFromEventLog(startIndex);
            PresentShuffleIntoDeckFromEventLog(startIndex);
        }

        private void OnAttackHitBatchProjected(
            int startIndex,
            int boardSlot,
            int resolvedCombatUid,
            PostKillBoardPresentationResult result)
        {
            var pipeline = NineGridArchitecture.Current.GetSystem<IActionPipelineSystem>();
            result.DamagePopups = CollectDamagePopups(pipeline.EventLog.Entries, startIndex);

            if (_attackHitPresentChannel != null)
            {
                _attackHitPresentChannel.Enqueue(boardSlot, resolvedCombatUid, result);
            }

            PresentGoldGainsFromEventLog(startIndex, ResolveBoardSlotWorldPosition(boardSlot));
            PresentEffectTriggersFromEventLog(startIndex);
            PresentShuffleIntoDeckFromEventLog(startIndex);
        }

        private void OnAttackBoardBatchProjected(
            int startIndex,
            int boardSlot,
            PostKillBoardPresentationResult result)
        {
            var pipeline = NineGridArchitecture.Current.GetSystem<IActionPipelineSystem>();
            result.DamagePopups = CollectDamagePopups(pipeline.EventLog.Entries, startIndex);

            if (_attackBoardPresentChannel != null)
            {
                _attackBoardPresentChannel.Enqueue(result);
            }

            PresentGoldGainsFromEventLog(startIndex, ResolveBoardSlotWorldPosition(boardSlot));
            PresentEffectTriggersFromEventLog(startIndex);
            PresentShuffleIntoDeckFromEventLog(startIndex);

            if (result.NodeClearedOrRewardPhase)
            {
                ScheduleNodeSettlementAfterBoardPresent();
            }
        }

        private void OnAttackCounterBatchProjected(
            int startIndex,
            int attackerBoardSlot,
            int attackerUid,
            PostKillBoardPresentationResult result)
        {
            var pipeline = NineGridArchitecture.Current.GetSystem<IActionPipelineSystem>();
            result.DamagePopups = CollectDamagePopups(pipeline.EventLog.Entries, startIndex);

            if (_attackCounterPresentChannel != null)
            {
                _attackCounterPresentChannel.Enqueue(attackerBoardSlot, attackerUid, result);
            }

            PresentGoldGainsFromEventLog(startIndex, ResolveBoardSlotWorldPosition(attackerBoardSlot));
            PresentEffectTriggersFromEventLog(startIndex);
            PresentShuffleIntoDeckFromEventLog(startIndex);
        }

        private void OnUseItemBatchProjected(
            int startIndex,
            int boardSlot,
            PostKillBoardPresentationResult result)
        {
            var pipeline = NineGridArchitecture.Current.GetSystem<IActionPipelineSystem>();
            result.DamagePopups = CollectDamagePopups(pipeline.EventLog.Entries, startIndex);
            _pendingUseItemPresent = BuildUseItemPresentationFromBatch(startIndex, result);

            if (_useItemPresentChannel != null)
            {
                _useItemPresentChannel.Enqueue(result);
            }

            PresentGoldGainsFromEventLog(
                startIndex,
                boardSlot > 0
                    ? ResolveBoardSlotWorldPosition(boardSlot)
                    : ResolveCardWorldPosition(_pendingUseItemPresent.PrimaryTargetUid));
            PresentEffectTriggersFromEventLog(startIndex);
            PresentShuffleIntoDeckFromEventLog(startIndex);
        }

        private void OnUseItemBoardBatchProjected(
            int startIndex,
            int boardSlot,
            PostKillBoardPresentationResult result)
        {
            var pipeline = NineGridArchitecture.Current.GetSystem<IActionPipelineSystem>();
            result.DamagePopups = CollectDamagePopups(pipeline.EventLog.Entries, startIndex);

            if (_useItemBoardPresentChannel != null)
            {
                _useItemBoardPresentChannel.Enqueue(result);
            }

            PresentGoldGainsFromEventLog(startIndex, ResolveBoardSlotWorldPosition(boardSlot));
            PresentEffectTriggersFromEventLog(startIndex);
            PresentShuffleIntoDeckFromEventLog(startIndex);

            if (result.NodeClearedOrRewardPhase)
            {
                ScheduleNodeSettlementAfterBoardPresent();
            }
        }

        private void OnUseItemResolvedWithoutKill()
        {
            // 非击杀副作用（宝箱 Bounce 等）已在用牌 Present 通道处理。
        }

        private UseItemPresentationResult BuildUseItemPresentationFromBatch(
            int startIndex,
            PostKillBoardPresentationResult boardResult)
        {
            var arch = NineGridArchitecture.Current;
            var pipeline = arch.GetSystem<IActionPipelineSystem>();
            var entries = pipeline.EventLog.Entries;
            var killedUids = new List<int>(2);
            var primaryUid = 0;
            var damageAmount = 0;

            for (var i = startIndex; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type == CoreEventType.CardKilled && e.CardUid > 0 && !killedUids.Contains(e.CardUid))
                {
                    killedUids.Add(e.CardUid);
                }

                if (e.Type == CoreEventType.DamageDealt && e.Amount > 0 && e.TargetUid > 0)
                {
                    if (primaryUid == 0)
                    {
                        primaryUid = e.TargetUid;
                    }

                    if (e.TargetUid == primaryUid)
                    {
                        damageAmount = e.Amount;
                    }
                }
            }

            if (primaryUid == 0 && killedUids.Count > 0)
            {
                primaryUid = killedUids[0];
            }

            var phase = arch.GetSystem<IPhaseSystem>().CurrentPhase;
            return new UseItemPresentationResult
            {
                Accepted = true,
                TargetKilled = killedUids.Count > 0,
                KilledTargetUids = killedUids.Count > 0 ? killedUids.ToArray() : Array.Empty<int>(),
                DamagePopups = boardResult.DamagePopups ?? Array.Empty<CombatDamagePopup>(),
                DamageAmount = damageAmount,
                PrimaryTargetUid = primaryUid,
                AvatarDefeated = phase == GamePhase.Defeat || boardResult.AvatarDefeated,
                RewardChoicePending =
                    arch.GetModel<PendingChoiceModel>().Kind.Value == PendingChoiceKind.Reward,
                NodeClearedOrRewardPhase = boardResult.NodeClearedOrRewardPhase,
                PostKillBoard = boardResult,
            };
        }

        private void ScheduleNodeSettlementAfterBoardPresent()
        {
            // 旋转批投影已进清场相位：等当前 Present drain 完成后再结算（下一帧检查导演空闲）。
            AwaitDirectorIdleThenSettleAsync().Forget();
        }

        private async UniTaskVoid AwaitDirectorIdleThenSettleAsync()
        {
            var token = EnsurePresentationToken();
            try
            {
                while (_presentationDirector != null && _presentationDirector.IsMainlineBusy)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                CombatHitSink.RequestNodeSettlement();
            }
            catch (OperationCanceledException)
            {
            }
        }

        private UniTask PlayDirectorAttackHitPresentAsync(
            int boardSlot,
            int resolvedCombatUid,
            PostKillBoardPresentationResult result,
            CancellationToken token)
        {
            var battle = FieldBattleManagerSingleton.Instance;
            if (battle == null)
            {
                Debug.LogWarning("[InBattleManager] PlayDirectorAttackHitPresent：无 FieldBattleManager。");
                return UniTask.CompletedTask;
            }

            return battle.PlayDirectorAttackHitPresentAsync(boardSlot, resolvedCombatUid, result, token);
        }

        private UniTask PlayDirectorCounterPresentAsync(
            int attackerSlot,
            int attackerUid,
            PostKillBoardPresentationResult result,
            CancellationToken token)
        {
            var battle = FieldBattleManagerSingleton.Instance;
            if (battle == null)
            {
                Debug.LogWarning("[InBattleManager] PlayDirectorCounterPresent：无 FieldBattleManager。");
                return UniTask.CompletedTask;
            }

            return battle.PlayDirectorCounterPresentAsync(attackerSlot, attackerUid, result, token);
        }
    }
}
