using System;
using System.Collections;
using DG.Tweening;
using NineGrid.Battle;
using UnityEngine;

namespace NineGrid.GameFlow
{
    public enum HydraulicSceneState
    {
        Hidden = 0,
        Entering = 1,
        Active = 2,
        Exiting = 3,
        Hydraulic = 4,
    }

    /// <summary>
    /// 液压子场景独立状态机：入场 / 常规退场 / 液压完成退场。
    /// 战斗中由 <see cref="BattleController.TryEnterForgeMode"/> 拉起；亦保留小键盘测试入口。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HydraulicSceneController : MonoBehaviour
    {
        public static HydraulicSceneController Instance { get; private set; }

        [Header("Root")]
        [SerializeField] Transform sceneRoot;

        [Header("BG")]
        [SerializeField] Transform bg;
        [SerializeField] Transform bgInOut;

        [Header("Display")]
        [SerializeField] Transform display;
        [SerializeField] Transform displayInOut;

        [Header("Pipe")]
        [SerializeField] Transform pipe;
        [SerializeField] Transform pipeInOut;

        [Header("Hammer")]
        [SerializeField] Transform hammer;
        [SerializeField] Transform hammerPressPoint;

        [Header("Other Visuals")]
        [SerializeField] Transform[] otherVisuals;

        [Header("Materials")]
        [SerializeField] HydraulicMaterialLane materialLane;
        [SerializeField] HydraulicMaterialBoard materialBoard;

        [Header("Hammer Impact")]
        [Tooltip("锤头下压落点瞬间触发的屏幕震动（留空则用默认参数静态触发）。")]
        [SerializeField] ScreenShakeEffect hammerImpactShake;

        [Header("Enter / Exit Timing")]
        [SerializeField] float moveDuration = 0.45f;
        [SerializeField] float enterStagger = 0.12f;
        [SerializeField] float exitStagger = 0.12f;
        [SerializeField] Ease enterEase = Ease.OutCubic;
        [SerializeField] Ease exitEase = Ease.InCubic;

        [Header("Hammer Timing")]
        [SerializeField] float hammerDropDuration = 0.22f;
        [SerializeField] float hammerHoldDuration = 0.5f;
        [SerializeField] float hammerRiseDuration = 0.32f;
        [SerializeField] Ease hammerDropEase = Ease.InExpo;
        [SerializeField] Ease hammerRiseEase = Ease.InCubic;

        [Header("Debug Input")]
        [Tooltip("宿主失活时 Update 不会跑；战斗侧 BattleController 也会监听小键盘1/2。")]
        [SerializeField] bool enableDebugHotkeys = true;
        [SerializeField] KeyCode toggleEnterExitKey = KeyCode.Keypad1;
        [SerializeField] KeyCode hydraulicKey = KeyCode.Keypad2;

        Vector3 _bgStay;
        Vector3 _displayStay;
        Vector3 _pipeStay;
        Vector3 _hammerStay;
        Collider2D _bgRayBlocker;
        Coroutine _routine;
        Tween _activeTween;
        HydraulicSceneState _state = HydraulicSceneState.Hidden;

        public HydraulicSceneState State => _state;
        public bool IsBusy =>
            _state == HydraulicSceneState.Entering
            || _state == HydraulicSceneState.Exiting
            || _state == HydraulicSceneState.Hydraulic;

        public bool IsActive => _state == HydraulicSceneState.Active;

        /// <summary>锻造台材料布置（供伤害预览 / 钻头亮起查询）。</summary>
        public HydraulicMaterialBoard MaterialBoard => materialBoard;

        /// <summary>材料滑道（供发牌 / 桌面状态查询）。</summary>
        public HydraulicMaterialLane MaterialLane => materialLane;

        /// <summary>
        /// 铸造提交（锤头开始下压前触发），携带各锻造台是否有材料的占用表（长度 3）。
        /// 供战斗侧据此在外面亮出对应钻头。此刻材料尚未被消耗。
        /// </summary>
        public event Action<bool[]> ForgeCommitted;

        /// <summary>入场协程刚启动（宿主已激活，视觉尚未就位）。</summary>
        public event Action EnterStarted;

        /// <summary>常规入场完成，停留在 stay。</summary>
        public event Action EnterCompleted;

        /// <summary>常规退场完成（玩家切出去看对手）。</summary>
        public event Action ExitCompleted;

        /// <summary>液压流程完成并整场景退场完毕。</summary>
        public event Action HydraulicCompleted;

        public event Action HammerFallStarted;
        public event Action HammerImpacted;
        public event Action HammerHoldStarted;
        public event Action HammerRiseStarted;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[HydraulicScene] 场景中存在多个实例，保留先创建的。");
                return;
            }

            Instance = this;
            ResolveRefs();
            CacheStayPositions();
            EnsureBgRayBlocker();
            ApplyHiddenImmediate();
            // 开局清空一次；此后常规进出保留桌面 / 锻造台状态，仅铸造完成后清空。
            ResetMaterials();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            KillMotion();
        }

        void Update()
        {
            if (!enableDebugHotkeys)
            {
                return;
            }

            // 宿主激活时自听；默认失活时由 BattleController 代听（同一套 DebugHotkeyInput）。
            if (DebugHotkeyInput.WasPressedThisFrame(toggleEnterExitKey))
            {
                if (_state == HydraulicSceneState.Hidden)
                {
                    Enter();
                }
                else if (_state == HydraulicSceneState.Active
                         && (BattleController.Instance == null || !BattleController.Instance.IsActive))
                {
                    Exit();
                }
            }

            if (DebugHotkeyInput.WasPressedThisFrame(hydraulicKey))
            {
                PlayHydraulic();
            }
        }

        /// <summary>沿管道倾倒一份材料到桌面（小键盘5 / 外部调用）。</summary>
        public bool TryDeliverMaterial()
        {
            if (_state != HydraulicSceneState.Active)
            {
                return false;
            }

            ResolveRefs();
            return materialLane != null && materialLane.TryDeliver();
        }

        /// <summary>
        /// 入场：BG → 显示屏 → 管道，轻微错峰。
        /// 宿主 GameObject 可默认失活；外部拉起时会先激活再跑协程，退场结束后再失活。
        /// </summary>
        public bool Enter()
        {
            if (_state != HydraulicSceneState.Hidden)
            {
                return false;
            }

            // 失活对象无法 StartCoroutine；先激活宿主（首次会跑 Awake）。
            EnsureHostActive();

            if (_state != HydraulicSceneState.Hidden)
            {
                return false;
            }

            ResolveRefs();
            CacheStayPositions();
            KillMotion();
            _state = HydraulicSceneState.Entering;
            EnterStarted?.Invoke();
            _routine = StartCoroutine(EnterRoutine());
            return true;
        }

        /// <summary>常规退场：管道 → 显示屏 → BG，out 点即入场点。</summary>
        public bool Exit()
        {
            if (_state != HydraulicSceneState.Active)
            {
                return false;
            }

            if (BattleController.Instance != null && BattleController.Instance.IsActive)
            {
                return false;
            }

            KillMotion();
            _state = HydraulicSceneState.Exiting;
            SetBgRayBlockEnabled(false);
            _routine = StartCoroutine(ExitRoutine(invokeExitCompleted: true));
            return true;
        }

        /// <summary>
        /// 液压完成退场：锤头迅猛下压 → 停留 → 先慢后快抬起。
        /// 锤头落位瞬间其他元素退场；抬起完成后整场景退场完成。
        /// </summary>
        public bool PlayHydraulic()
        {
            if (_state != HydraulicSceneState.Active)
            {
                return false;
            }

            KillMotion();
            _state = HydraulicSceneState.Hydraulic;
            _routine = StartCoroutine(HydraulicRoutine());
            return true;
        }

        IEnumerator EnterRoutine()
        {
            SetRootActive(true);
            SetVisualActive(bg, true);
            SetVisualActive(display, true);
            SetVisualActive(pipe, true);
            SetVisualActive(hammer, true);
            SetOtherVisualsActive(true);
            SetBgRayBlockEnabled(false);

            PlaceAt(bg, bgInOut);
            PlaceAt(display, displayInOut);
            PlaceAt(pipe, pipeInOut);
            PlaceAtWorld(hammer, _hammerStay);

            var sequence = DOTween.Sequence().SetUpdate(true);
            _activeTween = sequence;

            AppendMove(sequence, bg, _bgStay, moveDuration, enterEase, 0f);
            AppendMove(sequence, display, _displayStay, moveDuration, enterEase, enterStagger);
            AppendMove(sequence, pipe, _pipeStay, moveDuration, enterEase, enterStagger * 2f);

            var completed = false;
            sequence.OnComplete(() => completed = true);
            yield return new WaitUntil(() => completed);

            PlaceAtWorld(bg, _bgStay);
            PlaceAtWorld(display, _displayStay);
            PlaceAtWorld(pipe, _pipeStay);
            SetBgRayBlockEnabled(true);
            // 常规入场不清空材料：保留桌面 / 锻造台状态。

            _activeTween = null;
            _routine = null;
            _state = HydraulicSceneState.Active;
            EnterCompleted?.Invoke();
        }

        IEnumerator ExitRoutine(bool invokeExitCompleted)
        {
            SetBgRayBlockEnabled(false);
            SetOtherVisualsActive(false);
            SetVisualActive(hammer, false);

            var sequence = DOTween.Sequence().SetUpdate(true);
            _activeTween = sequence;

            // 退场顺序与入场相反：管道 → 显示屏 → BG
            AppendMove(sequence, pipe, GetInOutPosition(pipeInOut, _pipeStay), moveDuration, exitEase, 0f);
            AppendMove(sequence, display, GetInOutPosition(displayInOut, _displayStay), moveDuration, exitEase, exitStagger);
            AppendMove(sequence, bg, GetInOutPosition(bgInOut, _bgStay), moveDuration, exitEase, exitStagger * 2f);

            var completed = false;
            sequence.OnComplete(() => completed = true);
            yield return new WaitUntil(() => completed);

            ApplyHiddenImmediate();
            _activeTween = null;
            _routine = null;
            _state = HydraulicSceneState.Hidden;

            if (invokeExitCompleted)
            {
                ExitCompleted?.Invoke();
            }

            // 退场完成后失活宿主，保持「默认失活、外部拉起」。
            DeactivateHost();
        }

        IEnumerator HydraulicRoutine()
        {
            // 铸造提交：锤头下压前，先播报各锻造台占用情况（此刻材料尚未消耗）。
            NotifyForgeCommitted();

            if (hammer == null)
            {
                yield return ExitRoutine(invokeExitCompleted: false);
                ResetMaterials();
                HydraulicCompleted?.Invoke();
                yield break;
            }

            PlaceAtWorld(hammer, _hammerStay);
            SetBgRayBlockEnabled(false);

            var dropCompleted = false;
            HammerFallStarted?.Invoke();
            _activeTween = hammer
                .DOMove(GetPressPosition(), hammerDropDuration)
                .SetEase(hammerDropEase)
                .SetUpdate(true)
                .OnComplete(() => dropCompleted = true);

            yield return new WaitUntil(() => dropCompleted);
            _activeTween = null;
            PlaceAtWorld(hammer, GetPressPosition());

            // 锤头落位瞬间：屏幕震动。
            HammerImpacted?.Invoke();
            PlayHammerShake();

            // 锤头完整落位后，其他元素立刻退场（与抬起并行）。
            _otherExitRoutine = StartCoroutine(ExitOthersRoutine());

            HammerHoldStarted?.Invoke();
            if (hammerHoldDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(hammerHoldDuration);
            }

            var riseCompleted = false;
            HammerRiseStarted?.Invoke();
            _activeTween = hammer
                .DOMove(_hammerStay, hammerRiseDuration)
                .SetEase(hammerRiseEase)
                .SetUpdate(true)
                .OnComplete(() => riseCompleted = true);

            yield return new WaitUntil(() => riseCompleted);
            _activeTween = null;
            PlaceAtWorld(hammer, _hammerStay);

            // 锤头抬起完成后，若其他元素仍在退场则等其结束，再宣告整场景退场完成。
            if (_otherExitRoutine != null)
            {
                yield return _otherExitRoutine;
            }

            ApplyHiddenImmediate();
            // 铸造完成：材料被消耗，清空桌面 / 锻造台。
            ResetMaterials();
            _routine = null;
            _state = HydraulicSceneState.Hidden;
            HydraulicCompleted?.Invoke();
            DeactivateHost();
        }

        void NotifyForgeCommitted()
        {
            var occupancy = new bool[BoreManager.BoreCount];
            materialBoard?.GetOccupancy(occupancy);
            ForgeCommitted?.Invoke(occupancy);
        }

        void PlayHammerShake()
        {
            if (hammerImpactShake != null)
            {
                hammerImpactShake.Play();
                return;
            }

            // 未配置专用效果时，用一组稳妥的默认参数触发一次。
            ScreenShakeEffect.Trigger(0.6f, 0.35f, 34f);
        }

        Coroutine _otherExitRoutine;
        Sequence _otherExitSequence;

        IEnumerator ExitOthersRoutine()
        {
            SetOtherVisualsActive(false);

            var sequence = DOTween.Sequence().SetUpdate(true);
            _otherExitSequence = sequence;

            AppendMove(sequence, pipe, GetInOutPosition(pipeInOut, _pipeStay), moveDuration, exitEase, 0f);
            AppendMove(sequence, display, GetInOutPosition(displayInOut, _displayStay), moveDuration, exitEase, exitStagger);
            AppendMove(sequence, bg, GetInOutPosition(bgInOut, _bgStay), moveDuration, exitEase, exitStagger * 2f);

            var completed = false;
            sequence.OnComplete(() => completed = true);
            yield return new WaitUntil(() => completed);

            SetVisualActive(bg, false);
            SetVisualActive(display, false);
            SetVisualActive(pipe, false);
            _otherExitSequence = null;
            _otherExitRoutine = null;
        }

        void ApplyHiddenImmediate()
        {
            KillMotion();
            SetBgRayBlockEnabled(false);

            PlaceAtWorld(bg, _bgStay);
            PlaceAtWorld(display, _displayStay);
            PlaceAtWorld(pipe, _pipeStay);
            PlaceAtWorld(hammer, _hammerStay);

            SetVisualActive(bg, false);
            SetVisualActive(display, false);
            SetVisualActive(pipe, false);
            SetVisualActive(hammer, false);
            SetOtherVisualsActive(false);
            // 不在此处清空材料：常规退场需保留桌面 / 锻造台状态；清空由 Awake / 铸造完成 / ResetForgeState 显式触发。
            // 不在此处失活宿主：Awake / Enter 过程中宿主必须保持激活才能跑协程。
        }

        /// <summary>显式清空桌面与锻造台材料（新战斗开始等场景）。</summary>
        public void ResetForgeState()
        {
            ResetMaterials();
        }

        void ResetMaterials()
        {
            materialBoard?.ResetBoard();
            materialLane?.ResetLane();
        }

        void EnsureHostActive()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        void DeactivateHost()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        void KillMotion()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            if (_otherExitRoutine != null)
            {
                StopCoroutine(_otherExitRoutine);
                _otherExitRoutine = null;
            }

            if (_activeTween != null && _activeTween.IsActive())
            {
                _activeTween.Kill();
            }

            _activeTween = null;

            if (_otherExitSequence != null && _otherExitSequence.IsActive())
            {
                _otherExitSequence.Kill();
            }

            _otherExitSequence = null;

            if (bg != null)
            {
                bg.DOKill();
            }

            if (display != null)
            {
                display.DOKill();
            }

            if (pipe != null)
            {
                pipe.DOKill();
            }

            if (hammer != null)
            {
                hammer.DOKill();
            }
        }

        void CacheStayPositions()
        {
            if (bg != null)
            {
                _bgStay = bg.position;
            }

            if (display != null)
            {
                _displayStay = display.position;
            }

            if (pipe != null)
            {
                _pipeStay = pipe.position;
            }

            if (hammer != null)
            {
                _hammerStay = hammer.position;
            }
        }

        void EnsureBgRayBlocker()
        {
            if (bg == null)
            {
                return;
            }

            _bgRayBlocker = bg.GetComponent<Collider2D>();
            if (_bgRayBlocker == null)
            {
                var box = bg.gameObject.AddComponent<BoxCollider2D>();
                var renderer = bg.GetComponent<SpriteRenderer>();
                if (renderer != null && renderer.sprite != null)
                {
                    box.size = renderer.sprite.bounds.size;
                    box.offset = renderer.sprite.bounds.center;
                }

                // Trigger：拦截射线，但不参与刚体运动碰撞。
                box.isTrigger = true;
                _bgRayBlocker = box;
            }
            else
            {
                _bgRayBlocker.isTrigger = true;
            }

            _bgRayBlocker.enabled = false;
        }

        void SetBgRayBlockEnabled(bool enabled)
        {
            if (_bgRayBlocker == null)
            {
                EnsureBgRayBlocker();
            }

            if (_bgRayBlocker != null)
            {
                _bgRayBlocker.enabled = enabled;
            }
        }

        void ResolveRefs()
        {
            if (sceneRoot == null)
            {
                sceneRoot = transform;
            }

            bg = ResolveChild(bg, "液压场景BG");
            bgInOut = ResolveChild(bgInOut, "液压场景BG in/out");
            display = ResolveChild(display, "显示屏");
            displayInOut = ResolveChild(displayInOut, "显示屏 in/out");
            pipe = ResolveChild(pipe, "管道");
            pipeInOut = ResolveChild(pipeInOut, "管道 in/out");
            hammer = ResolveChild(hammer, "锤头");
            hammerPressPoint = ResolveChild(hammerPressPoint, "锤头下压落点");

            if (NeedsResolve(otherVisuals))
            {
                otherVisuals = System.Array.Empty<Transform>();
            }

            if (materialLane == null)
            {
                materialLane = GetComponent<HydraulicMaterialLane>()
                    ?? GetComponentInChildren<HydraulicMaterialLane>(true);
            }

            if (materialBoard == null)
            {
                materialBoard = GetComponent<HydraulicMaterialBoard>()
                    ?? GetComponentInChildren<HydraulicMaterialBoard>(true);
            }
        }

        static bool NeedsResolve(Transform[] targets)
        {
            if (targets == null || targets.Length == 0)
            {
                return true;
            }

            for (var i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null)
                {
                    return true;
                }
            }

            return false;
        }

        Transform ResolveChild(Transform current, string childName)
        {
            return current != null ? current : FindChild(childName);
        }

        Transform FindChild(string childName)
        {
            var root = sceneRoot != null ? sceneRoot : transform;
            var direct = root.Find(childName);
            if (direct != null)
            {
                return direct;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        static void AppendMove(
            Sequence sequence,
            Transform target,
            Vector3 end,
            float duration,
            Ease ease,
            float atPosition)
        {
            if (target == null)
            {
                return;
            }

            if (duration <= 0f)
            {
                sequence.InsertCallback(atPosition, () => PlaceAtWorld(target, end));
                return;
            }

            sequence.Insert(atPosition, target.DOMove(end, duration).SetEase(ease));
        }

        Vector3 GetPressPosition()
        {
            return hammerPressPoint != null ? hammerPressPoint.position : _hammerStay + Vector3.down;
        }

        static Vector3 GetInOutPosition(Transform marker, Vector3 fallback)
        {
            return marker != null ? marker.position : fallback;
        }

        static void PlaceAt(Transform target, Transform marker)
        {
            if (target == null || marker == null)
            {
                return;
            }

            target.position = marker.position;
        }

        static void PlaceAtWorld(Transform target, Vector3 position)
        {
            if (target == null)
            {
                return;
            }

            target.position = position;
        }

        void SetRootActive(bool active)
        {
            if (sceneRoot != null)
            {
                sceneRoot.gameObject.SetActive(active);
            }
        }

        static void SetVisualActive(Transform target, bool active)
        {
            if (target != null)
            {
                target.gameObject.SetActive(active);
            }
        }

        void SetOtherVisualsActive(bool active)
        {
            if (otherVisuals == null)
            {
                return;
            }

            for (var i = 0; i < otherVisuals.Length; i++)
            {
                SetVisualActive(otherVisuals[i], active);
            }
        }
    }
}
