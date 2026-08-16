using System;
using NineGrid.Core.Localization;
using NineGrid.Flow;
using NineGrid.Flow.BoardBriefTip;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NineGrid.Presentation.Ui
{
    /// <summary>
    /// 局内功能菜单 BugLogo：打开输入窗；上勾提交 Bug（合并 log → FNS），下勾提交意见。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlaytestFeedbackPanel : MonoBehaviour
    {
        public const string LogoName = "BugLogo";
        public const string FormName = "输入框窗口";
        public const string BugInputName = "遇到问题了？遇到 bug 了?";
        public const string SuggestionInputName = "你对游戏的意见、建议";
        public const string BugSubmitName = "导出log日志按钮";
        public const string SuggestionSubmitName = "提交意见按钮";
        public const string CloseButtonName = "关闭面板按钮";

        private const int FormSwallowSort = BattleUiDimmerOverlay.CloseHitSort + 20;
        private const int FormButtonSort = BattleUiDimmerOverlay.CloseHitSort + 30;

        private static PlaytestFeedbackPanel sInstance;
        private static SubmitHost sSubmitHost;

        private Transform mLogo;
        private GameObject mForm;
        private TMP_InputField mBugInput;
        private TMP_InputField mSuggestionInput;
        private bool mBusy;
        private bool mWired;

        public static bool IsFormOpen =>
            sInstance != null
            && sInstance.mForm != null
            && sInstance.mForm.activeSelf;

        public static void EnsureBound(Transform functionMenuRoot)
        {
            if (functionMenuRoot == null)
            {
                return;
            }

            var logo = functionMenuRoot.Find("功能模块/" + LogoName);
            if (logo == null)
            {
                return;
            }

            var panel = logo.GetComponent<PlaytestFeedbackPanel>();
            if (panel == null)
            {
                panel = logo.gameObject.AddComponent<PlaytestFeedbackPanel>();
            }

            panel.mLogo = logo;
            panel.WireIfNeeded();
        }

        public static bool TryHandleEscape()
        {
            if (!IsFormOpen)
            {
                return false;
            }

            sInstance.SetFormOpen(false);
            return true;
        }

        public static void CloseForm()
        {
            if (sInstance != null)
            {
                sInstance.SetFormOpen(false);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sInstance = null;
            sSubmitHost = null;
        }

        private void OnEnable()
        {
            sInstance = this;
        }

        private void OnDestroy()
        {
            if (sInstance == this)
            {
                sInstance = null;
            }
        }

        private void WireIfNeeded()
        {
            if (mWired || mLogo == null)
            {
                return;
            }

            sInstance = this;
            mForm = mLogo.Find(FormName)?.gameObject;
            if (mForm == null)
            {
                Debug.LogWarning("[PlaytestFeedback] 未找到「" + FormName + "」。");
                return;
            }

            EnsureWorldCanvas(mForm);
            mBugInput = FindInput(mForm.transform, BugInputName);
            mSuggestionInput = FindInput(mForm.transform, SuggestionInputName);
            ConfigureInput(mBugInput, L10n.Tr("playtest.bug_placeholder", "请具体描述你遇到的问题…"));
            ConfigureInput(mSuggestionInput, L10n.Tr("playtest.suggestion_placeholder", "请写下你的意见或建议…"));

            WireHit(mLogo, OpenForm, FormSwallowSort);
            WireHit(mForm.transform, null, FormSwallowSort);
            WireHit(FindNamed(mForm.transform, BugSubmitName), SubmitBug, FormButtonSort);
            WireHit(FindNamed(mForm.transform, SuggestionSubmitName), SubmitSuggestion, FormButtonSort);
            WireHit(FindNamed(mForm.transform, CloseButtonName), () => SetFormOpen(false), FormButtonSort);

            mForm.SetActive(false);
            mWired = true;
        }

        private void OpenForm()
        {
            SetFormOpen(true);
        }

        private void SetFormOpen(bool open)
        {
            if (mForm == null)
            {
                return;
            }

            if (open == mForm.activeSelf)
            {
                if (open)
                {
                    FocusBugInput();
                }

                return;
            }

            mForm.SetActive(open);
            InteractionAudioCues.Pulse(
                open ? InteractionAudioCues.UiConfirm : InteractionAudioCues.UiCancel,
                "PlaytestFeedbackPanel.SetFormOpen",
                open ? "playtest.form.open" : "playtest.form.close");
            if (open)
            {
                FocusBugInput();
            }
            else
            {
                mBugInput?.DeactivateInputField();
                mSuggestionInput?.DeactivateInputField();
            }
        }

        private void SubmitBug()
        {
            if (mBusy)
            {
                return;
            }

            var text = mBugInput != null ? mBugInput.text : string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                Notify(L10n.Tr("playtest.bug_empty", "请先填写遇到的问题再提交。"));
                InteractionAudioCues.Pulse(
                    InteractionAudioCues.UiPress,
                    "PlaytestFeedbackPanel.SubmitBug",
                    "playtest.bug.reject");
                return;
            }

            mBusy = true;
            Notify(L10n.Tr("playtest.submitting", "正在提交…"));
            var panel = this;
            EnsureSubmitHost().StartCoroutine(FnsPlaytestClient.SubmitBug(text, result =>
            {
                if (panel != null)
                {
                    panel.mBusy = false;
                    if (result.Success && panel.mBugInput != null)
                    {
                        panel.mBugInput.text = string.Empty;
                    }
                }

                Notify(result.Message);
                InteractionAudioCues.Pulse(
                    result.Success ? InteractionAudioCues.UiConfirm : InteractionAudioCues.UiPress,
                    "PlaytestFeedbackPanel.SubmitBug",
                    result.Success ? "playtest.bug.ok" : "playtest.bug.fail");
            }));
        }

        private void SubmitSuggestion()
        {
            if (mBusy)
            {
                return;
            }

            var text = mSuggestionInput != null ? mSuggestionInput.text : string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                Notify(L10n.Tr("playtest.suggestion_empty", "请先填写意见或建议再提交。"));
                InteractionAudioCues.Pulse(
                    InteractionAudioCues.UiPress,
                    "PlaytestFeedbackPanel.SubmitSuggestion",
                    "playtest.suggestion.reject");
                return;
            }

            mBusy = true;
            Notify(L10n.Tr("playtest.submitting", "正在提交…"));
            var panel = this;
            EnsureSubmitHost().StartCoroutine(FnsPlaytestClient.SubmitSuggestion(text, result =>
            {
                if (panel != null)
                {
                    panel.mBusy = false;
                    if (result.Success && panel.mSuggestionInput != null)
                    {
                        panel.mSuggestionInput.text = string.Empty;
                    }
                }

                Notify(result.Message);
                InteractionAudioCues.Pulse(
                    result.Success ? InteractionAudioCues.UiConfirm : InteractionAudioCues.UiPress,
                    "PlaytestFeedbackPanel.SubmitSuggestion",
                    result.Success ? "playtest.suggestion.ok" : "playtest.suggestion.fail");
            }));
        }

        private void FocusBugInput()
        {
            if (mBugInput != null && mBugInput.isActiveAndEnabled)
            {
                mBugInput.ActivateInputField();
            }
        }

        private static void Notify(string message)
        {
            var tip = BoardBriefTipPresenter.InstanceOrNull();
            if (tip != null)
            {
                tip.ShowNotice(message);
                return;
            }

            Debug.Log("[PlaytestFeedback] " + message);
        }

        private static void ConfigureInput(TMP_InputField field, string placeholder)
        {
            if (field == null)
            {
                return;
            }

            field.lineType = TMP_InputField.LineType.MultiLineNewline;
            if (field.placeholder is TMP_Text ph && !string.IsNullOrEmpty(placeholder))
            {
                ph.text = placeholder;
            }
        }

        private static void EnsureWorldCanvas(GameObject form)
        {
            var canvas = form.GetComponent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            canvas.renderMode = RenderMode.WorldSpace;
            if (canvas.worldCamera == null)
            {
                canvas.worldCamera = Camera.main;
            }

            if (form.GetComponent<GraphicRaycaster>() == null)
            {
                form.AddComponent<GraphicRaycaster>();
            }
        }

        private static SubmitHost EnsureSubmitHost()
        {
            if (sSubmitHost != null)
            {
                return sSubmitHost;
            }

            var go = new GameObject(nameof(PlaytestFeedbackPanel) + ".SubmitHost");
            DontDestroyOnLoad(go);
            sSubmitHost = go.AddComponent<SubmitHost>();
            return sSubmitHost;
        }

        private static void WireHit(Transform target, Action onClick, int hitSort)
        {
            if (target == null)
            {
                return;
            }

            var col = target.GetComponent<BoxCollider2D>();
            if (col == null)
            {
                col = target.gameObject.AddComponent<BoxCollider2D>();
            }

            var sr = target.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                var size = sr.drawMode == SpriteDrawMode.Simple
                    ? (Vector2)sr.sprite.bounds.size
                    : sr.size;
                col.size = onClick != null
                    ? new Vector2(size.x * 1.4f, size.y * 1.4f)
                    : size;
                col.offset = Vector2.zero;
            }
            else
            {
                var rt = target.GetComponent<RectTransform>();
                if (rt != null)
                {
                    var size = rt.rect.size;
                    if (size.x > 0.01f && size.y > 0.01f)
                    {
                        col.size = size;
                        col.offset = rt.rect.center;
                    }
                }
            }

            if (col.size.x < 0.2f)
            {
                col.size = new Vector2(Mathf.Max(0.45f, col.size.x), Mathf.Max(0.45f, col.size.y));
            }

            col.isTrigger = false;
            col.enabled = true;

            var button = target.GetComponent<WorldUiHitButton>();
            if (button == null)
            {
                button = target.gameObject.AddComponent<WorldUiHitButton>();
            }

            button.Configure(
                onClick,
                hitSort,
                PointerHitSurfacePriorities.Overlay,
                hoverScale: onClick != null ? 1.08f : 1f);
        }

        private static TMP_InputField FindInput(Transform root, string name)
        {
            var named = FindNamed(root, name);
            if (named != null)
            {
                var field = named.GetComponent<TMP_InputField>();
                if (field != null)
                {
                    return field;
                }
            }

            return root.GetComponentInChildren<TMP_InputField>(true);
        }

        private static Transform FindNamed(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindNamed(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private sealed class SubmitHost : MonoBehaviour
        {
        }
    }
}
