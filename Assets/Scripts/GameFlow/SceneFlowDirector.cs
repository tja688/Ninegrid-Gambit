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
            if (!mEnabled)
            {
                return;
            }

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

            EnsureSubscribed();
            // 处理开局状态（GameFlow 可能已在自己的 Start 里 Boot 并发过一次事件）。
            SyncToCurrentState();
        }

        void OnDestroy()
        {
            Unsubscribe();
            UnbindBattle();
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

            flow.StateChanged += OnStateChanged;
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (GameFlowController.Instance != null)
            {
                GameFlowController.Instance.StateChanged -= OnStateChanged;
            }

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
            RouteTo(next);
        }

        void RouteTo(GameFlowState state)
        {
            _handledState = state;

            UnbindBattle();

            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            _routine = StartCoroutine(TransitionRoutine(state));
        }

        IEnumerator TransitionRoutine(GameFlowState state)
        {
            // 事件状态：不加载场景，由 EventController 处理
            if (state >= GameFlowState.Event1 && state <= GameFlowState.Event6)
            {
                // 短暂黑场过渡
                yield return Fade(0f, 1f);
                yield return new WaitForSecondsRealtime(0.3f);
                EventController.Instance?.ShowEvent(state);
                yield return Fade(1f, 0f);
                _routine = null;
                yield break;
            }

            // 胜利结算：BOSS改造三选一 → 传说事件 → 回主菜单
            if (state == GameFlowState.VictorySettlement)
            {
                yield return Fade(0f, 1f);
                yield return new WaitForSecondsRealtime(0.3f);

                // BOSS改造选择
                EventController.Instance?.ShowBossRelicSelection();

                // 等待 EventController 完成（它会调用 Advance）
                // 但 VictorySettlement 是最终节点，Advance 会回主菜单
                // EventController 完成后会 Advance，此时状态变为 MainMenu
                yield return new WaitUntil(() =>
                    GameFlowController.Instance == null
                    || GameFlowController.Instance.CurrentState != GameFlowState.VictorySettlement
                    || (EventController.Instance != null && !EventController.Instance.IsPanelActive));

                // 如果还没切换状态，显示通关提示
                if (GameFlowController.Instance != null
                    && GameFlowController.Instance.CurrentState == GameFlowState.VictorySettlement)
                {
                    var ui = UiSystem.Instance;
                    ui?.Notice?.Show(NoticeChannel.Notice, "航线终末——你活着回来了。", victoryHold);
                    yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, victoryHold));
                    GameFlowController.Instance?.Advance();
                }

                yield return Fade(1f, 0f);
                _routine = null;
                yield break;
            }

            var targetScene = GameFlowScenes.GetSceneName(state);
            var activeScene = SceneManager.GetActiveScene().name;
            var needLoad = !string.Equals(targetScene, activeScene, StringComparison.Ordinal);

            if (needLoad)
            {
                yield return Fade(0f, 1f);

                var op = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Single);
                if (op != null)
                {
                    while (!op.isDone)
                    {
                        yield return null;
                    }
                }

                // 等 Awake/Start 落定，场景内控制器完成自解析。
                yield return null;
                yield return null;
            }

            // 触发节点进入动作（序章演出 / 战斗入场；岛屿/路线/主菜单为 no-op，由场景控制器自处理）。
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

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
