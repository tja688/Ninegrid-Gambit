using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Flow.Diagnostics;
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
            try
            {
                // 失败重开：先落盘上一局，再开新 session，避免 Flow 粘连 / Battle 被 Bootstrap 清掉后对不上。
                BattleTraceRecorder.RotateSessionForNewRun();
                FlowTraceRecorder.Record(
                    FlowTraceCategory.UI,
                    FlowTraceNames.StartRun,
                    new Dictionary<string, string>
                    {
                        { "testMode", testMode ? "true" : "false" },
                    },
                    loopState: _state.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MainGameLoop] FlowTrace StartRun: " + ex.Message);
            }

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

            try
            {
                FlowTraceRecorder.Record(
                    FlowTraceCategory.UI,
                    FlowTraceNames.ReturnMainMenu,
                    loopState: _state.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MainGameLoop] FlowTrace ReturnMainMenu: " + ex.Message);
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
                try
                {
                    FlowTraceRecorder.Record(
                        FlowTraceCategory.CoreGate,
                        FlowTraceNames.RewardPresented,
                        new Dictionary<string, string>
                        {
                            { "skipped", "true" },
                            { "phase", phase.ToString() },
                            { "pending", pending.Kind.Value.ToString() },
                            { "nodeIndex", _nodeIndex.ToString() },
                        },
                        loopState: _state.ToString(),
                        phaseBefore: phase.ToString(),
                        accepted: false);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[MainGameLoop] FlowTrace RewardSkipped: " + ex.Message);
                }

                return;
            }

            try
            {
                var optionIds = new List<string>(pending.RewardOptions.Count);
                for (var i = 0; i < pending.RewardOptions.Count; i++)
                {
                    optionIds.Add(pending.RewardOptions[i].DefId ?? string.Empty);
                }

                FlowTraceRecorder.Record(
                    FlowTraceCategory.CoreGate,
                    FlowTraceNames.RewardPresented,
                    new Dictionary<string, string>
                    {
                        { "optionCount", pending.RewardOptions.Count.ToString() },
                        { "options", string.Join(",", optionIds) },
                        { "nodeIndex", _nodeIndex.ToString() },
                        { "source", "nodeClear" },
                    },
                    loopState: _state.ToString(),
                    phaseBefore: phase.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MainGameLoop] FlowTrace RewardPresented: " + ex.Message);
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
                try
                {
                    FlowTraceRecorder.Record(
                        FlowTraceCategory.CoreGate,
                        FlowTraceNames.RoomPresented,
                        new Dictionary<string, string>
                        {
                            { "skipped", "true" },
                            { "phase", phaseSystem.CurrentPhase.ToString() },
                            { "pending", pending.Kind.Value.ToString() },
                            { "nodeIndex", _nodeIndex.ToString() },
                        },
                        loopState: _state.ToString(),
                        phaseBefore: phaseSystem.CurrentPhase.ToString(),
                        accepted: false);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[MainGameLoop] FlowTrace RoomSkipped: " + ex.Message);
                }

                return;
            }

            var left = pending.RoomOptions[0];
            var right = pending.RoomOptions[1];
            try
            {
                FlowTraceRecorder.Record(
                    FlowTraceCategory.CoreGate,
                    FlowTraceNames.RoomPresented,
                    new Dictionary<string, string>
                    {
                        { "left", left.ToString() },
                        { "right", right.ToString() },
                        { "nodeIndex", _nodeIndex.ToString() },
                    },
                    loopState: _state.ToString(),
                    phaseBefore: phaseSystem.CurrentPhase.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MainGameLoop] FlowTrace RoomPresented: " + ex.Message);
            }

            panelRouter.ShowRoomChoiceOverlay();

            var pickedIndex = -1;
            var pickedId = string.Empty;
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
                        pickedId = optionId ?? string.Empty;
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

            var phaseBeforeSelect = phaseSystem.CurrentPhase.ToString();
            var pipeline = arch.GetSystem<IActionPipelineSystem>();
            var goldEventStart = pipeline.EventLog.Entries.Count;
            var result = phaseSystem.SelectRoom(pickedIndex);
            if (!result.Accepted)
            {
                Debug.LogWarning($"[MainGameLoop] SelectRoom 被拒: {result.Reason}");
            }

            try
            {
                FlowTraceRecorder.Record(
                    FlowTraceCategory.CoreGate,
                    FlowTraceNames.RoomChosen,
                    new Dictionary<string, string>
                    {
                        { "index", pickedIndex.ToString() },
                        { "optionId", string.IsNullOrEmpty(pickedId) ? pickedIndex.ToString() : pickedId },
                        { "reason", result.Reason ?? string.Empty },
                        { "nodeIndex", _nodeIndex.ToString() },
                    },
                    loopState: _state.ToString(),
                    phaseBefore: phaseBeforeSelect,
                    phaseAfter: phaseSystem.CurrentPhase.ToString(),
                    accepted: result.Accepted);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MainGameLoop] FlowTrace RoomChosen: " + ex.Message);
            }

            // 未使用帮助卡结算等：先按 EventLog 开演，再静默对齐 HUD。
            InBattleManagerSingleton.PresentGoldGainsFromEventLog(goldEventStart);
            PlayerInfoHudPresenter.TryGetInstance()?.SyncFromCore(animate: false);
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

            var phaseBeforeEnter = phaseSystem.CurrentPhase.ToString();
            var pipeline = arch.GetSystem<IActionPipelineSystem>();
            var goldEventStart = pipeline.EventLog.Entries.Count;
            var enter = phaseSystem.EnterRoom();
            if (!enter.Accepted)
            {
                Debug.LogWarning($"[MainGameLoop] EnterRoom 被拒: {enter.Reason}");
                try
                {
                    FlowTraceRecorder.Record(
                        FlowTraceCategory.CoreGate,
                        FlowTraceNames.EnterRoom,
                        new Dictionary<string, string>
                        {
                            { "room", selectedRoom.ToString() },
                            { "reason", enter.Reason ?? string.Empty },
                            { "nodeIndex", _nodeIndex.ToString() },
                        },
                        loopState: _state.ToString(),
                        phaseBefore: phaseBeforeEnter,
                        phaseAfter: phaseSystem.CurrentPhase.ToString(),
                        accepted: false);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[MainGameLoop] FlowTrace EnterRoom reject: " + ex.Message);
                }

                panelRouter.HideAllOverlays();
                return;
            }

            try
            {
                FlowTraceRecorder.Record(
                    FlowTraceCategory.CoreGate,
                    FlowTraceNames.EnterRoom,
                    new Dictionary<string, string>
                    {
                        { "room", selectedRoom.ToString() },
                        { "nodeIndex", _nodeIndex.ToString() },
                    },
                    loopState: _state.ToString(),
                    phaseBefore: phaseBeforeEnter,
                    phaseAfter: phaseSystem.CurrentPhase.ToString(),
                    accepted: true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MainGameLoop] FlowTrace EnterRoom: " + ex.Message);
            }

            // 房间金币等：先按 EventLog 开演，再静默对齐 HUD，避免二次 Sync 抢戏跳变。
            InBattleManagerSingleton.PresentGoldGainsFromEventLog(goldEventStart);
            PlayerInfoHudPresenter.TryGetInstance()?.SyncFromCore(animate: false);

            var pending = arch.GetModel<PendingChoiceModel>();
            if (pending.Kind.Value == PendingChoiceKind.Reward
                && pending.RewardOptions != null
                && pending.RewardOptions.Count > 0)
            {
                try
                {
                    var optionIds = new List<string>(pending.RewardOptions.Count);
                    for (var i = 0; i < pending.RewardOptions.Count; i++)
                    {
                        optionIds.Add(pending.RewardOptions[i].DefId ?? string.Empty);
                    }

                    FlowTraceRecorder.Record(
                        FlowTraceCategory.CoreGate,
                        FlowTraceNames.RewardPresented,
                        new Dictionary<string, string>
                        {
                            { "optionCount", pending.RewardOptions.Count.ToString() },
                            { "options", string.Join(",", optionIds) },
                            { "nodeIndex", _nodeIndex.ToString() },
                            { "source", "roomEvent" },
                            { "room", selectedRoom.ToString() },
                        },
                        loopState: _state.ToString(),
                        phaseBefore: phaseSystem.CurrentPhase.ToString());
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[MainGameLoop] FlowTrace RoomRewardPresented: " + ex.Message);
                }

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
            PlayerInfoHudPresenter.TryGetInstance()?.SyncFromCore(animate: false);
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
            try
            {
                FlowTraceRecorder.Record(
                    FlowTraceCategory.Loop,
                    victory ? FlowTraceNames.Victory : FlowTraceNames.Defeat,
                    new Dictionary<string, string>
                    {
                        { "message", message },
                        { "nodeIndex", _nodeIndex.ToString() },
                    },
                    loopState: _state.ToString());
                // 胜负当场落盘，避免未点「再开始」就退出 Play 时只靠退出钩子、或重开粘连。
                BattleTraceRecorder.ExportBothNow(silentIfEmpty: true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MainGameLoop] FlowTrace Victory/Defeat: " + ex.Message);
            }

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
            try
            {
                FlowTraceRecorder.BeginSessionIfNeeded();
                FlowTraceRecorder.Record(
                    FlowTraceCategory.Loop,
                    FlowTraceNames.EnterMainMenu,
                    loopState: LoopState.MainMenu.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MainGameLoop] FlowTrace EnterMainMenu: " + ex.Message);
            }

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
            var from = _state;
            _state = next;
            if (from == next)
            {
                return;
            }

            try
            {
                FlowTraceRecorder.Record(
                    FlowTraceCategory.Loop,
                    FlowTraceNames.SetState,
                    new Dictionary<string, string>
                    {
                        { "from", from.ToString() },
                        { "to", next.ToString() },
                        { "nodeIndex", _nodeIndex.ToString() },
                    },
                    loopState: next.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MainGameLoop] FlowTrace SetState: " + ex.Message);
            }
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
