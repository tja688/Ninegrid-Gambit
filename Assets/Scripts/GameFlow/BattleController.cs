using System;
using System.Collections;
using DG.Tweening;
using NineGrid.Presentation.Visuals;
using NineGrid.UI;
using NinegridGambit.Grapple;
using UnityEngine;
using UnityEngine.Serialization;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NineGrid.GameFlow
{
    public enum BattlePhaseState
    {
        Idle = 0,
        Entering = 1,
        Active = 2,
        Exiting = 3,
    }

    /// <summary>
    /// 战斗环节独立状态机：由 <see cref="GameFlowController"/> 在序章/战斗节点就绪后拉起。
    /// 负责权限解锁、敌人信息面板、锻造 / 抛锚入口；胜负结算留位。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleController : MonoBehaviour
    {
        public static BattleController Instance { get; private set; }

        [Header("Ships")]
        [SerializeField] SelectableSceneElement playerSelectable;
        [SerializeField] SelectableSceneElement enemySelectable;
        [SerializeField] Transform player;
        [SerializeField] Transform playerExit;

        [Header("Systems")]
        [SerializeField] SceneElementPointerSelector pointerSelector;
        [SerializeField] HydraulicSceneController hydraulicScene;
        [FormerlySerializedAs("enemyIntroducePanel")]
        [SerializeField] EnemyIntroducePanelController enemyInfoPanel;
        [SerializeField] AnchorChainLauncher anchorChain;

        [Header("Exit Motion")]
        [SerializeField] float playerExitDuration = 1f;
        [SerializeField] Ease playerExitEase = Ease.InCubic;

        [Header("Debug Input")]
        [SerializeField] bool enableDebugHotkeys = true;
        [SerializeField] KeyCode endBattleKey = KeyCode.Keypad4;

        Coroutine _routine;
        Coroutine _anchorRoutine;
        Tween _playerExitTween;
        BattlePhaseState _state = BattlePhaseState.Idle;

        public BattlePhaseState State => _state;
        public bool IsBusy =>
            _state == BattlePhaseState.Entering
            || _state == BattlePhaseState.Exiting;
        public bool IsActive => _state == BattlePhaseState.Active;

        /// <summary>战斗中可点击玩家进入锻造。</summary>
        public bool CanEnterForgeMode { get; private set; }

        /// <summary>战斗中可点击敌人触发抛锚准备（子状态机尚未落地）。</summary>
        public bool CanEnterAnchorMode { get; private set; }

        /// <summary>战斗入场完成（权限已开，敌人信息面板入场结束，进入 Active）。</summary>
        public event Action EnterCompleted;

        /// <summary>战斗退场完成。暂不推进主流程下一节点。</summary>
        public event Action ExitCompleted;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Battle] 场景中存在多个实例，保留先创建的。");
                return;
            }

            Instance = this;
            ResolveRefs();
            LockPermissions();
            enemyInfoPanel?.HideImmediate();
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
            // 小键盘挂在始终激活的 GameFlow 上。
            // 液压宿主默认可失活，不能依赖它自己的 Update。
            if (enableDebugHotkeys)
            {
                HandleDebugHotkeys();
            }

            if (_state == BattlePhaseState.Active)
            {
                UpdateActiveCombat();
            }
        }

        void HandleDebugHotkeys()
        {
            // Keypad1：液压入场 / 常规退场（宿主失活时由这里拉起）
            if (DebugHotkeyInput.WasPressedThisFrame(KeyCode.Keypad1))
            {
                ResolveRefs();
                if (hydraulicScene == null)
                {
                    Debug.LogWarning("[Battle] 找不到液压场景，无法响应小键盘1。");
                }
                else if (hydraulicScene.State == HydraulicSceneState.Hidden)
                {
                    hydraulicScene.Enter();
                }
                else if (hydraulicScene.State == HydraulicSceneState.Active)
                {
                    hydraulicScene.Exit();
                }
            }

            // Keypad2：液压完成退场
            if (DebugHotkeyInput.WasPressedThisFrame(KeyCode.Keypad2))
            {
                ResolveRefs();
                if (hydraulicScene == null)
                {
                    Debug.LogWarning("[Battle] 找不到液压场景，无法响应小键盘2。");
                }
                else
                {
                    hydraulicScene.PlayHydraulic();
                }
            }

            // Keypad4：结束战斗（仅 Active）
            if (_state == BattlePhaseState.Active && DebugHotkeyInput.WasPressedThisFrame(endBattleKey))
            {
                ExitBattle();
            }
        }

        /// <summary>主流程钩子：进入战斗环节。</summary>
        public bool EnterBattle()
        {
            if (_state != BattlePhaseState.Idle)
            {
                Debug.LogWarning($"[Battle] EnterBattle 被忽略，当前状态={_state}");
                return false;
            }

            ResolveRefs();
            KillMotion();
            _state = BattlePhaseState.Entering;
            _routine = StartCoroutine(EnterRoutine());
            return true;
        }

        /// <summary>结束战斗（调试热键 / 后续胜负条件）。</summary>
        public bool ExitBattle()
        {
            if (_state != BattlePhaseState.Active)
            {
                Debug.LogWarning($"[Battle] ExitBattle 被忽略，当前状态={_state}");
                return false;
            }

            KillMotion();
            _state = BattlePhaseState.Exiting;
            _routine = StartCoroutine(ExitRoutine());
            return true;
        }

        /// <summary>占位：敌人/玩家死亡时结束战斗。当前未接线。</summary>
        public void NotifyCombatantDefeated(bool playerDied)
        {
            // TODO: 接入 HP / 胜负后在此调用 ExitBattle，并区分胜负演出。
            Debug.Log($"[Battle] NotifyCombatantDefeated 占位 playerDied={playerDied}");
            if (_state == BattlePhaseState.Active)
            {
                ExitBattle();
            }
        }

        /// <summary>进入锻造子状态机（液压场景，可默认失活，由 Enter 拉起）。</summary>
        public bool TryEnterForgeMode()
        {
            if (!CanEnterForgeMode || _state != BattlePhaseState.Active)
            {
                return false;
            }

            ResolveRefs();
            if (hydraulicScene == null)
            {
                Debug.LogWarning("[Battle] 找不到 HydraulicSceneController，无法进入锻造。");
                return false;
            }

            if (hydraulicScene.State != HydraulicSceneState.Hidden)
            {
                return false;
            }

            return hydraulicScene.Enter();
        }

        /// <summary>
        /// 点击敌人：抛锚准备。测试阶段完整播完抛锚后取消，后续接互撞子状态机。
        /// </summary>
        public bool TryEnterAnchorMode()
        {
            if (!CanEnterAnchorMode || _state != BattlePhaseState.Active)
            {
                return false;
            }

            ResolveRefs();
            if (anchorChain == null)
            {
                Debug.LogWarning("[Battle] 找不到 AnchorChainSystem，无法抛锚。");
                return false;
            }

            if (anchorChain.IsBusy)
            {
                return false;
            }

            // 扔出船锚准备（等飞出+收绳完成后再取消，避免同帧 Reset 看不见）。
            if (!anchorChain.Fire())
            {
                Debug.LogWarning(
                    $"[Battle] Anchor Fire 失败 phase={anchorChain.CurrentPhase} canFire={anchorChain.CanFire}");
                return false;
            }

            if (_anchorRoutine != null)
            {
                StopCoroutine(_anchorRoutine);
            }

            _anchorRoutine = StartCoroutine(AnchorPrepareThenCancelRoutine());
            return true;
        }

        IEnumerator AnchorPrepareThenCancelRoutine()
        {
            // 等抛锚飞出并收绳到 Attached，便于测试看见完整抛锚。
            var timeoutAt = Time.unscaledTime + 5f;
            yield return new WaitUntil(() =>
                anchorChain == null
                || anchorChain.CurrentPhase == AnchorChainLauncher.Phase.Attached
                || Time.unscaledTime >= timeoutAt);

            // TODO: 抛锚互撞子状态机 — 命中后双方拉近、结算等。
            // 测试阶段先做到「发射准备」这一步，随后取消并隐藏锚链。
            if (anchorChain != null)
            {
                anchorChain.ResetToIdle();
            }

            _anchorRoutine = null;
        }

        IEnumerator EnterRoutine()
        {
            UnlockPermissions();

            // 战斗开始：敌人信息面板入场，介绍文字默认显示，战斗中保持。
            if (enemyInfoPanel != null)
            {
                var enterDone = false;
                void OnEnterCompleted()
                {
                    enemyInfoPanel.EnterCompleted -= OnEnterCompleted;
                    enterDone = true;
                }

                enemyInfoPanel.EnterCompleted += OnEnterCompleted;
                try
                {
                    enemyInfoPanel.RequestShow();
                }
                catch (Exception ex)
                {
                    enemyInfoPanel.EnterCompleted -= OnEnterCompleted;
                    Debug.LogException(ex);
                    enterDone = true;
                }

                if (!enterDone && enemyInfoPanel.IsShown)
                {
                    enemyInfoPanel.EnterCompleted -= OnEnterCompleted;
                    enterDone = true;
                }

                if (!enterDone)
                {
                    var timeout = Time.unscaledTime + 2f;
                    yield return new WaitUntil(() =>
                        enterDone
                        || enemyInfoPanel.State == EnemyIntroducePanelState.Shown
                        || enemyInfoPanel.State == EnemyIntroducePanelState.Hidden
                        || Time.unscaledTime >= timeout);

                    enemyInfoPanel.EnterCompleted -= OnEnterCompleted;
                }
            }

            _routine = null;
            _state = BattlePhaseState.Active;
            EnterCompleted?.Invoke();
            Debug.Log("[Battle] Active");
        }

        IEnumerator ExitRoutine()
        {
            LockPermissions();

            // 若锻造子场景开着，先常规退场。
            if (hydraulicScene != null
                && (hydraulicScene.IsActive || hydraulicScene.IsBusy))
            {
                // 宿主可能仍激活且处于 Active；Hidden 则无需处理。
                if (hydraulicScene.State == HydraulicSceneState.Active)
                {
                    var hydraulicDone = false;
                    void OnHydraulicExit()
                    {
                        hydraulicScene.ExitCompleted -= OnHydraulicExit;
                        hydraulicDone = true;
                    }

                    hydraulicScene.ExitCompleted += OnHydraulicExit;
                    if (!hydraulicScene.Exit())
                    {
                        hydraulicScene.ExitCompleted -= OnHydraulicExit;
                        hydraulicDone = true;
                    }

                    yield return new WaitUntil(() =>
                        hydraulicDone || hydraulicScene.State == HydraulicSceneState.Hidden);
                }
            }

            // 战斗结束：收起敌人信息面板与文字。
            if (enemyInfoPanel != null
                && enemyInfoPanel.State != EnemyIntroducePanelState.Hidden)
            {
                var panelDone = false;
                void OnPanelExit()
                {
                    enemyInfoPanel.ExitCompleted -= OnPanelExit;
                    panelDone = true;
                }

                enemyInfoPanel.ExitCompleted += OnPanelExit;
                enemyInfoPanel.RequestHide();

                if (enemyInfoPanel.State == EnemyIntroducePanelState.Hidden)
                {
                    enemyInfoPanel.ExitCompleted -= OnPanelExit;
                    panelDone = true;
                }

                if (!panelDone)
                {
                    var timeout = Time.unscaledTime + 2f;
                    yield return new WaitUntil(() =>
                        panelDone
                        || enemyInfoPanel.State == EnemyIntroducePanelState.Hidden
                        || Time.unscaledTime >= timeout);
                    enemyInfoPanel.ExitCompleted -= OnPanelExit;
                }
            }
            else
            {
                enemyInfoPanel?.HideImmediate();
            }

            // 玩家缓动走到 player exit。
            yield return SailPlayerToExit();

            _routine = null;
            _state = BattlePhaseState.Idle;
            ExitCompleted?.Invoke();
            Debug.Log("[Battle] Idle（未推进主流程下一节点）");
        }

        void UpdateActiveCombat()
        {
            if (!WasPrimaryClickPressedThisFrame())
            {
                return;
            }

            var hovered = pointerSelector != null ? pointerSelector.Hovered : null;
            if (hovered == null)
            {
                return;
            }

            if (hovered == playerSelectable)
            {
                TryEnterForgeMode();
                return;
            }

            if (hovered == enemySelectable)
            {
                TryEnterAnchorMode();
            }
        }

        void UnlockPermissions()
        {
            SetSelectableEnabled(playerSelectable, true);
            SetSelectableEnabled(enemySelectable, true);
            CanEnterForgeMode = true;
            CanEnterAnchorMode = true;
        }

        void LockPermissions()
        {
            CanEnterForgeMode = false;
            CanEnterAnchorMode = false;
            SetSelectableEnabled(playerSelectable, false);
            SetSelectableEnabled(enemySelectable, false);

            if (playerSelectable != null)
            {
                playerSelectable.SetHovered(false);
            }

            if (enemySelectable != null)
            {
                enemySelectable.SetHovered(false);
            }
        }

        static void SetSelectableEnabled(SelectableSceneElement selectable, bool enabled)
        {
            if (selectable != null)
            {
                selectable.enabled = enabled;
            }
        }

        IEnumerator SailPlayerToExit()
        {
            ResolveRefs();
            if (player == null || playerExit == null)
            {
                Debug.LogWarning("[Battle] player / player exit 缺失，跳过出场缓动。");
                yield break;
            }

            if (playerExitDuration <= 0f)
            {
                player.position = playerExit.position;
                yield break;
            }

            var completed = false;
            _playerExitTween = player
                .DOMove(playerExit.position, playerExitDuration)
                .SetEase(playerExitEase)
                .SetUpdate(true)
                .OnComplete(() => completed = true);

            yield return new WaitUntil(() => completed);
            _playerExitTween = null;
            player.position = playerExit.position;
        }

        void KillMotion()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            if (_anchorRoutine != null)
            {
                StopCoroutine(_anchorRoutine);
                _anchorRoutine = null;
            }

            if (anchorChain != null && anchorChain.IsBusy)
            {
                anchorChain.ResetToIdle();
            }

            if (_playerExitTween != null && _playerExitTween.IsActive())
            {
                _playerExitTween.Kill();
            }

            _playerExitTween = null;

            if (player != null)
            {
                player.DOKill();
            }
        }

        void ResolveRefs()
        {
            if (player == null)
            {
                player = FindByName("player");
            }

            if (playerExit == null)
            {
                playerExit = FindByName("player exit");
            }

            if (playerSelectable == null && player != null)
            {
                playerSelectable = player.GetComponent<SelectableSceneElement>();
            }

            if (enemySelectable == null)
            {
                var enemy = FindByName("enemy");
                if (enemy != null)
                {
                    enemySelectable = enemy.GetComponent<SelectableSceneElement>();
                }
            }

            if (pointerSelector == null)
            {
                pointerSelector = FindFirstObjectByType<SceneElementPointerSelector>();
            }

            if (hydraulicScene == null)
            {
                // 液压场景默认失活，需包含未激活对象。
                hydraulicScene = HydraulicSceneController.Instance
                    ?? FindFirstObjectByType<HydraulicSceneController>(FindObjectsInactive.Include);
            }

            if (enemyInfoPanel == null)
            {
                enemyInfoPanel = UiSystem.Instance != null
                    ? UiSystem.Instance.EnemyInfo
                    : FindFirstObjectByType<EnemyIntroducePanelController>();
            }

            if (anchorChain == null)
            {
                anchorChain = FindFirstObjectByType<AnchorChainLauncher>(FindObjectsInactive.Include);
            }
        }

        static Transform FindByName(string objectName)
        {
            var go = GameObject.Find(objectName);
            return go != null ? go.transform : null;
        }

        static bool WasPrimaryClickPressedThisFrame()
        {
            // 鼠标点击：旧 Input 优先，新 Input System 兜底。
            if (Input.GetMouseButtonDown(0))
            {
                return true;
            }

#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                return true;
            }
#endif
            return false;
        }
    }
}
