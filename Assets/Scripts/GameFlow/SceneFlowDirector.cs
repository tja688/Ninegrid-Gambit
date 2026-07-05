using System;
using System.Collections;
using NineGrid.UI;
using QFramework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 场景编导：把 <see cref="GameFlowController"/> 的状态切换翻译成真正的场景加载与过渡蒙版。
    ///
    /// 职责：
    /// - 监听 GameFlow 状态变化，按 <see cref="GameFlowScenes.GetSceneName"/> 加载对应场景（带黑场淡入淡出）。
    /// - 场景就绪后调用 <see cref="GameFlowController.RunNodeEntry(GameFlowState)"/> 触发序章演出 / 战斗入场。
    /// - 战斗结束（<see cref="BattleController.BattleFinished"/>）后自动推进到下一节点。
    /// - Event* / VictorySettlement 作为「过场节点」不驻留：Event 立即推进到下一战斗；Victory 弹一条通关提示后回主菜单。
    ///
    /// 自举：无需在任何场景手动挂载——通过 RuntimeInitialize 自动创建常驻实例；若场景里已放了一个也会复用。
    /// </summary>
    public sealed class SceneFlowDirector : PersistentMonoSingleton<SceneFlowDirector>
    {
        [Header("过渡蒙版")]
        [SerializeField] float fadeDuration = 0.35f;
        [SerializeField] Color fadeColor = Color.black;

        [Header("过场节点停留")]
        [Tooltip("VictorySettlement 通关提示停留秒数，之后自动回主菜单。")]
        [SerializeField] float victoryHold = 2.5f;

        Canvas _overlayCanvas;
        Image _fadeImage;
        Coroutine _routine;
        BattleController _boundBattle;
        Action<bool> _battleFinishedHandler;

        GameFlowState? _handledState;
        bool _subscribed;
        bool _bootstrapComplete;
        GameFlowController _boundFlow;
        static SceneFlowDirector s_active;

        /// <summary>确保 GameFlow 在任何场景 Awake 之前就转为「延迟节点入口」，交由本编导驱动。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void EnableDeferredEntry()
        {
            GameFlowController.DeferNodeEntry = true;
        }

        /// <summary>首个场景加载后自举一个常驻编导（若场景没放）。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            // 触发 Instance 访问：已有则复用，没有则自动建一个常驻对象。
            _ = Instance;
        }

        protected override void Awake()
        {
            base.Awake();
            // Instance getter 可能在 Awake 前写入 mInstance，导致 base 未置 mEnabled。
            if (mInstance == this)
            {
                mEnabled = true;
            }

            if (!mEnabled)
            {
                return;
            }

            s_active = this;
            gameObject.name = "[SceneFlowDirector]";
            GameFlowController.DeferNodeEntry = true;
            BuildOverlay();
        }

        void Start()
        {
            if (!mEnabled)
            {
                return;
            }

            StartCoroutine(BootstrapRoutine());
        }

        void Update()
        {
            if (!mEnabled)
            {
                return;
            }

            if (!_subscribed)
            {
                EnsureSubscribed();
                if (_subscribed)
                {
                    SyncToCurrentState();
                }
            }

            if (_bootstrapComplete)
            {
                TryReconcileScene();
            }
        }

        void OnDestroy()
        {
            if (s_active == this)
            {
                s_active = null;
            }

            Unsubscribe();
            UnbindBattle();
        }

        /// <summary>由 <see cref="GameFlowController"/> 在 FSM 切换时直接调用，不依赖 event 订阅时序。</summary>
        internal static void HandleStateChanged(GameFlowState state)
        {
            EnsureActiveDirector();
            if (s_active == null)
            {
                Debug.LogError("[SceneFlow] SceneFlowDirector 未就绪，无法切换场景。");
                return;
            }

            s_active.RouteTo(state);
        }

        static void EnsureActiveDirector()
        {
            if (s_active != null)
            {
                return;
            }

            _ = Instance;
        }

        IEnumerator BootstrapRoutine()
        {
            while (GameFlowController.Instance == null)
            {
                yield return null;
            }

            EnsureSubscribed();
            SyncToCurrentState();
            _bootstrapComplete = true;
        }

        void EnsureSubscribed()
        {
            if (_subscribed)
            {
                return;
            }

            var flow = GameFlowController.Instance;
            if (flow == null)
            {
                return;
            }

            if (_boundFlow != null && _boundFlow != flow)
            {
                _boundFlow.StateChanged -= OnStateChanged;
                _subscribed = false;
            }

            if (_subscribed)
            {
                return;
            }

            _boundFlow = flow;
            _boundFlow.StateChanged += OnStateChanged;
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (_boundFlow != null)
            {
                _boundFlow.StateChanged -= OnStateChanged;
            }

            _boundFlow = null;
            _subscribed = false;
        }

        void SyncToCurrentState()
        {
            var flow = GameFlowController.Instance;
            if (flow == null)
            {
                return;
            }

            // GameFlow 尚未 Boot 时，CurrentState 只是占位默认值；等它 Start 里 Boot 后会补发事件，交给 OnStateChanged。
            if (!flow.IsBooted)
            {
                return;
            }

            var state = flow.CurrentState;
            if (_handledState == state)
            {
                return;
            }

            RouteTo(state);
        }

        void OnStateChanged(GameFlowState previous, GameFlowState next)
        {
            // 场景切换由 GameFlowController FSM 回调 HandleStateChanged 驱动，避免重复 RouteTo。
        }

        void RouteTo(GameFlowState state)
        {
            _handledState = state;

            Debug.Log(
                $"[SceneFlow] RouteTo {state} active={SceneManager.GetActiveScene().name}");

            UnbindBattle();

            if (_routine != null)
            {
                var host = ResolveCoroutineHost();
                host.StopCoroutine(_routine);
                _routine = null;
                EnsureOverlayHidden();
            }

            _routine = ResolveCoroutineHost().StartCoroutine(TransitionRoutine(state));
        }

        MonoBehaviour ResolveCoroutineHost()
        {
            var flow = GameFlowController.Instance;
            if (flow != null && flow.isActiveAndEnabled)
            {
                return flow;
            }

            return this;
        }

        void TryReconcileScene()
        {
            if (_routine != null)
            {
                return;
            }

            var flow = GameFlowController.Instance;
            if (flow == null || !flow.IsBooted)
            {
                return;
            }

            var state = flow.CurrentState;
            if (state == GameFlowState.MainMenu || state == GameFlowState.VictorySettlement)
            {
                return;
            }

            var expected = ResolveExpectedSceneName(state);
            if (string.IsNullOrEmpty(expected))
            {
                return;
            }

            var active = SceneManager.GetActiveScene().name;
            if (string.Equals(expected, active, StringComparison.Ordinal))
            {
                return;
            }

            Debug.LogWarning(
                $"[SceneFlow] 场景与状态不一致 state={state} active={active} expected={expected}，重新过渡。");
            RouteTo(state);
        }

        static string ResolveExpectedSceneName(GameFlowState state)
        {
            if (GameFlowScenes.IsEventState(state))
            {
                return GameFlowScenes.Route;
            }

            if (GameFlowScenes.IsBattleState(state))
            {
                return GameFlowScenes.Main;
            }

            return GameFlowScenes.GetSceneName(state);
        }

        IEnumerator TransitionRoutine(GameFlowState state)
        {
            // 事件状态：复用 RouteScene（RouteController 已在 Route* 展示事件；Event* 可能仅作跳板）
            if (GameFlowScenes.IsEventState(state))
            {
                yield return EnsureRouteSceneLoaded();
                _routine = null;
                yield break;
            }

            // 胜利结算：旗舰改造（简化为自动领取）→ 通关提示 → 主菜单
            if (state == GameFlowState.VictorySettlement)
            {
                yield return Fade(0f, 1f);
                yield return new WaitForSecondsRealtime(0.3f);

                var relicMsg = EventController.ApplyBossRelicChoice(0);
                var ui = UiSystem.Instance;
                ui?.Notice?.Show(NoticeChannel.Notice, relicMsg, victoryHold);
                yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, victoryHold));

                ui?.Notice?.Show(NoticeChannel.Notice, "航线终末——你活着回来了。", victoryHold);
                yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, victoryHold));
                GameFlowController.Instance?.Advance();

                yield return Fade(1f, 0f);
                _routine = null;
                yield break;
            }

            var targetScene = GameFlowScenes.GetSceneName(state);
            var activeScene = SceneManager.GetActiveScene().name;
            var needLoad = !string.Equals(targetScene, activeScene, StringComparison.Ordinal);

            // 战斗态：必须切到 MainScene，BGM 变了但场景没换时这里兜底。
            if (GameFlowScenes.IsBattleState(state))
            {
                needLoad = !string.Equals(GameFlowScenes.Main, activeScene, StringComparison.Ordinal);
                targetScene = GameFlowScenes.Main;
            }

            if (needLoad)
            {
                Debug.Log($"[SceneFlow] {activeScene} → {targetScene} (state={state})");
                yield return Fade(0f, 1f);
                yield return LoadSceneSingle(targetScene);
                yield return null;
                yield return null;
            }

            var flow = GameFlowController.Instance;
            flow?.RunNodeEntry(state);

            if (GameFlowScenes.IsBattleState(state))
            {
                BindBattleFinish();
            }

            if (needLoad)
            {
                yield return Fade(1f, 0f);
            }

            _routine = null;
        }

        IEnumerator EnsureRouteSceneLoaded()
        {
            var routeScene = GameFlowScenes.Route;
            var currentScene = SceneManager.GetActiveScene().name;
            if (string.Equals(routeScene, currentScene, StringComparison.Ordinal))
            {
                yield break;
            }

            yield return Fade(0f, 1f);
            yield return LoadSceneSingle(routeScene);
            yield return null;
            yield return null;
            yield return Fade(1f, 0f);
        }

        IEnumerator LoadSceneSingle(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (op != null)
            {
                while (!op.isDone)
                {
                    yield return null;
                }

                yield break;
            }

            Debug.LogWarning($"[SceneFlow] LoadSceneAsync 失败，同步加载 {sceneName}");
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        IEnumerator RunPassThrough(GameFlowState state)
        {
            if (state == GameFlowState.VictorySettlement)
            {
                var ui = UiSystem.Instance;
                ui?.Notice?.Show(NoticeChannel.Notice, "航线终末——你活着回来了。", victoryHold);
                yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, victoryHold));
            }
            else
            {
                // Event*：占位过场，直接推进到下一战斗。
                yield return null;
            }

            GameFlowController.Instance?.Advance();
        }

        static bool IsPassThrough(GameFlowState state)
        {
            // Event1-6 和 VictorySettlement 不再是空过场：由 EventController 处理真实事件。
            return false;
        }

        void BindBattleFinish()
        {
            UnbindBattle();

            var battle = BattleController.Instance;
            if (battle == null)
            {
                battle = FindFirstObjectByType<BattleController>();
            }

            if (battle == null)
            {
                Debug.LogWarning("[SceneFlow] 战斗场景没有 BattleController，无法在战斗结束后自动推进。");
                return;
            }

            _boundBattle = battle;
            _battleFinishedHandler = OnBattleFinished;
            battle.BattleFinished += _battleFinishedHandler;
        }

        void UnbindBattle()
        {
            if (_boundBattle != null && _battleFinishedHandler != null)
            {
                _boundBattle.BattleFinished -= _battleFinishedHandler;
            }

            _boundBattle = null;
            _battleFinishedHandler = null;
        }

        void OnBattleFinished(bool won)
        {
            UnbindBattle();
            if (won)
            {
                // 胜利：推进到下一节点（岛屿/事件/下一场战斗）
                GameFlowController.Instance?.Advance();
            }
            else
            {
                // 失败：回主菜单，清除存档
                Debug.Log("[SceneFlow] 战斗失败，回主菜单");
                GameFlowController.Instance?.NotifyPlayerDefeated();
            }
        }

        void BuildOverlay()
        {
            if (_overlayCanvas != null)
            {
                return;
            }

            var canvasGo = new GameObject("[SceneTransition]");
            canvasGo.transform.SetParent(transform, false);

            _overlayCanvas = canvasGo.AddComponent<Canvas>();
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.sortingOrder = 32760;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasGo.AddComponent<GraphicRaycaster>();

            var imageGo = new GameObject("Fade");
            imageGo.transform.SetParent(canvasGo.transform, false);
            _fadeImage = imageGo.AddComponent<Image>();
            _fadeImage.color = WithAlpha(fadeColor, 0f);
            _fadeImage.raycastTarget = true;

            var rect = _fadeImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            SetOverlayActive(false);
        }

        IEnumerator Fade(float from, float to)
        {
            if (_fadeImage == null)
            {
                BuildOverlay();
            }

            if (_fadeImage == null)
            {
                yield break;
            }

            SetOverlayActive(true);

            if (fadeDuration <= 0f)
            {
                _fadeImage.color = WithAlpha(fadeColor, to);
            }
            else
            {
                var elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    var t = Mathf.Clamp01(elapsed / fadeDuration);
                    _fadeImage.color = WithAlpha(fadeColor, Mathf.Lerp(from, to, t));
                    yield return null;
                }

                _fadeImage.color = WithAlpha(fadeColor, to);
            }

            // 完全透明时关闭蒙版，避免挡住点击。
            if (to <= 0.001f)
            {
                SetOverlayActive(false);
            }
        }

        void SetOverlayActive(bool active)
        {
            if (_overlayCanvas != null)
            {
                _overlayCanvas.gameObject.SetActive(active);
            }
        }

        void EnsureOverlayHidden()
        {
            if (_fadeImage != null)
            {
                _fadeImage.color = WithAlpha(fadeColor, 0f);
            }

            SetOverlayActive(false);
        }

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
