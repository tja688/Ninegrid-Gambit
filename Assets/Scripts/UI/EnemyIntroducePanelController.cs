using System;
using DG.Tweening;
using Febucci.TextAnimatorForUnity.TextMeshPro;
using TMPro;
using UnityEngine;

namespace NineGrid.UI
{
    public enum EnemyIntroducePanelState
    {
        Hidden = 0,
        Entering = 1,
        Shown = 2,
        Exiting = 3,
    }

    /// <summary>
    /// 敌人信息面板（Enemy Info Panel）：战斗开始入场并默认显示介绍文字，
    /// 战斗结束退场收起。从 <c>Enemy Info Panel in</c> 缓动入/出，就位后播 TextAnimator。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyIntroducePanelController : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] Transform panel;
        [SerializeField] Transform panelIn;

        [Header("Text")]
        [SerializeField] GameObject textObject;
        [SerializeField] TextAnimator_TMP textAnimator;
        [SerializeField] TextMeshProUGUI textMesh;
        [SerializeField] UiSystem ui;

        [Header("Motion")]
        [SerializeField] float moveDuration = 0.4f;
        [SerializeField] Ease enterEase = Ease.OutCubic;
        [SerializeField] Ease exitEase = Ease.InCubic;

        [Header("Content")]
        [Tooltip("为空时使用 TextMesh 上已有文案。")]
        [SerializeField] [TextArea(2, 8)] string defaultText;

        Vector3 _stayPosition;
        bool _stayCached;
        bool _wantVisible;
        bool _suspended;
        bool _resumeAfterSuspend;
        string _pendingText;
        Tween _moveTween;
        EnemyIntroducePanelState _state = EnemyIntroducePanelState.Hidden;

        public EnemyIntroducePanelState State => _state;
        public bool IsBusy =>
            _state == EnemyIntroducePanelState.Entering
            || _state == EnemyIntroducePanelState.Exiting;
        public bool IsShown => _state == EnemyIntroducePanelState.Shown;
        public bool WantVisible => _wantVisible;
        public bool IsSuspended => _suspended;

        public event Action EnterCompleted;
        public event Action ExitCompleted;

        void Awake()
        {
            ResolveRefs();
            CacheStayPosition();
            // 先缓存文案，再隐藏；隐藏时不得对未激活 TMP 调 SetText。
            if (string.IsNullOrEmpty(_pendingText))
            {
                _pendingText = ResolveDefaultText();
            }

            ApplyHiddenImmediate();
        }

        void OnDestroy()
        {
            KillMotion();
        }

        /// <summary>申请入场。可在退场中途打断并重入。</summary>
        public void RequestShow(string text = null)
        {
            ResolveRefs();
            CacheStayPosition();

            if (!string.IsNullOrEmpty(text))
            {
                _pendingText = text;
            }
            else if (string.IsNullOrEmpty(_pendingText))
            {
                _pendingText = ResolveDefaultText();
            }

            _wantVisible = true;

            // 锻造挂起期间只记意图，退出锻造时 ResumeImmediate 再亮。
            if (_suspended)
            {
                _resumeAfterSuspend = true;
                return;
            }

            if (_state == EnemyIntroducePanelState.Hidden
                || _state == EnemyIntroducePanelState.Exiting)
            {
                BeginEnter();
            }
        }

        /// <summary>申请退场。可在入场中途打断。</summary>
        public void RequestHide()
        {
            ClearSuspendFlags();
            _wantVisible = false;
            if (_state == EnemyIntroducePanelState.Shown
                || _state == EnemyIntroducePanelState.Entering)
            {
                BeginExit();
            }
        }

        /// <summary>立即隐藏，无缓动。</summary>
        public void HideImmediate()
        {
            ClearSuspendFlags();
            _wantVisible = false;
            ApplyHiddenImmediate();
        }

        /// <summary>
        /// 锻造等子模式：瞬间藏起面板与文字，保留「战斗中应显示」意图。
        /// </summary>
        public void SuspendImmediate()
        {
            ResolveRefs();
            CacheStayPosition();

            if (_suspended)
            {
                return;
            }

            _suspended = true;
            _resumeAfterSuspend = _wantVisible
                                  || _state == EnemyIntroducePanelState.Shown
                                  || _state == EnemyIntroducePanelState.Entering;

            KillMotion();
            HideTextVisual();
            SetPanelActive(false);
        }

        /// <summary>退出子模式后瞬间恢复面板与文字（无缓动）。</summary>
        public void ResumeImmediate()
        {
            if (!_suspended)
            {
                return;
            }

            var shouldShow = _resumeAfterSuspend;
            ClearSuspendFlags();

            if (!shouldShow)
            {
                return;
            }

            ResolveRefs();
            CacheStayPosition();
            KillMotion();

            if (panel != null)
            {
                panel.position = _stayPosition;
            }

            SetPanelActive(true);
            _wantVisible = true;
            _state = EnemyIntroducePanelState.Shown;
            HideTextVisual();
        }

        /// <summary>瞬间显示介绍文字（不改变面板状态，无 TextAnimator 入场）。</summary>
        public void ShowDescriptionImmediate()
        {
            if (_suspended || _state != EnemyIntroducePanelState.Shown)
            {
                return;
            }

            PresentTextInstant(_pendingText);
        }

        /// <summary>瞬间隐藏介绍文字（不改变面板状态）。</summary>
        public void HideDescriptionImmediate()
        {
            HideTextVisual();
        }

        void ClearSuspendFlags()
        {
            _suspended = false;
            _resumeAfterSuspend = false;
        }

        void BeginEnter()
        {
            KillMotion();
            SetPanelActive(true);
            HideTextVisual();

            if (_state == EnemyIntroducePanelState.Hidden)
            {
                PlaceAt(panel, panelIn);
            }

            _state = EnemyIntroducePanelState.Entering;

            if (panel == null)
            {
                FinishEnter();
                return;
            }

            if (moveDuration <= 0f)
            {
                panel.position = _stayPosition;
                FinishEnter();
                return;
            }

            _moveTween = panel
                .DOMove(_stayPosition, moveDuration)
                .SetEase(enterEase)
                .SetUpdate(true)
                .OnComplete(FinishEnter);
        }

        void FinishEnter()
        {
            _moveTween = null;
            if (panel != null)
            {
                panel.position = _stayPosition;
            }

            _state = EnemyIntroducePanelState.Shown;

            if (!_wantVisible)
            {
                BeginExit();
                return;
            }

            // 介绍文字改由战斗 hover 控制；面板入场时不自动亮字。
            HideTextVisual();
            EnterCompleted?.Invoke();
        }

        void BeginExit()
        {
            KillMotion();
            HideTextVisual();
            _state = EnemyIntroducePanelState.Exiting;

            if (panel == null)
            {
                FinishExit();
                return;
            }

            var end = panelIn != null ? panelIn.position : _stayPosition;
            if (moveDuration <= 0f)
            {
                panel.position = end;
                FinishExit();
                return;
            }

            _moveTween = panel
                .DOMove(end, moveDuration)
                .SetEase(exitEase)
                .SetUpdate(true)
                .OnComplete(FinishExit);
        }

        void FinishExit()
        {
            _moveTween = null;
            SetPanelActive(false);
            HideTextVisual();
            _state = EnemyIntroducePanelState.Hidden;
            ExitCompleted?.Invoke();

            if (_wantVisible)
            {
                BeginEnter();
            }
        }

        void PresentText(string text)
        {
            EnsureTextHierarchyActive();

            if (textAnimator == null && textObject != null)
            {
                textAnimator = textObject.GetComponent<TextAnimator_TMP>();
            }

            var content = text ?? string.Empty;
            if (textAnimator != null && IsTextReadyForAnimator())
            {
                textAnimator.SetText(content, hideText: true);
                textAnimator.SetVisibilityEntireText(true, canPlayEffects: true);
                return;
            }

            if (textMesh != null)
            {
                textMesh.text = content;
            }
        }

        void PresentTextInstant(string text)
        {
            EnsureTextHierarchyActive();

            if (textAnimator == null && textObject != null)
            {
                textAnimator = textObject.GetComponent<TextAnimator_TMP>();
            }

            var content = text ?? string.Empty;
            if (textMesh != null)
            {
                textMesh.text = content;
            }

            if (textAnimator != null && IsTextReadyForAnimator())
            {
                textAnimator.SetText(content, hideText: false);
                textAnimator.SetVisibilityEntireText(true, canPlayEffects: false);
                return;
            }

            SetTextActive(true);
        }

        /// <summary>
        /// 仅在文字层级已激活时清 TextAnimator。
        /// 未激活的 TMP 调 SetText 会在 ClearMesh 处 NRE。
        /// </summary>
        void ClearTextAnimatorIfReady()
        {
            if (!IsTextReadyForAnimator())
            {
                return;
            }

            if (textAnimator != null)
            {
                textAnimator.SetText(string.Empty);
            }
        }

        void HideTextVisual()
        {
            ClearTextAnimatorIfReady();
            SetTextActive(false);
            ReleaseOverlayIfUnused();
        }

        void EnsureTextHierarchyActive()
        {
            if (ui == null)
            {
                ui = UiSystem.Instance;
            }

            // Enemy Info Text 挂在 Overlay 下，父级未激活时 TMP mesh 不可用。
            ui?.SetOverlayActive(true);
            SetTextActive(true);
        }

        bool IsTextReadyForAnimator()
        {
            return textObject != null
                   && textObject.activeInHierarchy
                   && textAnimator != null;
        }

        void ReleaseOverlayIfUnused()
        {
            if (ui == null)
            {
                ui = UiSystem.Instance;
            }

            if (ui == null)
            {
                return;
            }

            ui.SetOverlayActive(false);
        }

        void ApplyHiddenImmediate()
        {
            KillMotion();
            ClearSuspendFlags();
            _wantVisible = false;
            _state = EnemyIntroducePanelState.Hidden;
            PlaceAt(panel, panelIn);
            HideTextVisual();
            SetPanelActive(false);
        }

        void KillMotion()
        {
            if (_moveTween != null && _moveTween.IsActive())
            {
                _moveTween.Kill();
            }

            _moveTween = null;

            if (panel != null)
            {
                panel.DOKill();
            }
        }

        void CacheStayPosition()
        {
            if (_stayCached || panel == null)
            {
                return;
            }

            // 场景里面板默认摆在 stay；in 点仅作入场待命。
            _stayPosition = panel.position;
            _stayCached = true;
        }

        string ResolveDefaultText()
        {
            if (!string.IsNullOrEmpty(defaultText))
            {
                return defaultText;
            }

            if (textMesh != null && !string.IsNullOrEmpty(textMesh.text))
            {
                return textMesh.text;
            }

            if (textAnimator != null && !string.IsNullOrEmpty(textAnimator.textFull))
            {
                return textAnimator.textFull;
            }

            return string.Empty;
        }

        void ResolveRefs()
        {
            if (ui == null)
            {
                ui = UiSystem.Instance != null ? UiSystem.Instance : GetComponent<UiSystem>();
            }

            if (panel == null)
            {
                panel = FindChildTransform("Enemy Info Panel")
                        ?? FindChildTransform("Enemy Introduce Panel");
            }

            if (panelIn == null)
            {
                panelIn = FindChildTransform("Enemy Info Panel in")
                          ?? FindChildTransform("Enemy Introduce Panel in");
            }

            if (textObject == null)
            {
                var textTransform = FindChildTransform("Enemy Info Text")
                                   ?? FindChildTransform("Enemy Introduce Text");
                textObject = textTransform != null ? textTransform.gameObject : null;
            }

            if (textObject != null)
            {
                if (textAnimator == null)
                {
                    textAnimator = textObject.GetComponent<TextAnimator_TMP>();
                }

                if (textMesh == null)
                {
                    textMesh = textObject.GetComponent<TextMeshProUGUI>();
                }
            }
        }

        Transform FindChildTransform(string trimmedName)
        {
            var transforms = GetComponentsInChildren<Transform>(true);
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

        void SetPanelActive(bool active)
        {
            if (panel != null)
            {
                panel.gameObject.SetActive(active);
            }
        }

        void SetTextActive(bool active)
        {
            if (textObject != null)
            {
                textObject.SetActive(active);
            }
        }

        static void PlaceAt(Transform target, Transform marker)
        {
            if (target == null || marker == null)
            {
                return;
            }

            target.position = marker.position;
        }
    }
}
