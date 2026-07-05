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

        /// <summary>跑局 HUD 需要 Overlay 根保持激活（供商店等逻辑参考）。</summary>
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
            UnsubscribeFlow();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        void Start()
        {
            ResolveGoldRefs();
            ResolveBattleRefs();
            SubscribeFlow();
            SyncRunState();
            RefreshGold();
            ApplyVisibility();
        }

        void SubscribeFlow()
        {
            var flow = GameFlowController.Instance;
            if (flow == null)
            {
                return;
            }

            flow.StateChanged += OnFlowStateChanged;
            flow.RunStarted += OnRunStarted;
        }

        void UnsubscribeFlow()
        {
            var flow = GameFlowController.Instance;
            if (flow == null)
            {
                return;
            }

            flow.StateChanged -= OnFlowStateChanged;
            flow.RunStarted -= OnRunStarted;
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
        }

        void SyncRunState(GameFlowState? state = null)
        {
            var next = state
                ?? (GameFlowController.Instance != null
                    ? GameFlowController.Instance.CurrentState
                    : GameFlowState.MainMenu);
            _inRun = next != GameFlowState.MainMenu;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResolveBattleRefs();
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
            var visible = _inRun && _suppressions.Count == 0;

            SetRootActive(goldCountRoot, visible);
            SetRootActive(goldIconRoot, visible);
            _anchorHp?.SetVisible(visible);
            _hullModSlots?.SetVisible(visible);

            if (visible)
            {
                UiSystem.Instance?.SetOverlayActive(true);
            }
            else
            {
                TryReleaseOverlay();
            }
        }

        void TryReleaseOverlay()
        {
            var ui = UiSystem.Instance;
            if (ui == null)
            {
                return;
            }

            var noticeOpen = ui.Notice != null && ui.Notice.IsShowing;
            var dialogueOpen = ui.Dialogue != null && ui.Dialogue.IsOpen;
            var enemyInfoOpen = ui.EnemyInfo != null && ui.EnemyInfo.IsShown;
            if (!noticeOpen && !dialogueOpen && !enemyInfoOpen)
            {
                ui.SetOverlayActive(false);
            }
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

        void ResolveBattleRefs()
        {
            if (!string.Equals(SceneManager.GetActiveScene().name, GameFlowScenes.Main, System.StringComparison.Ordinal))
            {
                _anchorHp = null;
                _hullModSlots = null;
                return;
            }

            _anchorHp = FindFirstObjectByType<AnchorHpTracker>(FindObjectsInactive.Include);
            _hullModSlots = FindFirstObjectByType<HullModSlotsController>(FindObjectsInactive.Include);
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
