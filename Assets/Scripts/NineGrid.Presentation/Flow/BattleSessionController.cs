using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation;
using NineGrid.Presentation.Controllers;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 局内会话场景 View / 生命周期壳：场景绑定与桥接注册；业务由 <see cref="IBattleSessionSystem"/> 持有。
    /// </summary>
    public sealed class BattleSessionController : MonoBehaviour, IBattleSessionView
    {
        [Tooltip("卡牌宿主；由 PresentationSceneRoot.BindSceneHosts 注入，也可手动拖入。")]
        [SerializeField] private CardManagerSingleton cardManager;

        [Tooltip("牌库宿主；由 PresentationSceneRoot.BindSceneHosts 注入，也可手动拖入。")]
        [SerializeField] private CardDeckManagerSingleton deckManager;

        [Tooltip("场地宿主；由 PresentationSceneRoot.BindSceneHosts 注入，也可手动拖入。")]
        [SerializeField] private GroundFieldView fieldManager;

        [Tooltip("遗物栏宿主；由 PresentationSceneRoot.BindSceneHosts 注入，也可手动拖入。")]
        [SerializeField] private RelicManagerSingleton relicManager;

        [Tooltip("手牌宿主；由 PresentationSceneRoot.BindSceneHosts 注入，也可手动拖入。")]
        [SerializeField] private CardHandManagerSingleton handManager;

        [Tooltip("交战宿主；由 PresentationSceneRoot.BindSceneHosts 注入，也可手动拖入。")]
        [SerializeField] private FieldBattleView battleManager;

        [Tooltip("主流程宿主；由 PresentationSceneRoot.BindSceneHosts 注入，也可手动拖入。保留字段但不做业务调用。")]
        [SerializeField] private GameFlowController mainGameLoop;

        [Tooltip("选择器宿主；留空则会话侧兜底查找。")]
        [SerializeField] private SelectorManagerSingleton selectorManager;

        [Tooltip("面板路由；留空则输出投影侧查找。")]
        [SerializeField] private UiPanelRouter panelRouter;

        private bool _fieldSignalSubscribed;
        private Func<bool> _ensurePresentationRuntime;
        private Action<IntentClearReason> _shutdownPresentationRuntime;

        public CardManagerSingleton CardManager => cardManager;
        public CardDeckManagerSingleton DeckManager => deckManager;
        public GroundFieldView FieldManager => fieldManager;
        public RelicManagerSingleton RelicManager => relicManager;
        public CardHandManagerSingleton HandManager => handManager;
        public SelectorManagerSingleton SelectorManager => selectorManager;
        public FieldBattleView BattleManager => battleManager;
        public UiPanelRouter PanelRouter => panelRouter;

        public bool IsBusy => SessionOrNull()?.IsBusy ?? false;

        private event Action _nodeSettlementReady;

        public event Action OnNodeSettlementReady
        {
            add => _nodeSettlementReady += value;
            remove => _nodeSettlementReady -= value;
        }

        private Action _forwardSettlementReady;

        public void BindSceneHosts(
            CardManagerSingleton cards,
            CardDeckManagerSingleton deck,
            GroundFieldView field,
            RelicManagerSingleton relic,
            CardHandManagerSingleton hand,
            FieldBattleView battle,
            GameFlowController loop)
        {
            if (cards != null) cardManager = cards;
            if (deck != null) deckManager = deck;
            if (field != null) fieldManager = field;
            if (relic != null) relicManager = relic;
            if (hand != null) handManager = hand;
            if (battle != null) battleManager = battle;
            if (loop != null) mainGameLoop = loop;
        }

        public void BindRuntimeLifecycle(
            Func<bool> ensureInstalled,
            Action<IntentClearReason> shutdown)
        {
            _ensurePresentationRuntime = ensureInstalled;
            _shutdownPresentationRuntime = shutdown;
        }

        public bool EnsurePresentationRuntimeInstalled()
        {
            if (_ensurePresentationRuntime != null)
            {
                return _ensurePresentationRuntime();
            }

            Debug.LogWarning(
                "[BattleSession] PresentationSceneRoot 未绑定 Runtime 生命周期，无法安装导演。");
            return false;
        }

        public void ShutdownPresentationRuntime(IntentClearReason reason)
        {
            if (_shutdownPresentationRuntime != null)
            {
                _shutdownPresentationRuntime(reason);
                return;
            }

            SessionOrNull()?.ClearPresentChannels();
        }

        private void Awake()
        {
            BattleSessionPresentationController.WireSession(this);
            var session = RequireSession();
            _forwardSettlementReady = () => _nodeSettlementReady?.Invoke();
            session.OnNodeSettlementReady += _forwardSettlementReady;
            SubscribeFieldSignal();
            RegisterSceneBridges();
            session.RegisterPresentationIntentHandlers();
        }

        private void OnDestroy()
        {
            var session = SessionOrNull();
            if (session != null && _forwardSettlementReady != null)
            {
                session.OnNodeSettlementReady -= _forwardSettlementReady;
            }

            session?.CancelPresentationWork();
            UnsubscribeFieldSignal();
            UnregisterSceneBridges();
            session?.UnregisterPresentationIntentHandlers();
            session?.UnbindIfView(this);
        }

        public InitialGameSnapshot BootstrapRun(
            InitialGameOptions options = null,
            bool preserveRunInventory = false)
        {
            return RequireSession().BootstrapRun(options, preserveRunInventory);
        }

        public void ClearPresentationSurface()
        {
            RequireSession().ClearPresentationSurface();
        }

        public void ClearCardPresentationSurface()
        {
            RequireSession().ClearCardPresentationSurface();
        }

        public void RefreshPersistentInBattleUi(bool animate = false)
        {
            RequireSession().RefreshPersistentInBattleUi(animate);
        }

        public UniTask StartBattleNodeAsync(
            NodeDeckOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return RequireSession().StartBattleNodeAsync(options, cancellationToken);
        }

        public void NotifyPresentationBoardMayBeClear()
        {
            SessionOrNull()?.NotifyPresentationBoardMayBeClear();
        }

        public bool TryEnterNodeSettlement()
        {
            return SessionOrNull()?.TryEnterNodeSettlement() ?? false;
        }

        public UniTask PresentRewardChoiceFromCoreAsync(bool hoverOnNotice = false)
        {
            return RequireSession().PresentRewardChoiceFromCoreAsync(hoverOnNotice);
        }

        public UniTask PresentUnusedHelpCardSettlementFromEventLogAsync(
            int startIndex,
            CancellationToken cancellationToken = default)
        {
            return RequireSession().PresentUnusedHelpCardSettlementFromEventLogAsync(
                startIndex,
                cancellationToken);
        }

        private void SubscribeFieldSignal()
        {
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

        private void RegisterSceneBridges()
        {
            var session = RequireSession();
            RewardChoiceCoreHook.RequestWire();
            RelicHudHook.RequestWire();
            DiagnosticOutputHook.RequestAttach();
            RegisterHandBridge();
        }

        private void UnregisterSceneBridges()
        {
            DiagnosticOutputHook.RequestDetach();
            PresentationInputGates.ForceEndExternalHold("UnregisterSceneBridges");
            // Runtime Shutdown 由 PresentationSceneRoot / CompositionRoot 负责；此处仅清 Channel。
            SessionOrNull()?.ClearPresentChannels();
            UnregisterHandBridge();
        }

        private void RegisterHandBridge()
        {
            var hand = handManager ?? CardEntityLifecycleHook.HandOrNull();
            if (hand == null)
            {
                return;
            }

            hand.DragApplyValidator -= ValidateHandDragApplyAsync;
            hand.DragApplyValidator += ValidateHandDragApplyAsync;
            UseItemInputHook.RequestWire(hand);
            RecycleItemInputHook.RequestWire(hand);
            PickupInputHook.RequestWire(hand);

            CardEntityLifecycleHook.RequestWire(cardManager, hand, deckManager);
            if (fieldManager != null)
            {
                GroundFieldGeometryHook.RequestWire(fieldManager);
            }

            var battle = battleManager ?? FieldBattlePresentationHook.BattleOrNull();
            if (battle != null)
            {
                FieldBattlePresentationHook.RequestWire(battle);
            }

            BoardSelectionSystem.EnsureRegistered();
            ChoicePresentationSystem.EnsureRegistered();

            BoardCardSelectModeController.SelectionCompletedAsync -= OnBoardSelectionCompletedAsync;
            BoardCardSelectModeController.SelectionCompletedAsync += OnBoardSelectionCompletedAsync;
            BoardCardSelectModeController.SelectionAbortedAsync -= OnBoardSelectionAbortedAsync;
            BoardCardSelectModeController.SelectionAbortedAsync += OnBoardSelectionAbortedAsync;
        }

        private void UnregisterHandBridge()
        {
            var hand = handManager ?? CardEntityLifecycleHook.HandOrNull();
            if (hand != null)
            {
                hand.DragApplyValidator -= ValidateHandDragApplyAsync;
            }

            BoardSelectionSystem.EnsureRegistered().AbortBoardSelectIfActive("unregister-hand-bridge");
            BoardCardSelectModeController.SelectionCompletedAsync -= OnBoardSelectionCompletedAsync;
            BoardCardSelectModeController.SelectionAbortedAsync -= OnBoardSelectionAbortedAsync;
            BoardCardSelectModeController.End();
        }

        private UniTask<bool> ValidateHandDragApplyAsync(ManagedCard card, int? targetGroundSlot)
        {
            return BoardSelectionSystem.EnsureRegistered().ValidateHandDragApplyAsync(card, targetGroundSlot);
        }

        private UniTask OnBoardSelectionCompletedAsync(int itemUid, int[] selectedUids)
        {
            return BoardSelectionSystem.EnsureRegistered().OnBoardSelectionCompletedAsync(itemUid, selectedUids);
        }

        private UniTask OnBoardSelectionAbortedAsync(int itemUid, string defId, string reason)
        {
            return BoardSelectionSystem.EnsureRegistered().OnBoardSelectionAbortedAsync(itemUid, defId, reason);
        }

        private static IBattleSessionSystem SessionOrNull()
        {
            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            return arch?.GetSystem<IBattleSessionSystem>();
        }

        private static IBattleSessionSystem RequireSession()
        {
            return BattleSessionSystem.EnsureRegistered();
        }
    }
}
