using System.Collections;
using System.Collections.Generic;
using Febucci.TextAnimatorForUnity.TextMeshPro;
using NineGrid.Battle;
using NineGrid.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NineGrid.UI
{
    public enum PlayerRunHudSuppressReason
    {
        /// <summary>序章开场演出至战斗环节开始前。</summary>
        PrologueOpening = 0,

        /// <summary>战斗中打开锻造场景（液压子场景）。</summary>
        ForgeScene = 1,
    }

    /// <summary>
    /// 跑局常驻 HUD：金币数量/图标、船锚余量、船体强化槽位。默认显示，通过 <see cref="Suppress"/> 按需屏蔽。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerRunHudController : MonoBehaviour
    {
        public static PlayerRunHudController Instance { get; private set; }

        readonly HashSet<PlayerRunHudSuppressReason> _suppressions = new();

        [Header("金币（留空自动解析）")]
        [SerializeField] GameObject goldCountRoot;
        [SerializeField] GameObject goldIconRoot;

        TMP_Text _goldText;
        TextAnimator_TMP _goldAnimator;

        AnchorHpTracker _anchorHp;
        HullModSlotsController _hullModSlots;

        bool _inRun;
        bool _flowHooked;
        Coroutine _reapplyRoutine;

        /// <summary>跑局 HUD 需要 Overlay 根保持激活（供 UiSystem.ShouldKeepOverlayActive 参考）。</summary>
        public bool WantsOverlayActive => _inRun && _suppressions.Count == 0;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnhookFlow();

            if (_reapplyRoutine != null)
            {
                StopCoroutine(_reapplyRoutine);
                _reapplyRoutine = null;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        void Start()
        {
            ResolveGoldRefs();
            ResolveHudRefs();
            TryHookFlow(forceApply: true);
        }

        void Update()
        {
            if (!_flowHooked)
            {
                TryHookFlow(forceApply: true);
            }
        }

        void LateUpdate()
        {
            if (!WantsOverlayActive)
            {
                return;
            }

            MaintainVisibility();
        }

        void TryHookFlow(bool forceApply)
        {
            var flow = GameFlowController.Instance;
            if (flow == null || !flow.IsBooted)
            {
                return;
            }

            if (!_flowHooked)
            {
                flow.StateChanged += OnFlowStateChanged;
                flow.RunStarted += OnRunStarted;
                _flowHooked = true;
            }

            SyncRunState();
            if (forceApply)
            {
                RefreshGold();
                ApplyVisibility();
            }
        }

        void UnhookFlow()
        {
            if (!_flowHooked)
            {
                return;
            }

            var flow = GameFlowController.Instance;
            if (flow != null)
            {
                flow.StateChanged -= OnFlowStateChanged;
                flow.RunStarted -= OnRunStarted;
            }

            _flowHooked = false;
        }

        void OnRunStarted()
        {
            _inRun = true;
            _suppressions.Clear();
            RefreshGold();
            ApplyVisibility();
        }

        void OnFlowStateChanged(GameFlowState previous, GameFlowState next)
        {
            SyncRunState(next);
            ApplyVisibility();
            ScheduleReapply();
        }

        void SyncRunState(GameFlowState? state = null)
        {
            var flow = GameFlowController.Instance;
            var next = state
                ?? (flow != null && flow.IsBooted
                    ? flow.CurrentState
                    : GameFlowState.MainMenu);
            _inRun = next != GameFlowState.MainMenu;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResolveGoldRefs();
            ResolveHudRefs();
            SyncRunState();
            RefreshGold();
            ApplyVisibility();
            ScheduleReapply();
        }

        void ScheduleReapply()
        {
            if (_reapplyRoutine != null)
            {
                StopCoroutine(_reapplyRoutine);
            }

            _reapplyRoutine = StartCoroutine(ReapplyAfterSceneSettle());
        }

        IEnumerator ReapplyAfterSceneSettle()
        {
            // 等一帧：让 SceneFlowDirector 淡入淡出、敌人面板退场等异步逻辑先跑完。
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();

            _reapplyRoutine = null;
            SyncRunState();
            RefreshGold();
            ApplyVisibility();
        }

        public void Suppress(PlayerRunHudSuppressReason reason)
        {
            if (_suppressions.Add(reason))
            {
                ApplyVisibility();
            }
        }

        public void Release(PlayerRunHudSuppressReason reason)
        {
            if (_suppressions.Remove(reason))
            {
                ApplyVisibility();
            }
        }

        public void RefreshGold()
        {
            ResolveGoldRefs();
            if (!_inRun || goldCountRoot == null)
            {
                return;
            }

            var gold = RunData.Current != null ? RunData.Current.Gold : RunData.Ensure().Gold;
            var content = gold.ToString();
            if (_goldAnimator != null && goldCountRoot.activeInHierarchy)
            {
                _goldAnimator.SetText(content, hideText: false);
                _goldAnimator.SetVisibilityEntireText(true, canPlayEffects: false);
                return;
            }

            if (_goldText != null)
            {
                _goldText.text = content;
            }
        }

        void ApplyVisibility()
        {
            var visible = WantsOverlayActive;

            if (visible)
            {
                UiSystem.Instance?.SetOverlayActive(true);
            }

            SetRootActive(goldCountRoot, visible);
            SetRootActive(goldIconRoot, visible);
            _anchorHp?.SetVisible(visible);
            _hullModSlots?.SetVisible(visible);

            if (!visible)
            {
                UiSystem.Instance?.SetOverlayActive(false);
            }
        }

        void MaintainVisibility()
        {
            ResolveGoldRefs();
            ResolveHudRefs();

            UiSystem.Instance?.SetOverlayActive(true);
            SetRootActive(goldCountRoot, true);
            SetRootActive(goldIconRoot, true);
            _anchorHp?.SetVisible(true);
            _hullModSlots?.SetVisible(true);
        }

        static void SetRootActive(GameObject go, bool active)
        {
            if (go != null)
            {
                go.SetActive(active);
            }
        }

        void ResolveGoldRefs()
        {
            var uiRoot = UiSystem.Instance != null ? UiSystem.Instance.transform : transform;

            if (goldCountRoot == null)
            {
                goldCountRoot = FindDeepChild(uiRoot, "金币数量")?.gameObject;
            }

            if (goldIconRoot == null)
            {
                goldIconRoot = FindDeepChild(uiRoot, "金币图标")?.gameObject;
            }

            if (goldCountRoot != null && _goldText == null)
            {
                _goldText = goldCountRoot.GetComponent<TMP_Text>();
                _goldAnimator = goldCountRoot.GetComponent<TextAnimator_TMP>();
                DisableRaycastOnText(goldCountRoot);
            }

            if (goldIconRoot != null)
            {
                DisableRaycastOnText(goldIconRoot);
            }
        }

        void ResolveHudRefs()
        {
            var ui = UiSystem.Instance;
            if (ui == null)
            {
                return;
            }

            _anchorHp ??= ui.GetComponentInChildren<AnchorHpTracker>(true);
            _hullModSlots ??= ui.GetComponentInChildren<HullModSlotsController>(true);
        }

        static Transform FindDeepChild(Transform parent, string trimmedName)
        {
            if (parent == null)
            {
                return null;
            }

            var transforms = parent.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t != null && t.name.Trim() == trimmedName)
                {
                    return t;
                }
            }

            return null;
        }

        static void DisableRaycastOnText(GameObject textObject)
        {
            if (textObject == null)
            {
                return;
            }

            var graphics = textObject.GetComponentsInChildren<Graphic>(true);
            for (var i = 0; i < graphics.Length; i++)
            {
                graphics[i].raycastTarget = false;
            }
        }
    }
}
