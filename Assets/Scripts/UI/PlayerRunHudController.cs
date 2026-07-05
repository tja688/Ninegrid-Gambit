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
    /// 跑局常驻 HUD：金币 / 船锚余量 / 船体强化槽位。
    /// 运行时收拢到独立 <c>RunHud</c> 根节点，仅本组件控制显隐，与 Overlay / 敌人面板等解耦。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerRunHudController : MonoBehaviour
    {
        const int RunHudCanvasSortOrder = 100;

        public static PlayerRunHudController Instance { get; private set; }

        readonly HashSet<PlayerRunHudSuppressReason> _suppressions = new();

        [Header("独立 HUD 根（运行时组装）")]
        [SerializeField] Transform runHudRoot;

        GameObject _goldCountRoot;
        TMP_Text _goldText;
        TextAnimator_TMP _goldAnimator;

        AnchorHpTracker _anchorHp;
        HullModSlotsController _hullModSlots;

        bool _assembled;
        bool _flowHooked;
        Coroutine _reapplyRoutine;

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
            EnsureAssembled();
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
            if (!_assembled || runHudRoot == null)
            {
                return;
            }

            var want = ShouldShow();
            if (runHudRoot.gameObject.activeSelf != want)
            {
                runHudRoot.gameObject.SetActive(want);
            }
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
            _suppressions.Clear();
            RefreshGold();
            ApplyVisibility();
        }

        void OnFlowStateChanged(GameFlowState previous, GameFlowState next)
        {
            ClearStaleSuppressions(next);
            ApplyVisibility();
            ScheduleReapply();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureAssembled();
            ClearStaleSuppressions();
            RefreshGold();
            ApplyVisibility();
            ScheduleReapply();
        }

        void ClearStaleSuppressions(GameFlowState? state = null)
        {
            var next = state
                ?? (GameFlowController.Instance != null && GameFlowController.Instance.IsBooted
                    ? GameFlowController.Instance.CurrentState
                    : (GameFlowState?)null);

            if (next == null)
            {
                return;
            }

            if (!GameFlowScenes.IsBattleState(next.Value))
            {
                _suppressions.Remove(PlayerRunHudSuppressReason.ForgeScene);
            }

            if (next.Value != GameFlowState.Prologue)
            {
                _suppressions.Remove(PlayerRunHudSuppressReason.PrologueOpening);
            }
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
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();

            _reapplyRoutine = null;
            ClearStaleSuppressions();
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
            if (!ShouldShow() || _goldCountRoot == null)
            {
                return;
            }

            var gold = RunData.Current != null ? RunData.Current.Gold : RunData.Ensure().Gold;
            var content = gold.ToString();
            if (_goldAnimator != null && _goldCountRoot.activeInHierarchy)
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

        bool ShouldShow()
        {
            if (_suppressions.Count > 0)
            {
                return false;
            }

            return IsRunActive();
        }

        bool IsRunActive()
        {
            var flow = GameFlowController.Instance;
            if (flow != null && flow.IsBooted && flow.CurrentState != GameFlowState.MainMenu)
            {
                return true;
            }

            return RunData.Current != null;
        }

        void ApplyVisibility()
        {
            EnsureAssembled();
            if (runHudRoot == null)
            {
                return;
            }

            var visible = ShouldShow();
            runHudRoot.gameObject.SetActive(visible);
            if (visible)
            {
                RefreshGold();
            }
        }

        void EnsureAssembled()
        {
            if (_assembled)
            {
                return;
            }

            var ui = UiSystem.Instance;
            if (ui == null)
            {
                return;
            }

            var uiRoot = ui.transform;

            if (runHudRoot == null)
            {
                var existing = uiRoot.Find("RunHud");
                runHudRoot = existing != null
                    ? existing
                    : new GameObject("RunHud").transform;
                runHudRoot.SetParent(uiRoot, false);
            }

            var canvasRoot = runHudRoot.Find("RunHudCanvas");
            Transform canvasTransform;
            if (canvasRoot == null)
            {
                var canvasGo = new GameObject("RunHudCanvas");
                canvasTransform = canvasGo.transform;
                canvasTransform.SetParent(runHudRoot, false);

                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = RunHudCanvasSortOrder;

                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(480f, 270f);
                scaler.matchWidthOrHeight = 0f;
            }
            else
            {
                canvasTransform = canvasRoot;
            }

            var goldCount = FindRunHudElement(uiRoot, "金币数量");
            var goldIcon = FindRunHudElement(uiRoot, "金币图标");
            var anchorRoot = FindRunHudElement(uiRoot, "船锚余量");
            var hullRoot = FindRunHudElement(uiRoot, "船体强化槽位");

            if (goldCount != null)
            {
                ReparentPreserveWorld(goldCount, canvasTransform);
                _goldCountRoot = goldCount.gameObject;
                _goldText = _goldCountRoot.GetComponent<TMP_Text>();
                _goldAnimator = _goldCountRoot.GetComponent<TextAnimator_TMP>();
                DisableRaycastOnText(_goldCountRoot);
            }

            if (goldIcon != null)
            {
                ReparentPreserveWorld(goldIcon, runHudRoot);
                DisableRaycastOnText(goldIcon.gameObject);
            }

            if (anchorRoot != null)
            {
                ReparentPreserveWorld(anchorRoot, runHudRoot);
                _anchorHp = anchorRoot.GetComponent<AnchorHpTracker>()
                    ?? anchorRoot.gameObject.AddComponent<AnchorHpTracker>();
            }

            if (hullRoot != null)
            {
                ReparentPreserveWorld(hullRoot, runHudRoot);
                _hullModSlots = hullRoot.GetComponent<HullModSlotsController>()
                    ?? hullRoot.gameObject.AddComponent<HullModSlotsController>();
            }

            _assembled = true;
        }

        static void ReparentPreserveWorld(Transform child, Transform newParent)
        {
            if (child == null || newParent == null || child.parent == newParent)
            {
                return;
            }

            child.SetParent(newParent, true);
        }

        static Transform FindRunHudElement(Transform uiRoot, string trimmedName)
        {
            Transform best = null;
            var bestDepth = int.MaxValue;

            var all = uiRoot.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null || t.name.Trim() != trimmedName)
                {
                    continue;
                }

                if (IsUnderShopPanel(t))
                {
                    continue;
                }

                var depth = GetDepth(t, uiRoot);
                if (depth >= bestDepth)
                {
                    continue;
                }

                best = t;
                bestDepth = depth;
            }

            return best;
        }

        static bool IsUnderShopPanel(Transform t)
        {
            var current = t.parent;
            while (current != null)
            {
                var name = current.name.Trim();
                if (name == "精炼厂panel" || name == "船坞panel")
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        static int GetDepth(Transform t, Transform root)
        {
            var depth = 0;
            var current = t;
            while (current != null && current != root)
            {
                depth++;
                current = current.parent;
            }

            return depth;
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
