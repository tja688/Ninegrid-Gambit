using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NineGrid.Flow
{
    /// <summary>
    /// 主游戏流程壳状态机（不接内核）：主菜单 → 局内占位 → 帮助卡选择 → 房间二选一 → 房间事件 → 下一节点。
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
        }

        private const string LeftRoomId = "room_left";
        private const string RightRoomId = "room_right";

        private static MainGameLoopManagerSingleton _instance;

        [Header("Refs")]
        [Tooltip("面板路由；留空则运行时在同物体上 GetComponent / AddComponent。")]
        [SerializeField] private UiPanelRouter panelRouter;

        [Tooltip("选择器管理；留空则运行时取 SelectorManagerSingleton.Instance。")]
        [SerializeField] private SelectorManagerSingleton selectorManager;

        [Tooltip("主菜单「开始」按钮；留空则运行时查找 MainPanel/StartRun。")]
        [SerializeField] private Collider2D startRunHit;

        [Tooltip("主菜单「退出」按钮；留空则运行时查找 MainPanel/QuitGame。")]
        [SerializeField] private Collider2D quitGameHit;

        [Tooltip("Notice Text；留空则运行时查找 TableNine Text Overlay UI/Notice Text。")]
        [SerializeField] private TextMeshProUGUI noticeText;

        [Tooltip("点选相机；留空则运行时取 Camera.main。")]
        [SerializeField] private Camera worldCamera;

        [Header("Timing")]
        [Tooltip("局内占位等待时长（秒）；到点即视为对战结束。")]
        [SerializeField] private float battleStubSeconds = 1f;

        [Tooltip("房间事件占位展示时长（秒）。")]
        [SerializeField] private float roomEventStubSeconds = 0.6f;

        [Tooltip("胜利 Notice 展示时长（秒）后回主菜单。")]
        [SerializeField] private float victoryNoticeSeconds = 1.6f;

        [Tooltip("胜利文案。")]
        [SerializeField] private string victoryMessage = "胜利";

        private LoopState _state = LoopState.MainMenu;
        private bool _isBusy;
        private bool _testMode;
        private bool _winAfterNextBattle;
        private int _nodeIndex;
        private CancellationTokenSource _loopCts;

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
            EnterMainMenuImmediate();
        }

        private void OnDestroy()
        {
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
            CancelLoopWork();
            _loopCts = new CancellationTokenSource();

            _testMode = testMode;
            _winAfterNextBattle = false;
            _nodeIndex = 0;
            HideNotice();
            // 进循环后立刻进入局内对战占位；此处先出壳即可。
            panelRouter.ShowInRunShell(inBattle: true);
            RunNodeCycleAsync(_loopCts.Token).Forget();
        }

        /// <summary>
        /// 强制停循环并回主菜单。
        /// </summary>
        public void ReturnToMainMenu()
        {
            CancelLoopWork();
            if (selectorManager != null && selectorManager.IsChoiceActive)
            {
                selectorManager.HideChoice();
            }

            EnterMainMenuImmediate();
        }

        private async UniTaskVoid RunNodeCycleAsync(CancellationToken ct)
        {
            _isBusy = true;
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    _nodeIndex++;
                    await PlayBattleStubAsync(ct);
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    if (_testMode && _winAfterNextBattle)
                    {
                        await ShowVictoryAndReturnAsync(ct);
                        return;
                    }

                    await PlayRewardChoiceAsync(ct);
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    var roomIndex = await PlayRoomChoiceAsync(ct);
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    if (_testMode)
                    {
                        // 左：无限循环；右：下一轮对战结束后宣告胜利
                        _winAfterNextBattle = roomIndex == 1;
                    }

                    await PlayRoomEventAsync(ct);
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    // 继续下一节点
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

        private async UniTask PlayBattleStubAsync(CancellationToken ct)
        {
            SetState(LoopState.BattleStub);
            EnsureBindings();
            panelRouter.ShowInRunShell(inBattle: true);
            Debug.Log($"[MainGameLoop] 节点 {_nodeIndex} 局内占位 {battleStubSeconds:0.##}s");
            await UniTask.Delay(
                TimeSpan.FromSeconds(Mathf.Max(0.05f, battleStubSeconds)),
                cancellationToken: ct);
        }

        private async UniTask PlayRewardChoiceAsync(CancellationToken ct)
        {
            SetState(LoopState.RewardChoice);
            EnsureBindings();
            panelRouter.ShowRewardOverlay();

            var picked = false;
            var finished = false;
            int pickIndex = -1;
            string pickId = null;

            selectorManager.BeginBounceChoice(
                3,
                (index, defId) =>
                {
                    picked = true;
                    pickIndex = index;
                    pickId = defId;
                    Debug.Log($"[MainGameLoop] 奖励已选 index={index} defId={defId}");
                },
                () => { finished = true; });

            await UniTask.WaitUntil(() => finished || ct.IsCancellationRequested, cancellationToken: ct);
            if (ct.IsCancellationRequested)
            {
                return;
            }

            if (!picked)
            {
                Debug.LogWarning("[MainGameLoop] 奖励选择未产生结果，继续流程。");
            }
            else
            {
                Debug.Log($"[MainGameLoop] 奖励会话结束 pick={pickIndex}:{pickId}");
            }

            panelRouter.HideAllOverlays();
        }

        private async UniTask<int> PlayRoomChoiceAsync(CancellationToken ct)
        {
            SetState(LoopState.RoomChoice);
            EnsureBindings();
            panelRouter.ShowRoomChoiceOverlay();

            var pickedIndex = -1;
            var finished = false;

            selectorManager.BeginRoomChoice(
                LeftRoomId,
                RightRoomId,
                (index, optionId) =>
                {
                    pickedIndex = index;
                    Debug.Log($"[MainGameLoop] 房间已选 index={index} id={optionId}");
                },
                () => { finished = true; });

            await UniTask.WaitUntil(() => finished || ct.IsCancellationRequested, cancellationToken: ct);
            panelRouter.HideAllOverlays();

            if (pickedIndex < 0)
            {
                pickedIndex = 0;
            }

            return pickedIndex;
        }

        private async UniTask PlayRoomEventAsync(CancellationToken ct)
        {
            SetState(LoopState.RoomEvent);
            EnsureBindings();
            panelRouter.ShowRoomEventOverlay();
            Debug.Log($"[MainGameLoop] 房间事件占位 {roomEventStubSeconds:0.##}s");
            await UniTask.Delay(
                TimeSpan.FromSeconds(Mathf.Max(0.05f, roomEventStubSeconds)),
                cancellationToken: ct);
            panelRouter.HideAllOverlays();
        }

        private async UniTask ShowVictoryAndReturnAsync(CancellationToken ct)
        {
            SetState(LoopState.VictoryNotice);
            EnsureBindings();
            panelRouter.ShowInRunShell(inBattle: false);
            ShowNotice(string.IsNullOrWhiteSpace(victoryMessage) ? "胜利" : victoryMessage);
            Debug.Log("[MainGameLoop] 测试胜利，准备回主菜单。");
            await UniTask.Delay(
                TimeSpan.FromSeconds(Mathf.Max(0.2f, victoryNoticeSeconds)),
                cancellationToken: ct);
            EnterMainMenuImmediate();
        }

        private void EnterMainMenuImmediate()
        {
            EnsureBindings();
            HideNotice();
            panelRouter.ShowMainMenu();
            SetState(LoopState.MainMenu);
            _winAfterNextBattle = false;
            _testMode = false;
            _nodeIndex = 0;
            _isBusy = false;
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
