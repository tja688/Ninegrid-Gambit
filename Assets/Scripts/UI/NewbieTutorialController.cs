using System;
using System.Collections;
using NineGrid.Battle;
using NineGrid.Battle.Combat;
using NineGrid.GameFlow;
using NineGrid.Presentation.Visuals;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NineGrid.UI
{
    /// <summary>
    /// 序章首次自然开局（非主菜单 StartNewRun）的新手教程：在指定时机显示指向箭头 + 通知文案，
    /// 并拦截下一次点击作为确认。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-500)]
    public sealed class NewbieTutorialController : MonoBehaviour
    {
        const int StepCount = 6;

        static NewbieTutorialController sInstance;

        static readonly TutorialBeat[] Beats =
        {
            new(
                new Vector3(0.15625f, 3.03125f, 0f),
                NoticeChannel.Notice,
                "钻头是你的核心武器，点击玩家舰船开始钻头锻造"),
            new(
                new Vector3(-6.39935684f, -2.50688601f, 0f),
                NoticeChannel.Notice,
                "抓取矿石放置于锻造台上，巧妙组合矿石和锻造台来获取最大化的伤害，完成后点击锻造按钮完成锻造"),
            new(
                new Vector3(-5.53402567f, 1.06432343f, 0f),
                NoticeChannel.Notice,
                "点击敌人，扔出船锚钩住它，来次猛烈的撞击吧"),
            new(
                new Vector3(3.49014521f, -1.600348f, 0f),
                NoticeChannel.Notice,
                "用你最快的速度点击绞盘！点的越快撞得越猛！"),
            new(
                new Vector3(-5.36588907f, 1.11132264f, 0f),
                NoticeChannel.Notice,
                "当一个锻造台摆了至少两个矿石，则该台对应矿石数值会变2倍，是聚力一击还是分散攻击，要根据敌人和持有矿石的特性谨慎抉择"),
            new(
                new Vector3(-4.40975046f, 0.0141148567f, 0f),
                NoticeChannel.Notice,
                "点击精炼厂或者船坞，购买矿石或者升级船体，右上角按钮离开"),
        };

        bool _sessionEligible;
        int _nextStep;
        int _forgeEnterCount;
        bool _showing;
        bool _awaitingDismiss;
        Coroutine _showRoutine;
        SceneElementPointerSelector[] _pointerSelectors;
        Vector3 _arrowHomeLocalPosition;
        Transform _arrowTransform;
        bool _arrowHomeCached;

        public static bool IsAwaitingDismiss => sInstance != null && sInstance._awaitingDismiss;

        public static void SetSessionEligible(bool eligible)
        {
            PendingSessionEligible = eligible;

            if (sInstance != null)
            {
                sInstance.ApplySessionEligible(eligible);
            }
        }

        public static bool PendingSessionEligible { get; private set; }

        /// <summary>
        /// 由 <see cref="FlowInput.TryGameplayClick"/> 调用：若正在等待确认，则消耗本次点击。
        /// </summary>
        public static bool TryConsumeDismissClick()
        {
            if (sInstance == null || !sInstance._awaitingDismiss)
            {
                return false;
            }

            if (!FlowInput.RawPrimaryClickThisFrame())
            {
                return false;
            }

            sInstance._awaitingDismiss = false;
            return true;
        }

        void Awake()
        {
            sInstance = this;
            CacheArrowHome();
        }

        void OnDestroy()
        {
            UnhookAll();
            ClearPointerBlock();
            if (sInstance == this)
            {
                sInstance = null;
            }
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            HookAll();
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnhookAll();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _pointerSelectors = null;
            HookAll();
        }

        void ApplySessionEligible(bool eligible)
        {
            if (eligible)
            {
                _sessionEligible = true;
                _nextStep = 0;
                _forgeEnterCount = 0;
                HookAll();
                TryCatchUpMissedSteps();
                Debug.Log($"[NewbieTutorial] 会话已启用，nextStep={_nextStep}");
                return;
            }

            if (_sessionEligible)
            {
                CancelShowing();
                UnhookAll();
            }

            _sessionEligible = false;
            _nextStep = StepCount;
        }

        void Update()
        {
            if (_awaitingDismiss)
            {
                TryConsumeDismissClick();
            }
        }

        void Start()
        {
            StartCoroutine(DeferredHookRoutine());
        }

        IEnumerator DeferredHookRoutine()
        {
            yield return null;
            if (!_sessionEligible)
            {
                yield break;
            }

            HookAll();
            TryCatchUpMissedSteps();
        }

        void TryCatchUpMissedSteps()
        {
            if (!CanRunTutorial() || _showing)
            {
                return;
            }

            if (_nextStep == 0 && IsPrologueContext())
            {
                var battle = BattleController.Instance
                    ?? FindFirstObjectByType<BattleController>(FindObjectsInactive.Include);
                if (battle != null && battle.IsActive)
                {
                    RequestShowStep(0);
                }
            }
        }

        /// <summary>供 BattleController 在 EnterCompleted 时直接触发，避免订阅时序问题。</summary>
        public static void NotifyBattleEnterCompleted()
        {
            sInstance?.OnBattleEnterCompleted();
        }

        void HookAll()
        {
            if (!_sessionEligible || _nextStep >= StepCount)
            {
                return;
            }

            var battle = BattleController.Instance ?? FindFirstObjectByType<BattleController>(FindObjectsInactive.Include);
            if (battle != null)
            {
                battle.EnterCompleted -= OnBattleEnterCompleted;
                battle.EnterCompleted += OnBattleEnterCompleted;
            }

            var hydraulic = HydraulicSceneController.Instance
                ?? FindFirstObjectByType<HydraulicSceneController>(FindObjectsInactive.Include);
            if (hydraulic != null)
            {
                hydraulic.EnterCompleted -= OnForgeEnterCompleted;
                hydraulic.EnterCompleted += OnForgeEnterCompleted;
                hydraulic.HydraulicCompleted -= OnForgeHydraulicCompleted;
                hydraulic.HydraulicCompleted += OnForgeHydraulicCompleted;
            }

            var board = hydraulic != null ? hydraulic.MaterialBoard : null;
            if (board == null)
            {
                board = FindFirstObjectByType<HydraulicMaterialBoard>(FindObjectsInactive.Include);
            }

            if (board != null)
            {
                board.PlacedOnAnvil -= OnMaterialPlacedOnAnvil;
                board.PlacedOnAnvil += OnMaterialPlacedOnAnvil;
            }

            var winch = FindFirstObjectByType<WinchCrankController>(FindObjectsInactive.Include);
            if (winch != null)
            {
                winch.CrankBegun -= OnWinchCrankBegun;
                winch.CrankBegun += OnWinchCrankBegun;
            }

            var island = FindFirstObjectByType<IslandController>(FindObjectsInactive.Include);
            if (island != null)
            {
                island.VisitPrepared -= OnIslandVisitPrepared;
                island.VisitPrepared += OnIslandVisitPrepared;
            }
        }

        void UnhookAll()
        {
            var battle = BattleController.Instance;
            if (battle != null)
            {
                battle.EnterCompleted -= OnBattleEnterCompleted;
            }

            var hydraulic = HydraulicSceneController.Instance;
            if (hydraulic != null)
            {
                hydraulic.EnterCompleted -= OnForgeEnterCompleted;
                hydraulic.HydraulicCompleted -= OnForgeHydraulicCompleted;
                var board = hydraulic.MaterialBoard;
                if (board != null)
                {
                    board.PlacedOnAnvil -= OnMaterialPlacedOnAnvil;
                }
            }

            var winch = FindFirstObjectByType<WinchCrankController>(FindObjectsInactive.Include);
            if (winch != null)
            {
                winch.CrankBegun -= OnWinchCrankBegun;
            }

            var island = FindFirstObjectByType<IslandController>(FindObjectsInactive.Include);
            if (island != null)
            {
                island.VisitPrepared -= OnIslandVisitPrepared;
            }
        }

        bool CanRunTutorial()
        {
            if (!_sessionEligible || _nextStep >= StepCount || _showing)
            {
                return false;
            }

            var flow = GameFlowController.Instance;
            return flow != null && flow.IsBooted;
        }

        bool IsPrologueContext()
        {
            var flow = GameFlowController.Instance;
            return flow != null && flow.IsPrologueRun;
        }

        void OnBattleEnterCompleted()
        {
            if (!CanRunTutorial() || _nextStep != 0 || !IsPrologueContext())
            {
                return;
            }

            RequestShowStep(0);
        }

        void OnMaterialPlacedOnAnvil(CardInstance card, int anvilIndex)
        {
            if (!CanRunTutorial() || _nextStep != 1 || _forgeEnterCount < 1)
            {
                return;
            }

            RequestShowStep(1);
        }

        void OnForgeHydraulicCompleted()
        {
            if (!CanRunTutorial() || _nextStep != 2)
            {
                return;
            }

            RequestShowStep(2);
        }

        void OnWinchCrankBegun()
        {
            if (!CanRunTutorial() || _nextStep != 3 || !IsPrologueContext())
            {
                return;
            }

            RequestShowStep(3);
        }

        void OnForgeEnterCompleted()
        {
            _forgeEnterCount++;
            if (!CanRunTutorial() || _nextStep != 4 || _forgeEnterCount != 2)
            {
                return;
            }

            RequestShowStep(4);
        }

        void OnIslandVisitPrepared()
        {
            if (!CanRunTutorial() || _nextStep != 5)
            {
                return;
            }

            var flow = GameFlowController.Instance;
            if (flow == null || flow.CurrentState != GameFlowState.Island1)
            {
                return;
            }

            RequestShowStep(5);
        }

        void RequestShowStep(int stepIndex)
        {
            if (_showRoutine != null)
            {
                StopCoroutine(_showRoutine);
            }

            _showRoutine = StartCoroutine(ShowStepRoutine(stepIndex));
        }

        IEnumerator ShowStepRoutine(int stepIndex)
        {
            if (stepIndex < 0 || stepIndex >= Beats.Length || stepIndex != _nextStep)
            {
                _showRoutine = null;
                yield break;
            }

            _showing = true;
            var beat = Beats[stepIndex];
            var ui = UiSystem.Instance;
            var notice = ui != null ? ui.Notice : null;

            ApplyPointerBlock();
            PositionArrow(beat.WorldPosition);
            ui?.SetContinueArrowActive(true);
            ui?.SetOverlayActive(true);

            var body = beat.Text + "\n\n点击继续…";
            if (notice == null)
            {
                Debug.LogWarning("[NewbieTutorial] NoticeSystem 缺失，无法显示教程。");
            }
            else
            {
                notice.Show(beat.Channel, body, 0f);
            }

            Debug.Log($"[NewbieTutorial] 显示步骤 {stepIndex + 1}/{StepCount}：{beat.Text}");

            _awaitingDismiss = true;
            yield return new WaitUntil(() => !_awaitingDismiss);

            notice?.Hide();
            ui?.SetContinueArrowActive(false);
            RestoreArrowPosition();
            ClearPointerBlock();

            _nextStep = stepIndex + 1;
            _showing = false;
            _showRoutine = null;

            if (_nextStep >= StepCount)
            {
                _sessionEligible = false;
                UnhookAll();
            }
            else
            {
                HookAll();
            }
        }

        void CancelShowing()
        {
            if (_showRoutine != null)
            {
                StopCoroutine(_showRoutine);
                _showRoutine = null;
            }

            _showing = false;
            _awaitingDismiss = false;
            UiSystem.Instance?.Notice?.Hide();
            UiSystem.Instance?.SetContinueArrowActive(false);
            RestoreArrowPosition();
            ClearPointerBlock();
        }

        void CacheArrowHome()
        {
            var ui = UiSystem.Instance;
            if (ui == null || ui.ContinueArrow == null)
            {
                return;
            }

            _arrowTransform = ui.ContinueArrow.transform;
            _arrowHomeLocalPosition = _arrowTransform.localPosition;
            _arrowHomeCached = true;
        }

        void PositionArrow(Vector3 worldPosition)
        {
            var ui = UiSystem.Instance;
            if (ui == null || ui.ContinueArrow == null)
            {
                return;
            }

            if (!_arrowHomeCached)
            {
                CacheArrowHome();
            }

            _arrowTransform = ui.ContinueArrow.transform;
            _arrowTransform.position = worldPosition;
        }

        void RestoreArrowPosition()
        {
            if (!_arrowHomeCached || _arrowTransform == null)
            {
                return;
            }

            _arrowTransform.localPosition = _arrowHomeLocalPosition;
        }

        void ApplyPointerBlock()
        {
            ResolvePointerSelectors();
            for (var i = 0; i < _pointerSelectors.Length; i++)
            {
                var selector = _pointerSelectors[i];
                if (selector != null)
                {
                    selector.ApplyInteractionScope(this);
                }
            }
        }

        void ClearPointerBlock()
        {
            if (_pointerSelectors == null)
            {
                return;
            }

            for (var i = 0; i < _pointerSelectors.Length; i++)
            {
                var selector = _pointerSelectors[i];
                if (selector != null)
                {
                    selector.ClearInteractionScope(this);
                }
            }
        }

        void ResolvePointerSelectors()
        {
            if (_pointerSelectors != null && _pointerSelectors.Length > 0)
            {
                return;
            }

            _pointerSelectors = FindObjectsByType<SceneElementPointerSelector>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        }

        readonly struct TutorialBeat
        {
            public TutorialBeat(Vector3 worldPosition, NoticeChannel channel, string text)
            {
                WorldPosition = worldPosition;
                Channel = channel;
                Text = text;
            }

            public Vector3 WorldPosition { get; }
            public NoticeChannel Channel { get; }
            public string Text { get; }
        }
    }
}
