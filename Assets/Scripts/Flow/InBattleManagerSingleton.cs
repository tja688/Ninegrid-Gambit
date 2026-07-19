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
        private bool _boardPresentationPumpRunning;
        private bool _pendingSyncFromCore;
        private int _boardPresentationRequestId;
        private readonly Queue<BoardPresentationRequest> _boardPresentationQueue = new();
        private CancellationTokenSource _presentationCts;
        private int _nodeEventLogStart;
        private readonly Queue<ShuffleIntoDeckPresentationEntry> _pendingShuffleInto = new();
        private bool _shuffleIntoDrainRunning;
        private Transform _shuffleOriginScratch;
        private readonly Dictionary<int, HashSet<int>> _pendingFusionRemoves = new();
        private readonly HashSet<int> _completedFusionActionIds = new();
        private PresentationDirector _presentationDirector;
        private QueuedBoardPresentChannel _explorePresentChannel;
        private CoreCommandDispatcher _coreCommandDispatcher;

        private sealed class BoardPresentationRequest
        {
            public PostKillBoardPresentationResult Result;
            public UniTaskCompletionSource Completion = new();
            public CancellationToken CancellationToken;
        }

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
                if (plan.PendingDeckAddUids.Contains(uid))
                {
                    continue;
                }

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

            if (sourceDefId.StartsWith("skill.", StringComparison.Ordinal))
            {
                var skills = player.SkillDefIds;
                for (var i = 0; i < skills.Count; i++)
                {
                    if (skills[i] == sourceDefId)
                    {
                        return 10000 + i;
                    }
                }

                return 19000;
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

            if (sourceDefId.StartsWith("skill.", StringComparison.Ordinal))
            {
                return skillManager != null && skillManager.TryGetDealOrigin(sourceDefId, out origin);
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
                                initialMode: CardDisplayMode.CardDeckMode);
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
                                initialMode: CardDisplayMode.GroundCardMode);
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
                // 与 Drain/UseItem 对齐：开局发牌后必须 Sync，否则会出现
                // 表现空槽可点、Core 非空拒 ClickEmpty（道具旋转 Sync 后“自愈”）。
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
                    && !_boardPresentationPumpRunning
                    && _boardPresentationQueue.Count == 0
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

            while (_boardPresentationQueue.Count > 0)
            {
                var request = _boardPresentationQueue.Dequeue();
                request.Completion.TrySetCanceled();
            }

            _pendingShuffleInto.Clear();
            _shuffleIntoDrainRunning = false;

            CombatHitSink.ForceEndPresentationLock("CancelPresentationWork");
            TeardownPresentationDirector(IntentClearReason.LayerChange);
            _boardPresentationPumpRunning = false;
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
            CombatHitSink.ResolvePlayerAttackTarget = ResolvePlayerAttackTargetFromCore;
            CombatHitSink.SyncCardPresentation = SyncManagedCardPresentation;
            CombatHitSink.SpawnDamageNumber = SpawnDamageNumberAt;
            CombatHitSink.SyncBoardFromCore = RequestSyncBoardFromCore;
            CombatHitSink.DrainPostKillBoard = DrainPostKillBoardAsync;
            CombatHitSink.ApplyPickupItem = ApplyPickupItemFromCore;
            CombatHitSink.TrySubmitExploreIntent = TrySubmitExploreIntentFromCards;
            CombatHitSink.ApplyUseItem = ApplyUseItemFromCore;
            CombatHitSink.NotifyBattleEnded = OnBattleEndedFromCombat;
            CombatHitSink.NotifyNodeSettlementReady = OnNodeSettlementFromCombat;
            FieldTraceHelper.RegisterSinkHandlers();
            PerfTraceRecorder.RegisterSinkHandlers();
            RegistryTraceRecorder.RegisterSinkHandlers();
            RegisterCardZoneOwnershipSink();
            RegisterHandBridge();
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

            CombatHitSink.ForceEndPresentationLock("UnregisterCombatHitSink");
            _drainInFlight = false;

            if (CombatHitSink.ApplyPickupItem == ApplyPickupItemFromCore)
            {
                CombatHitSink.ApplyPickupItem = null;
            }

            if (CombatHitSink.TrySubmitExploreIntent == TrySubmitExploreIntentFromCards)
            {
                CombatHitSink.TrySubmitExploreIntent = null;
            }

            CombatHitSink.DirectorMainlineBusy = false;
            TeardownPresentationDirector(IntentClearReason.LayerChange);

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
        /// 表现缓冲缓释入队：旋转/换位/hop 等大盘面 delta 串行播放，队列泵持有 PresentationLocked 至清空。
        /// </summary>
        private async UniTask DrainPostKillBoardAsync(
            PostKillBoardPresentationResult result,
            CancellationToken cancellationToken)
        {
            if (!result.Accepted)
            {
                return;
            }

            var request = new BoardPresentationRequest
            {
                Result = result,
                CancellationToken = cancellationToken,
            };
            _boardPresentationQueue.Enqueue(request);
            ChoreoTraceContext.BoardQueueDepth = _boardPresentationQueue.Count;
            FieldTraceHelper.SetBatchTag(FlowTraceBatchTags.BoardPresentationQueue);
            var stepCount = result.Steps?.Length ?? 0;
            FieldTraceHelper.RecordBoardQueueEnqueue(
                _boardPresentationQueue.Count,
                stepCount > 0 ? stepCount : (result.Moves?.Length ?? 0),
                result.Deals?.Length ?? 0);
            FieldTraceHelper.ClearBatchTag();
            EnsureBoardPresentationPumpRunning();
            await request.Completion.Task;
        }

        private void EnsureBoardPresentationPumpRunning()
        {
            if (_boardPresentationPumpRunning)
            {
                return;
            }

            RunBoardPresentationPumpAsync().Forget();
        }

        private async UniTask RunBoardPresentationPumpAsync()
        {
            if (_boardPresentationPumpRunning)
            {
                return;
            }

            _boardPresentationPumpRunning = true;
            _drainInFlight = true;
            ChoreoTraceContext.PumpRunning = true;
            ChoreoTraceContext.DrainInFlight = true;

            var acquiredHere = false;
            if (!CombatHitSink.PresentationLocked)
            {
                if (!CombatHitSink.TryBeginPresentationLock("BoardPresentationQueue"))
                {
                    Debug.LogWarning("[InBattleManager] 盘面表演队列无法获取表现锁，跳过缓释。");
                    var pendingCount = _boardPresentationQueue.Count;
                    FieldTraceHelper.RecordBoardQueueSkip("lockFail", pendingCount);
                    while (_boardPresentationQueue.Count > 0)
                    {
                        var pending = _boardPresentationQueue.Dequeue();
                        pending.Completion.TrySetResult();
                    }

                    ChoreoTraceContext.BoardQueueDepth = 0;
                    _boardPresentationPumpRunning = false;
                    _drainInFlight = false;
                    ChoreoTraceContext.PumpRunning = false;
                    ChoreoTraceContext.DrainInFlight = false;
                    return;
                }

                acquiredHere = true;
            }

            try
            {
                while (_boardPresentationQueue.Count > 0)
                {
                    var request = _boardPresentationQueue.Dequeue();
                    ChoreoTraceContext.BoardQueueDepth = _boardPresentationQueue.Count;
                    FieldTraceHelper.RecordBoardQueueDequeue(
                        _boardPresentationQueue.Count,
                        lockAcquired: true);
                    try
                    {
                        FieldTraceHelper.SetBatchTag(FlowTraceBatchTags.BoardPresentationQueue);
                        await DrainPostKillBoardCoreAsync(request.Result, request.CancellationToken);
                        request.Completion.TrySetResult();
                    }
                    catch (OperationCanceledException)
                    {
                        request.Completion.TrySetCanceled();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[InBattleManager] 盘面表演队列项失败: " + ex.Message);
                        request.Completion.TrySetException(ex);
                        _pendingSyncFromCore = true;
                    }
                    finally
                    {
                        FieldTraceHelper.ClearBatchTag();
                    }
                }
            }
            finally
            {
                _boardPresentationPumpRunning = false;
                _drainInFlight = false;
                ChoreoTraceContext.PumpRunning = false;
                ChoreoTraceContext.DrainInFlight = false;
                ChoreoTraceContext.BoardQueueDepth = 0;
                if (acquiredHere)
                {
                    CombatHitSink.EndPresentationLock("BoardPresentationQueue");
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
                        var fusionState = BuildFusionDrainState(result);
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

                        if (result.RemovedUids != null && result.RemovedUids.Length > 0
                            && _completedFusionActionIds.Count == 0)
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

            // 异常/取消后仍必跑 Sync + DrainEnd，避免 Core↔表现稳态分叉（不可放进 finally：需 await）。
            if (ranDrainBody)
            {
                try
                {
                    ResolveManagers();
                    if (fieldManager != null)
                    {
                        // 就位栅栏：解锁输入前必须等齐补牌飞行，再 Sync；禁止 flight 中 hardSet。
                        await fieldManager.WaitAllActiveDealFlightsAsync(CancellationToken.None);
                        SyncBoardOccupancyFromCore(force: true);
                        if (fieldManager.HasOccupancyConflictSinceClear)
                        {
                            Debug.LogWarning(
                                "[InBattleManager] Drain 期间发生 OccupancyConflict，再次强制 SyncBoardOccupancyFromCore。");
                            await fieldManager.WaitAllActiveDealFlightsAsync(CancellationToken.None);
                            SyncBoardOccupancyFromCore(force: true);
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
                    Debug.LogWarning("[InBattleManager] Drain 尾部 Sync/DrainEnd 失败: " + ex.Message);
                    _pendingSyncFromCore = true;
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
            Instance?.PresentShuffleIntoDeckFromEventLog(startIndex);

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

            FillBoardDeltaFromEventLog(pipeline, startIndex, out var moves, out var deals, out _, out var removedUids, out var boardSteps);
            if ((boardSteps != null && boardSteps.Length > 0)
                || (moves != null && moves.Length > 0)
                || (deals != null && deals.Length > 0)
                || (removedUids != null && removedUids.Length > 0)
                || summary.TargetKilled)
            {
                // 飘字由 PresentUseItemEffectsAsync 用 summary.DamagePopups 强兜底；此处不重复塞。
                summary.PostKillBoard = new PostKillBoardPresentationResult
                {
                    Accepted = true,
                    Steps = boardSteps ?? Array.Empty<BoardPresentationStep>(),
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

        /// <summary>
        /// 扫描局内洗入事件并入队；由 FlushPendingShuffleIntoPresentationAsync 在补牌前即时飞入卡组。
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

            var entries = arch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            if (startIndex >= entries.Count)
            {
                return;
            }

            ResolveManagers();
            if (deckManager == null || cardManager == null || deckManager.CurrentMode != CardDeckMode.InGame)
            {
                return;
            }

            var collected = ShuffleIntoDeckPresentationScanner.Collect(
                entries,
                startIndex,
                uid => deckManager.ContainsUid(uid));
            for (var i = 0; i < collected.Count; i++)
            {
                _pendingShuffleInto.Enqueue(collected[i]);
            }

            if (_pendingShuffleInto.Count > 0)
            {
                EnsureShuffleIntoDrainScheduled();
            }
        }

        private void EnsureShuffleIntoDrainScheduled()
        {
            if (_shuffleIntoDrainRunning)
            {
                return;
            }

            _shuffleIntoDrainRunning = true;
            DrainPendingShuffleIntoPresentationAsync(EnsurePresentationToken()).Forget();
        }

        private async UniTask FlushPendingShuffleIntoPresentationAsync(CancellationToken ct)
        {
            if (_pendingShuffleInto.Count == 0)
            {
                return;
            }

            if (!_shuffleIntoDrainRunning)
            {
                _shuffleIntoDrainRunning = true;
                try
                {
                    await DrainPendingShuffleIntoPresentationCoreAsync(ct);
                }
                finally
                {
                    _shuffleIntoDrainRunning = false;
                }

                return;
            }

            while (_shuffleIntoDrainRunning && _pendingShuffleInto.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            if (_pendingShuffleInto.Count > 0)
            {
                _shuffleIntoDrainRunning = true;
                try
                {
                    await DrainPendingShuffleIntoPresentationCoreAsync(ct);
                }
                finally
                {
                    _shuffleIntoDrainRunning = false;
                }
            }
        }

        private async UniTask DrainPendingShuffleIntoPresentationAsync(CancellationToken ct)
        {
            try
            {
                await DrainPendingShuffleIntoPresentationCoreAsync(ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[InBattleManager] ShuffleInto 表现失败: " + ex.Message);
            }
            finally
            {
                _shuffleIntoDrainRunning = false;
                if (_pendingShuffleInto.Count > 0)
                {
                    EnsureShuffleIntoDrainScheduled();
                }
            }
        }

        private async UniTask DrainPendingShuffleIntoPresentationCoreAsync(CancellationToken ct)
        {
            ResolveManagers();
            if (deckManager == null || cardManager == null)
            {
                return;
            }

            var dealInterval = deckManager.LayoutSettings != null
                ? deckManager.LayoutSettings.dealInterval
                : 0.05f;

            while (_pendingShuffleInto.Count > 0)
            {
                ct.ThrowIfCancellationRequested();

                while (deckManager.IsBusy)
                {
                    ct.ThrowIfCancellationRequested();
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                var entry = _pendingShuffleInto.Dequeue();
                await PresentOneShuffleIntoDeckAsync(entry, ct);

                if (_pendingShuffleInto.Count > 0 && dealInterval > 0f)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(dealInterval), cancellationToken: ct);
                }
            }
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
            if (ensureCard == null)
            {
                ensureCard = cardManager.SpawnView(
                    entry.Uid,
                    defId,
                    initialMode: CardDisplayMode.CardDeckMode);
                if (ensureCard != null)
                {
                    CoreCardPresentationMapper.ApplyToManagedCard(ensureCard);
                }
            }

            if (ensureCard == null)
            {
                Debug.LogWarning($"[InBattleManager] ShuffleInto 无法生成视图 uid={entry.Uid} def={defId}。");
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
                Debug.LogWarning($"[InBattleManager] ShuffleInto 入组失败 uid={entry.Uid} def={defId}。");
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
        }

        private sealed class FusionDrainState
        {
            public Dictionary<int, SkeletonFusionPresentationEntry> ParticipantIndex = new();
            public HashSet<int> ResultUids = new();
        }

        private static FusionDrainState BuildFusionDrainState(PostKillBoardPresentationResult result)
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
            var fusions = SkeletonFusionPresentationScanner.Collect(entries, 0);
            for (var i = 0; i < fusions.Count; i++)
            {
                var fusion = fusions[i];
                if (!FusionIntersectsBatch(fusion, removedInBatch))
                {
                    continue;
                }

                state.ResultUids.Add(fusion.ResultUid);
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
            if (fusionState == null || fusionState.ResultUids.Count == 0 || _pendingShuffleInto.Count == 0)
            {
                return;
            }

            var kept = new Queue<ShuffleIntoDeckPresentationEntry>();
            while (_pendingShuffleInto.Count > 0)
            {
                var entry = _pendingShuffleInto.Dequeue();
                if (!fusionState.ResultUids.Contains(entry.Uid))
                {
                    kept.Enqueue(entry);
                }
            }

            while (kept.Count > 0)
            {
                _pendingShuffleInto.Enqueue(kept.Dequeue());
            }
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
                fusionCt => DrainFusionRefillAsync(fusion.ResultUid, fusionCt),
                ct);
            RecordSkeletonFusionTrace(
                "PresentEnd",
                uid,
                new Dictionary<string, string> { ["skillId"] = fusion.SkillId });
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
                    initialMode: CardDisplayMode.GroundCardMode);
            }

            if (resultCard != null)
            {
                CoreCardPresentationMapper.ApplyToManagedCard(resultCard);
            }
        }

        /// <summary>
        /// 合体开始后补牌：排除刚洗入的合体结果 uid；牌堆仅有合体结果时跳过（等下一轮交互）。
        /// </summary>
        private async UniTask DrainFusionRefillAsync(int fusionResultUid, CancellationToken ct)
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

            if (!HasRefillCandidateExcluding(deck, fusionResultUid))
            {
                return;
            }

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

            var originalOrder = new List<int>(deck.DrawPileUids);
            var refillOrder = BuildRefillDrawOrder(originalOrder, fusionResultUid);
            if (!HasRefillCandidateExcludingOrder(refillOrder, fusionResultUid))
            {
                return;
            }

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
                var filtered = FilterDealsExcluding(refillDeals, fusionResultUid);
                if (filtered.Length > 0)
                {
                    await DrainDealsAsync(filtered, ct, refillSteps, 0);
                }
            }
            finally
            {
                deck.ReorderDrawPile(originalOrder);
            }
        }

        private static bool HasRefillCandidateExcluding(DeckModel deck, int excludeUid)
        {
            for (var i = 0; i < deck.DrawPileUids.Count; i++)
            {
                if (deck.DrawPileUids[i] != excludeUid)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasRefillCandidateExcludingOrder(IReadOnlyList<int> orderedUids, int excludeUid)
        {
            for (var i = 0; i < orderedUids.Count; i++)
            {
                if (orderedUids[i] != excludeUid)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<int> BuildRefillDrawOrder(IReadOnlyList<int> original, int excludeUid)
        {
            var nonFusion = new List<int>(original.Count);
            var fusionTail = new List<int>(1);
            for (var i = 0; i < original.Count; i++)
            {
                if (original[i] == excludeUid)
                {
                    fusionTail.Add(original[i]);
                }
                else
                {
                    nonFusion.Add(original[i]);
                }
            }

            nonFusion.AddRange(fusionTail);
            return nonFusion;
        }

        private static PostKillCardDeal[] FilterDealsExcluding(PostKillCardDeal[] deals, int excludeUid)
        {
            if (deals == null || deals.Length == 0 || excludeUid <= 0)
            {
                return deals ?? Array.Empty<PostKillCardDeal>();
            }

            var filtered = new List<PostKillCardDeal>(deals.Length);
            for (var i = 0; i < deals.Length; i++)
            {
                if (deals[i].Uid != excludeUid)
                {
                    filtered.Add(deals[i]);
                }
            }

            return filtered.Count > 0 ? filtered.ToArray() : Array.Empty<PostKillCardDeal>();
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

            if (!CombatHitSink.TryBeginPresentationLock("UseItem"))
            {
                if (IsStatBoostCard(card.DefId))
                {
                    RestoreHandCardAfterChoiceCancel(card);
                }

                return false;
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
            await VanishParkedBoardSelectItemIfPresentAsync(itemUid);
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
                card = cardManager.SpawnView(itemUid, defId, initialMode: CardDisplayMode.HandCardMode);
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
                    && ((useResult.PostKillBoard.Steps != null && useResult.PostKillBoard.Steps.Length > 0)
                        || (useResult.PostKillBoard.Moves != null && useResult.PostKillBoard.Moves.Length > 0)
                        || (useResult.PostKillBoard.Deals != null && useResult.PostKillBoard.Deals.Length > 0)
                        || (useResult.PostKillBoard.RemovedUids != null && useResult.PostKillBoard.RemovedUids.Length > 0)))
                {
                    await DrainPostKillBoardAsync(useResult.PostKillBoard, ct);
                }
                else
                {
                    SyncBoardOccupancyFromCore();
                }

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

                // 选完关闭覆盖层；Drain 期间由 PresentationLocked 挡输入（外层已持锁则复用）。
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
                        CoreCardPresentationMapper.SyncAllSpawnedCards();
                        UpdateAvatarDebugText();
                        SyncBoardOccupancyFromCore();
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
                SyncBoardOccupancyFromCore(force: true);
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
            SyncBoardOccupancyFromCore();
        }

        /// <summary>
        /// Sync Phase2 位置纠偏短预算（秒）。已在复杂域的卡用 L2 收敛，禁止裸写 Transform.position。
        /// </summary>
        private const float SyncPositionConvergeSeconds = 0.2f;

        private const float SyncPositionEpsilonSqr = 0.0001f;

        private bool ShouldSkipSyncPositionCorrect(ManagedCard card)
        {
            return TryGetSyncPositionSkipVerdict(card, out _);
        }

        /// <summary>
        /// Sync 位置纠偏/落锚门禁。回库途中、净土模式、开放 DeckTween 均不得征用。
        /// </summary>
        private bool TryGetSyncPositionSkipVerdict(ManagedCard card, out string verdict)
        {
            verdict = null;
            if (card == null)
            {
                verdict = "skipNull";
                return true;
            }

            if (fieldManager != null && fieldManager.IsDealInFlight(card.Uid))
            {
                verdict = "skipInFlight";
                return true;
            }

            if (SlotFrameConvergence.IsSlotConvergenceActive(card))
            {
                verdict = "skipInFlight";
                return true;
            }

            var deck = CardDeckManagerSingleton.Instance;
            if (deck != null && deck.IsReturnInFlight(card.Uid))
            {
                verdict = "skipDeckReturnInFlight";
                return true;
            }

            if (card.DisplayMode == CardDisplayMode.CardDeckMode)
            {
                verdict = "skipCardDeckMode";
                return true;
            }

            if (card.DisplayMode == CardDisplayMode.HandCardMode
                || card.DisplayMode == CardDisplayMode.DragCardMode)
            {
                verdict = "skipHandMode";
                return true;
            }

            if (CardDeckTween.IsMotionActive(card.Transform))
            {
                verdict = "skipDeckTween";
                return true;
            }

            return false;
        }

        /// <summary>
        /// 用视觉世界坐标判定是否偏离锚点；飞行中 skip；否则短预算 L2 收敛。禁止裸写 position。
        /// </summary>
        private void CorrectSyncVisualOrSkip(ManagedCard card, int slot, Transform anchor)
        {
            if (card?.Transform == null || anchor == null)
            {
                return;
            }

            var visual = SlotFrameConvergence.GetVisualWorldPosition(card);
            if ((visual - anchor.position).sqrMagnitude <= SyncPositionEpsilonSqr)
            {
                return;
            }

            if (TryGetSyncPositionSkipVerdict(card, out var skipVerdict))
            {
                if (skipVerdict == "skipDeckTween" || skipVerdict == "skipDeckReturnInFlight")
                {
                    CardPresentationProbe.Anomaly(
                        card.Uid,
                        PerfTraceAnomalyCodes.SyncReanchorDuringDeckTween,
                        "slot=" + slot + ";verdict=" + skipVerdict,
                        "Sync.Occupancy.Phase2",
                        layer: "L2",
                        verdict: "skipped");
                }

                CardPresentationProbe.Anomaly(
                    card.Uid,
                    "forceSnap",
                    "slot=" + slot,
                    "Sync.Occupancy.Phase2",
                    layer: "L2",
                    verdict: skipVerdict ?? "skipInFlight");
                return;
            }

            CardPresentationProbe.Anomaly(
                card.Uid,
                "forceSnap",
                "slot=" + slot,
                "Sync.Occupancy.Phase2",
                layer: "L0",
                verdict: "snapHome");
            SlotFrameConvergence.SnapHome(card, anchor.position, "Sync.Occupancy.Phase2", card.Uid);
            cardManager?.RefreshDisplayMode(card);
        }

        /// <summary>
        /// 安全网：两阶段对齐 Core 占格。
        /// 1) 卸下所有与 Core 不一致的占格（含仍在盘面但错位的 uid，避免目标格占用阻塞迁移）；
        /// 2) 按 Core 落位（已有视图迁/放锚，缺失则 Spawn）。
        /// </summary>
        private void SyncBoardOccupancyFromCore(bool force = false)
        {
            if (!force && ShouldDeferBoardTimelineSync())
            {
                _pendingSyncFromCore = true;
                FieldTraceHelper.RecordBoardSyncDeferred("SyncOccupancy", "timelineActive");
                return;
            }

            _pendingSyncFromCore = false;
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
                else if (registry.TryGet(occ.Uid, out var leavingCore)
                         && leavingCore.Zone.Value == ZoneId.DrawPile
                         && cardManager.TryGet(occ.Uid, out var deckView)
                         && deckManager != null)
                {
                    fieldManager.ClearSlotOccupancy(occ.Slot, skipBusyGuard: true);
                    deckManager.LaunchReturnFieldCardToDeck(deckView);
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

                if (deckManager != null
                    && (deckManager.ContainsUid(uid) || deckManager.IsReturnInFlight(uid)))
                {
                    Debug.LogWarning(
                        $"[InBattleManager] Sync 跳过：uid={uid} 在牌组/回库途中，Core 却要求场地格 {slot}");
                    if (cardManager.TryGet(uid, out var deckTransitView) && deckTransitView != null)
                    {
                        var skipVerdict = deckManager.IsReturnInFlight(uid)
                            ? "skipDeckReturnInFlight"
                            : "skipCardDeckMode";
                        CardPresentationProbe.Anomaly(
                            uid,
                            "forceSnap",
                            "slot=" + slot + ";deckTransit",
                            "Sync.Occupancy.Phase2",
                            layer: "L2",
                            verdict: skipVerdict);
                    }

                    continue;
                }

                if (!registry.TryGet(uid, out var coreCard))
                {
                    continue;
                }

                // Core 已离场：禁止 Spawn 幽灵视图。
                if (coreCard.Zone.Value == ZoneId.Graveyard
                    || coreCard.Zone.Value == ZoneId.Removed
                    || coreCard.Zone.Value == ZoneId.DrawPile
                    || coreCard.Zone.Value == ZoneId.ItemSlots)
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
                        CorrectSyncVisualOrSkip(existing, slot, anchor);
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
                                if (TryGetSyncPositionSkipVerdict(view, out var relocateSkip))
                                {
                                    CardPresentationProbe.Anomaly(
                                        uid,
                                        "forceSnap",
                                        "slot=" + slot + ";relocate",
                                        "Sync.Occupancy.Phase2",
                                        layer: "L2",
                                        verdict: relocateSkip ?? "skipInFlight");
                                    continue;
                                }

                                fieldManager.ClearSlotOccupancy(currentSlot, skipBusyGuard: true);
                                CardPresentationProbe.Anomaly(
                                    uid,
                                    "forceSnap",
                                    "slot=" + slot + ";relocate",
                                    "Sync.Occupancy.Phase2",
                                    layer: "L0",
                                    verdict: "snapHome");
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
                                CorrectSyncVisualOrSkip(view, slot, anchor);
                            }
                        }
                        else
                        {
                            if (TryGetSyncPositionSkipVerdict(view, out var reanchorSkip))
                            {
                                CardPresentationProbe.Anomaly(
                                    uid,
                                    "forceSnap",
                                    "slot=" + slot + ";reanchor",
                                    "Sync.Occupancy.Phase2",
                                    layer: "L2",
                                    verdict: reanchorSkip ?? "skipInFlight");
                                continue;
                            }

                            CardPresentationProbe.Anomaly(
                                uid,
                                "forceSnap",
                                "slot=" + slot + ";reanchor",
                                "Sync.Occupancy.Phase2",
                                layer: "L0",
                                verdict: "snapHome");
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

                // 视图缺失：Spawn 后必须落锚点（冷启动允许 SnapHome）。
                view = cardManager.SpawnView(uid, coreCard.DefId, initialMode: CardDisplayMode.GroundCardMode);
                if (view == null)
                {
                    continue;
                }

                CoreCardPresentationMapper.ApplyToManagedCard(view);
                CardPresentationProbe.Anomaly(
                    uid,
                    "forceSnap",
                    "slot=" + slot + ";spawn",
                    "Sync.Occupancy.Phase2",
                    layer: "L2",
                    verdict: "placeAtAnchor");
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

            var reconciled = ReconcileGroundViewsFromCore(avatarUid, hand);
            placed += reconciled;

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
            fieldManager.RefreshSlotHitColliders();

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
        /// 场地视图在 GroundCardMode 但未登记占格、Core 仍要求其在盘面时，强制落锚（Sync Phase 2 漏 place 的孤儿自愈）。
        /// </summary>
        private int ReconcileGroundViewsFromCore(int avatarUid, CardHandManagerSingleton hand)
        {
            ResolveManagers();
            if (cardManager == null || fieldManager == null)
            {
                return 0;
            }

            var board = NineGridArchitecture.Current.GetModel<BoardModel>();
            var reconciled = 0;

            foreach (var pair in cardManager.CardsByUid)
            {
                var uid = pair.Key;
                var view = pair.Value;
                if (uid <= 0 || uid == avatarUid || view == null)
                {
                    continue;
                }

                if (view.DisplayMode != CardDisplayMode.GroundCardMode)
                {
                    continue;
                }

                if (fieldManager.TryGetSlotOf(uid, out _))
                {
                    continue;
                }

                if (hand != null && hand.ContainsUid(uid))
                {
                    continue;
                }

                if (TryGetSyncPositionSkipVerdict(view, out _))
                {
                    continue;
                }

                var coreSlot = -1;
                for (var slot = SlotId.MinBoardIndex; slot <= SlotId.MaxBoardIndex; slot++)
                {
                    if (slot == GroundSlotTopology.AvatarReservedSlot)
                    {
                        continue;
                    }

                    if (board.GetCardUid(SlotId.Board(slot)) == uid)
                    {
                        coreSlot = slot;
                        break;
                    }
                }

                if (coreSlot < 0)
                {
                    continue;
                }

                CardPresentationProbe.Anomaly(
                    uid,
                    "forceSnap",
                    "slot=" + coreSlot + ";reconcileOrphan",
                    "Sync.Occupancy.Reconcile",
                    layer: "L0",
                    verdict: "snapHome");
                fieldManager.RequestPlaceCardAtAnchor(
                    coreSlot,
                    view,
                    skipBusyGuard: true,
                    snapToAnchor: true);
                reconciled++;
            }

            return reconciled;
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
                    || view.DisplayMode == CardDisplayMode.DragCardMode
                    || view.DisplayMode == CardDisplayMode.HandCardMode)
                {
                    continue;
                }

                if (hand != null && hand.ContainsUid(uid))
                {
                    continue;
                }

                // Pickup 进行中：手牌尚未 ContainsUid 的窗口，勿误 Sweep。
                if (hand != null
                    && (hand.IsBusy || hand.IsDragging)
                    && (view.DisplayMode == CardDisplayMode.HandCardMode
                        || view.DisplayMode == CardDisplayMode.DragCardMode))
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
                    if (coreCard.Zone.Value == ZoneId.ItemSlots)
                    {
                        continue;
                    }

                    if (coreCard.Zone.Value == ZoneId.DrawPile
                        && deck != null
                        && !deck.ContainsUid(uid))
                    {
                        deck.LaunchReturnFieldCardToDeck(view);
                        continue;
                    }

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
            var factory = new ExploreIntentScriptFactory(
                arch,
                _coreCommandDispatcher,
                _explorePresentChannel,
                OnExploreBatchProjected);
            _presentationDirector = new PresentationDirector(factory);
        }

        private void TeardownPresentationDirector(IntentClearReason reason)
        {
            if (_presentationDirector != null)
            {
                _presentationDirector.HardClearIntents(reason);
            }

            _presentationDirector = null;
            _explorePresentChannel = null;
            _coreCommandDispatcher = null;
            CombatHitSink.DirectorMainlineBusy = false;
        }

        private static bool TrySubmitExploreIntentFromCards(int groundSlot)
        {
            var instance = Instance;
            if (instance == null)
            {
                Debug.LogWarning("[InBattleManager] TrySubmitExploreIntent：无局内管理器。");
                return false;
            }

            instance.EnsurePresentationDirector();
            bool preview;
            var accepted = instance._presentationDirector.TrySubmitIntent(
                new InputIntent(InputIntentKinds.Explore, groundSlot),
                out preview);
            // 同帧同步 busy，避免等 Update 前出现门禁空窗。
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
    }
}
