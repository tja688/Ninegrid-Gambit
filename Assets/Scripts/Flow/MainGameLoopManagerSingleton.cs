using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using QFramework;
using TMPro;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NineGrid.Flow
{
    /// <summary>
    /// 主游戏流程壳状态机：主菜单 → 局内 → 通关奖励 → 房间二选一 → 房间事件 → 下一节点。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainGameLoopManagerSingleton : MonoBehaviour
    {
        public enum LoopState
        {
            MainMenu,
            BattleStub,
            RewardChoice,
            RoomChoice,
            RoomEvent,
            VictoryNotice,
            DefeatNotice,
        }

        private static MainGameLoopManagerSingleton _instance;

        [Header("Refs")]
        [Tooltip("面板路由；留空则运行时在同物体上 GetComponent / AddComponent。")]
        [SerializeField] private UiPanelRouter panelRouter;

        [Tooltip("选择器管理；留空则运行时取 SelectorManagerSingleton.Instance。")]
        [SerializeField] private SelectorManagerSingleton selectorManager;

        [Tooltip("局内管理；留空则运行时取 InBattleManagerSingleton.Instance。")]
        [SerializeField] private InBattleManagerSingleton inBattleManager;

        [Tooltip("主菜单「开始」按钮；留空则运行时查找 MainPanel/StartRun。")]
        [SerializeField] private Collider2D startRunHit;

        [Tooltip("主菜单「退出」按钮；留空则运行时查找 MainPanel/QuitGame。")]
        [SerializeField] private Collider2D quitGameHit;

        [Tooltip("Notice Text；留空则运行时查找 TableNine Text Overlay UI/Notice Text。")]
        [SerializeField] private TextMeshProUGUI noticeText;

        [Tooltip("点选相机；留空则运行时取 Camera.main。")]
        [SerializeField] private Camera worldCamera;

        [Header("Timing")]
        [Tooltip("房间事件即时结算 Notice 展示时长（秒）。")]
        [SerializeField] private float roomEventStubSeconds = 0.6f;

        [Tooltip("胜利 Notice 展示时长（秒）后回主菜单。")]
        [SerializeField] private float victoryNoticeSeconds = 1f;

        [Tooltip("失败 Notice 展示时长（秒）后回主菜单。")]
        [SerializeField] private float defeatNoticeSeconds = 1f;

        [Tooltip("胜利文案。")]
        [SerializeField] private string victoryMessage = "胜利";

        [Tooltip("失败文案。")]
        [SerializeField] private string defeatMessage = "失败";

        private LoopState _state = LoopState.MainMenu;
        private bool _isBusy;
        private bool _testMode;
        private int _nodeIndex;
        private CancellationTokenSource _loopCts;
        private CancellationTokenSource _battleEndCts;
        private UniTaskCompletionSource _settlementTcs;
        private bool _subscribedSettlement;

        public static MainGameLoopManagerSingleton Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<MainGameLoopManagerSingleton>();
                    if (_instance == null)
                    {
                        var go = new GameObject(nameof(MainGameLoopManagerSingleton));
                        _instance = go.AddComponent<MainGameLoopManagerSingleton>();
                    }
                }

                return _instance;
            }
        }

        public LoopState State => _state;
        public bool IsTestMode => _testMode;
        public int NodeIndex => _nodeIndex;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            EnsureBindings();
            SubscribeSettlement();
            EnterMainMenuImmediate();
        }

        private void OnDestroy()
        {
            UnsubscribeSettlement();
            CancelLoopWork();
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            if (_state != LoopState.MainMenu || _isBusy)
            {
                return;
            }

            if (!WorldPointerUtility.WasPrimaryPressedThisFrame())
            {
                return;
            }

            EnsureBindings();
            if (WorldPointerUtility.TryPickCollider(worldCamera, startRunHit))
            {
                BeginRun(testMode: true);
                return;
            }

            if (WorldPointerUtility.TryPickCollider(worldCamera, quitGameHit))
            {
                QuitGame();
            }
        }

        /// <summary>
        /// DevTest / 按钮入口：开启一局主循环（默认测试模式）。
        /// </summary>
        public void BeginRun(bool testMode = true)
        {
            if (_isBusy && _state != LoopState.MainMenu)
            {
                Debug.LogWarning("[MainGameLoop] 当前循环仍在进行，忽略 BeginRun。");
                return;
            }

            EnsureBindings();
            SubscribeSettlement();
            CancelLoopWork();
            _loopCts = new CancellationTokenSource();

            _testMode = testMode;
            _nodeIndex = 0;
            HideNotice();
            panelRouter.ShowInRunShell(inBattle: true);
            RunNodeCycleAsync(_loopCts.Token).Forget();
        }

        /// <summary>
        /// 强制停循环并回主菜单。
        /// </summary>
        public void ReturnToMainMenu()
        {
            CancelLoopWork();
            CancelBattleEndWork();
            if (selectorManager != null && selectorManager.IsChoiceActive)
            {
                selectorManager.HideChoice();
            }

            EnterMainMenuImmediate();
        }

        /// <summary>
        /// 整局胜利：Notice → 等待 → 回主菜单。
        /// </summary>
        public void NotifyBattleVictory()
        {
            ShowBattleEndAndReturnAsync(victory: true).Forget();
        }

        /// <summary>
        /// 玩家战败：Notice → 等待 → 回主菜单。
        /// </summary>
        public void NotifyBattleDefeat()
        {
            ShowBattleEndAndReturnAsync(victory: false).Forget();
        }

        private async UniTaskVoid RunNodeCycleAsync(CancellationToken ct)
        {
            _isBusy = true;
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    _nodeIndex++;
                    await PlayRealBattleAsync(ct);
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    await PlayRewardChoiceAsync(ct);
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    await PlayRoomChoiceAsync(ct);
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    await PlayRoomEventAsync(ct);
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    var phase = NineGridArchitecture.Current.GetSystem<IPhaseSystem>().CurrentPhase;
                    if (phase == GamePhase.Victory)
                    {
                        await ShowVictoryAndReturnAsync(ct);
                        return;
                    }

                    // 继续下一节点（Core 已 AdvanceNode → NodeCompleted）
                }
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            finally
            {
                _isBusy = false;
            }
        }

        private async UniTask PlayRealBattleAsync(CancellationToken ct)
        {
            SetState(LoopState.BattleStub);
            EnsureBindings();
            panelRouter.ShowInRunShell(inBattle: true);

            if (inBattleManager == null)
            {
                inBattleManager = InBattleManagerSingleton.Instance;
            }

            if (inBattleManager == null)
            {
                Debug.LogError("[MainGameLoop] 未找到 InBattleManagerSingleton，无法入场。");
                return;
            }

            SubscribeSettlement();
            _settlementTcs = new UniTaskCompletionSource();

            var arch = NineGridArchitecture.Current;
            var phase = arch.GetSystem<IPhaseSystem>();
            if (_nodeIndex <= 1 || !phase.CanExecute(GameCommandKind.StartNode))
            {
                inBattleManager.BootstrapRun();
            }

            CoreCardPresentationMapper.EnsureContentCatalogLoaded();

            var options = arch.GetSystem<IRewardSystem>().BuildNodeDeckOptions(_nodeIndex, monsterDeckId: null);
            if (options == null)
            {
                options = NodeDeckOptions.CreateDefaultBattle();
            }

            Debug.Log($"[MainGameLoop] 节点 {_nodeIndex} 真实局内入场");
            await inBattleManager.StartBattleNodeAsync(options, ct);
            if (ct.IsCancellationRequested)
            {
                return;
            }

            // 开局即空怪时 StartBattleNode 内可能已 Raise 结算；补一次探测。
            inBattleManager.TryEnterNodeSettlement();

            Debug.Log($"[MainGameLoop] 节点 {_nodeIndex} 已入场，等待节点结算");
            await _settlementTcs.Task.AttachExternalCancellation(ct);
            Debug.Log($"[MainGameLoop] 节点 {_nodeIndex} 结算就绪，进入奖励");
        }

        private async UniTask PlayRewardChoiceAsync(CancellationToken ct)
        {
            SetState(LoopState.RewardChoice);
            EnsureBindings();

            var arch = NineGridArchitecture.Current;
            var phase = arch.GetSystem<IPhaseSystem>().CurrentPhase;
            var pending = arch.GetModel<PendingChoiceModel>();
            if (phase != GamePhase.RewardItemChoice
                || pending.Kind.Value != PendingChoiceKind.Reward
                || pending.RewardOptions == null
                || pending.RewardOptions.Count == 0)
            {
                Debug.LogWarning(
                    $"[MainGameLoop] 跳过通关奖励 phase={phase} pending={pending.Kind.Value}");
                return;
            }

            panelRouter.ShowRewardOverlay();
            CombatHitSink.ChoiceOverlayActive = true;
            try
            {
                await inBattleManager.PresentRewardChoiceFromCoreAsync(hoverOnNotice: true);
            }
            finally
            {
                CombatHitSink.ChoiceOverlayActive = false;
                panelRouter.HideAllOverlays();
            }

            await UniTask.Yield(cancellationToken: ct);
        }

        private async UniTask PlayRoomChoiceAsync(CancellationToken ct)
        {
            SetState(LoopState.RoomChoice);
            EnsureBindings();

            var arch = NineGridArchitecture.Current;
            var phaseSystem = arch.GetSystem<IPhaseSystem>();
            var pending = arch.GetModel<PendingChoiceModel>();
            if (phaseSystem.CurrentPhase != GamePhase.RoomChoice
                || pending.Kind.Value != PendingChoiceKind.Room
                || pending.RoomOptions == null
                || pending.RoomOptions.Count < 2)
            {
                Debug.LogWarning(
                    $"[MainGameLoop] 跳过房间选择 phase={phaseSystem.CurrentPhase} pending={pending.Kind.Value}");
                return;
            }

            var left = pending.RoomOptions[0];
            var right = pending.RoomOptions[1];
            panelRouter.ShowRoomChoiceOverlay();

            var pickedIndex = -1;
            var finished = false;
            CombatHitSink.ChoiceOverlayActive = true;
            try
            {
                selectorManager.BeginRoomChoice(
                    left.ToString(),
                    right.ToString(),
                    (index, optionId) =>
                    {
                        pickedIndex = index;
                        Debug.Log($"[MainGameLoop] 房间已选 index={index} id={optionId}");
                    },
                    () => { finished = true; },
                    hoverOnNotice: true);

                await UniTask.WaitUntil(() => finished || ct.IsCancellationRequested, cancellationToken: ct);
            }
            finally
            {
                CombatHitSink.ChoiceOverlayActive = false;
                panelRouter.HideAllOverlays();
            }

            if (ct.IsCancellationRequested)
            {
                return;
            }

            if (pickedIndex < 0)
            {
                pickedIndex = 0;
            }

            var result = phaseSystem.SelectRoom(pickedIndex);
            if (!result.Accepted)
            {
                Debug.LogWarning($"[MainGameLoop] SelectRoom 被拒: {result.Reason}");
            }

            PlayerInfoHudPresenter.TryGetInstance()?.SyncFromCore(animate: true);
        }

        private async UniTask PlayRoomEventAsync(CancellationToken ct)
        {
            SetState(LoopState.RoomEvent);
            EnsureBindings();

            var arch = NineGridArchitecture.Current;
            var phaseSystem = arch.GetSystem<IPhaseSystem>();
            if (phaseSystem.CurrentPhase != GamePhase.RoomEvent)
            {
                Debug.LogWarning($"[MainGameLoop] 跳过房间事件 phase={phaseSystem.CurrentPhase}");
                return;
            }

            var selectedRoom = arch.GetModel<PendingChoiceModel>().SelectedRoom.Value;
            panelRouter.ShowRoomEventOverlay();

            var enter = phaseSystem.EnterRoom();
            if (!enter.Accepted)
            {
                Debug.LogWarning($"[MainGameLoop] EnterRoom 被拒: {enter.Reason}");
                panelRouter.HideAllOverlays();
                return;
            }

            PlayerInfoHudPresenter.TryGetInstance()?.SyncFromCore(animate: true);

            var pending = arch.GetModel<PendingChoiceModel>();
            if (pending.Kind.Value == PendingChoiceKind.Reward
                && pending.RewardOptions != null
                && pending.RewardOptions.Count > 0)
            {
                // 商店 / 宝箱房：复用 Bounce 默认选择器。
                panelRouter.ShowRewardOverlay();
                CombatHitSink.ChoiceOverlayActive = true;
                try
                {
                    await inBattleManager.PresentRewardChoiceFromCoreAsync(hoverOnNotice: true);
                }
                finally
                {
                    CombatHitSink.ChoiceOverlayActive = false;
                }
            }
            else
            {
                var message = BuildRoomResolvedNotice(selectedRoom);
                if (!string.IsNullOrEmpty(message))
                {
                    ShowNotice(message);
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(Mathf.Max(0.05f, roomEventStubSeconds)),
                        cancellationToken: ct);
                    HideNotice();
                }
            }

            panelRouter.HideAllOverlays();
            PlayerInfoHudPresenter.TryGetInstance()?.SyncFromCore(animate: true);
        }

        private static string BuildRoomResolvedNotice(RoomKind room)
        {
            CoreCardPresentationMapper.EnsureContentCatalogLoaded();
            var arch = NineGridArchitecture.Current;
            if (arch != null)
            {
                var content = arch.GetSystem<IContentSystem>();
                if (content != null
                    && content.HasCatalog
                    && content.Catalog.Rewards.TryGetRoom(room, out var def)
                    && !string.IsNullOrWhiteSpace(def.DisplayName))
                {
                    if (def.GoldDelta != 0)
                    {
                        return $"{def.DisplayName}：金币{(def.GoldDelta > 0 ? "+" : string.Empty)}{def.GoldDelta}";
                    }

                    if (def.MaxHpDelta != 0 || def.HealToFull)
                    {
                        return def.HealToFull
                            ? $"{def.DisplayName}：血量上限+{def.MaxHpDelta}，已回满"
                            : $"{def.DisplayName}：血量上限+{def.MaxHpDelta}";
                    }

                    return def.DisplayName;
                }
            }

            return room == RoomKind.None ? string.Empty : room.ToString();
        }

        private async UniTask ShowVictoryAndReturnAsync(CancellationToken ct)
        {
            await ShowBattleEndAndReturnAsync(victory: true, ct);
        }

        private async UniTaskVoid ShowBattleEndAndReturnAsync(bool victory)
        {
            CancelBattleEndWork();
            _battleEndCts = new CancellationTokenSource();
            try
            {
                await ShowBattleEndAndReturnAsync(victory, _battleEndCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async UniTask ShowBattleEndAndReturnAsync(bool victory, CancellationToken ct)
        {
            CancelLoopWork();
            if (inBattleManager == null)
            {
                inBattleManager = InBattleManagerSingleton.Instance;
            }

            inBattleManager?.ClearPresentationSurface();
            FieldBattleManagerSingleton.Instance?.CancelBattleWork();

            SetState(victory ? LoopState.VictoryNotice : LoopState.DefeatNotice);
            EnsureBindings();
            panelRouter.ShowInRunShell(inBattle: false);
            var message = victory
                ? (string.IsNullOrWhiteSpace(victoryMessage) ? "胜利" : victoryMessage)
                : (string.IsNullOrWhiteSpace(defeatMessage) ? "失败" : defeatMessage);
            ShowNotice(message);
            Debug.Log(victory
                ? "[MainGameLoop] 整局胜利，准备回主菜单。"
                : "[MainGameLoop] 战斗失败，准备回主菜单。");
            var seconds = victory
                ? Mathf.Max(0.2f, victoryNoticeSeconds)
                : Mathf.Max(0.2f, defeatNoticeSeconds);
            await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: ct);
            EnterMainMenuImmediate();
        }

        private void EnterMainMenuImmediate()
        {
            EnsureBindings();
            HideNotice();
            FieldBattleManagerSingleton.Instance?.CancelBattleWork();
            inBattleManager?.ClearPresentationSurface();
            panelRouter.ShowMainMenu();
            SetState(LoopState.MainMenu);
            _testMode = false;
            _nodeIndex = 0;
            _isBusy = false;
            _settlementTcs = null;
        }

        private void SubscribeSettlement()
        {
            EnsureBindings();
            if (inBattleManager == null)
            {
                inBattleManager = InBattleManagerSingleton.Instance;
            }

            if (inBattleManager == null || _subscribedSettlement)
            {
                return;
            }

            inBattleManager.OnNodeSettlementReady += OnNodeSettlementReady;
            _subscribedSettlement = true;
        }

        private void UnsubscribeSettlement()
        {
            if (!_subscribedSettlement || inBattleManager == null)
            {
                return;
            }

            inBattleManager.OnNodeSettlementReady -= OnNodeSettlementReady;
            _subscribedSettlement = false;
        }

        private void OnNodeSettlementReady()
        {
            _settlementTcs?.TrySetResult();
        }

        private void CancelBattleEndWork()
        {
            if (_battleEndCts == null)
            {
                return;
            }

            _battleEndCts.Cancel();
            _battleEndCts.Dispose();
            _battleEndCts = null;
        }

        private void ShowNotice(string message)
        {
            EnsureBindings();
            if (noticeText == null)
            {
                Debug.LogWarning($"[MainGameLoop] NoticeText 缺失，文案：{message}");
                return;
            }

            noticeText.text = message;
            noticeText.gameObject.SetActive(true);
        }

        private void HideNotice()
        {
            if (noticeText != null)
            {
                noticeText.gameObject.SetActive(false);
            }
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetState(LoopState next)
        {
            _state = next;
        }

        private void CancelLoopWork()
        {
            _settlementTcs?.TrySetCanceled();
            _settlementTcs = null;

            if (_loopCts == null)
            {
                return;
            }

            _loopCts.Cancel();
            _loopCts.Dispose();
            _loopCts = null;
        }

        private void EnsureBindings()
        {
            if (panelRouter == null)
            {
                panelRouter = GetComponent<UiPanelRouter>();
                if (panelRouter == null)
                {
                    panelRouter = gameObject.AddComponent<UiPanelRouter>();
                }
            }

            panelRouter.EnsureBindings();

            if (selectorManager == null)
            {
                selectorManager = SelectorManagerSingleton.Instance;
            }

            if (inBattleManager == null)
            {
                inBattleManager = InBattleManagerSingleton.Instance;
            }

            worldCamera = WorldPointerUtility.ResolveCamera(worldCamera);

            if (startRunHit == null)
            {
                var start = FindDeep("StartRun");
                if (start != null)
                {
                    startRunHit = start.GetComponent<Collider2D>();
                }
            }

            if (quitGameHit == null)
            {
                var quit = FindDeep("QuitGame");
                if (quit != null)
                {
                    quitGameHit = quit.GetComponent<Collider2D>();
                }
            }

            if (noticeText == null)
            {
                var noticeGo = FindDeep("Notice Text");
                if (noticeGo != null)
                {
                    noticeText = noticeGo.GetComponent<TextMeshProUGUI>();
                }
            }
        }

        private static GameObject FindDeep(string objectName)
        {
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null || t.name != objectName || !t.gameObject.scene.IsValid())
                {
                    continue;
                }

                return t.gameObject;
            }

            return null;
        }
    }
}
