using System;
using System.Collections.Generic;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using TMPro;
using UnityEngine;

namespace NineGrid.Presentation.Ui
{
    /// <summary>
    /// 通用确认提示框：绑定场景预置「提示框」节点（多条提示文案 TMP 子节点 +
    /// 确认 <c>F_UI_MenuIcons_A4</c> / 取消 <c>F_UI_MenuIcons_A3</c> 图标按钮）。
    /// 打开时全屏阻塞其余点击（模态），确认执行回调，取消 / Esc 关闭返回。
    /// 服务：覆盖存档、退出游戏、回到主菜单等危险操作二次确认。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiConfirmPrompt : MonoBehaviour
    {
        public const string NodeName = "提示框";
        public const string OverwriteSaveMessage = "覆盖存档的提示";
        public const string QuitGameMessage = "退出游戏提示";
        public const string ReturnMainMenuMessage = "回到主菜单提示";

        private const string ConfirmNodeName = "F_UI_MenuIcons_A4";
        private const string CancelNodeName = "F_UI_MenuIcons_A3";

        // 模态阻塞：压过作弊面板(10000)/手牌/场地等一切既有命中排序。
        private const int BlockerHitSort = 20000;
        private const int ButtonHitSort = 20010;

        private static readonly List<UiConfirmPrompt> sOpen = new List<UiConfirmPrompt>(2);
        private static int sEscapeHandledFrame = -1;

        private bool mWired;
        private readonly List<TMP_Text> mMessages = new List<TMP_Text>(4);
        private Action mOnConfirm;
        private Action mOnCancel;
        private string mContentId = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sOpen.Clear();
            sEscapeHandledFrame = -1;
        }

        /// <summary>任意提示框开着（面板 Esc / 点击处理须让位）。</summary>
        public static bool IsAnyOpen
        {
            get
            {
                for (var i = sOpen.Count - 1; i >= 0; i--)
                {
                    if (sOpen[i] == null || !sOpen[i].isActiveAndEnabled)
                    {
                        sOpen.RemoveAt(i);
                    }
                }

                return sOpen.Count > 0;
            }
        }

        /// <summary>本帧 Esc 已被提示框消费（宿主面板勿再响应同一次按键）。</summary>
        public static bool EscapeHandledThisFrame => sEscapeHandledFrame == Time.frameCount;

        /// <summary>把提示框组件挂到场景预置节点；节点缺失返回 null（调用方回退直接执行）。</summary>
        public static UiConfirmPrompt Attach(Transform promptRoot)
        {
            if (promptRoot == null)
            {
                return null;
            }

            var prompt = promptRoot.GetComponent<UiConfirmPrompt>();
            if (prompt == null)
            {
                prompt = promptRoot.gameObject.AddComponent<UiConfirmPrompt>();
            }

            prompt.EnsureWired();
            if (!prompt.IsOpen && promptRoot.gameObject.activeSelf)
            {
                promptRoot.gameObject.SetActive(false);
            }

            return prompt;
        }

        public bool IsOpen => sOpen.Contains(this);

        /// <summary>
        /// 打开提示框：只显示 <paramref name="messageName"/> 对应文案子节点，
        /// 确认执行 <paramref name="onConfirm"/>，取消 / Esc 执行 <paramref name="onCancel"/>。
        /// </summary>
        public void Show(string messageName, Action onConfirm, Action onCancel = null)
        {
            EnsureWired();
            mOnConfirm = onConfirm;
            mOnCancel = onCancel;
            mContentId = "confirm_prompt." + (messageName ?? string.Empty);

            for (var i = 0; i < mMessages.Count; i++)
            {
                var text = mMessages[i];
                if (text == null)
                {
                    continue;
                }

                text.gameObject.SetActive(string.Equals(
                    text.gameObject.name,
                    messageName,
                    StringComparison.Ordinal));
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (!sOpen.Contains(this))
            {
                sOpen.Add(this);
            }

            InteractionAudioCues.Pulse(
                InteractionAudioCues.UiPress,
                "UiConfirmPrompt.Show",
                mContentId);
        }

        public void Cancel()
        {
            if (!IsOpen)
            {
                return;
            }

            InteractionAudioCues.Pulse(
                InteractionAudioCues.UiCancel,
                "UiConfirmPrompt.Cancel",
                mContentId);
            var onCancel = mOnCancel;
            HideInternal();
            onCancel?.Invoke();
        }

        private void Confirm()
        {
            if (!IsOpen)
            {
                return;
            }

            InteractionAudioCues.Pulse(
                InteractionAudioCues.UiConfirm,
                "UiConfirmPrompt.Confirm",
                mContentId);
            var onConfirm = mOnConfirm;
            HideInternal();
            onConfirm?.Invoke();
        }

        private void HideInternal()
        {
            mOnConfirm = null;
            mOnCancel = null;
            sOpen.Remove(this);
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (!IsOpen || sOpen.Count == 0 || sOpen[sOpen.Count - 1] != this)
            {
                return;
            }

            if (KeyboardUtility.GetKeyDown(KeyCode.Escape))
            {
                sEscapeHandledFrame = Time.frameCount;
                Cancel();
            }
        }

        private void OnDisable()
        {
            // 随宿主面板整体关闭：清回调并出栈，避免僵尸模态挡输入。
            mOnConfirm = null;
            mOnCancel = null;
            sOpen.Remove(this);
        }

        private void EnsureWired()
        {
            if (mWired)
            {
                return;
            }

            mMessages.Clear();
            Transform confirmNode = null;
            Transform cancelNode = null;
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child == null || child.name.StartsWith("__", StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(child.name, ConfirmNodeName, StringComparison.Ordinal))
                {
                    confirmNode = child;
                    continue;
                }

                if (string.Equals(child.name, CancelNodeName, StringComparison.Ordinal))
                {
                    cancelNode = child;
                    continue;
                }

                var text = child.GetComponent<TMP_Text>();
                if (text != null)
                {
                    mMessages.Add(text);
                    child.gameObject.SetActive(false);
                }
            }

            WireButton(confirmNode, Confirm);
            WireButton(cancelNode, Cancel);
            WireBlocker();

            if (confirmNode == null || cancelNode == null || mMessages.Count == 0)
            {
                Debug.LogWarning(
                    "[UiConfirmPrompt] 提示框结构不完整（文案/确认/取消缺失）：" + name);
            }

            mWired = true;
        }

        private void WireButton(Transform target, Action onClick)
        {
            if (target == null)
            {
                return;
            }

            var collider = target.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = target.gameObject.AddComponent<BoxCollider2D>();
            }

            var renderer = target.GetComponent<SpriteRenderer>();
            var size = renderer != null && renderer.sprite != null
                ? (renderer.drawMode == SpriteDrawMode.Simple
                    ? (Vector2)renderer.sprite.bounds.size
                    : renderer.size)
                : new Vector2(0.5f, 0.5f);
            // 图标偏小，命中盒加一点余量方便点。
            collider.size = new Vector2(size.x * 1.4f, size.y * 1.4f);
            collider.offset = Vector2.zero;
            collider.isTrigger = false;
            collider.enabled = true;

            var button = target.GetComponent<WorldUiHitButton>();
            if (button == null)
            {
                button = target.gameObject.AddComponent<WorldUiHitButton>();
            }

            button.Configure(
                onClick,
                ButtonHitSort,
                PointerHitSurfacePriorities.Overlay,
                hoverScale: 1.12f,
                onHoverEnter: () => InteractionAudioCues.Pulse(
                    InteractionAudioCues.MainMenuHover,
                    "UiConfirmPrompt.ButtonHover",
                    mContentId));
        }

        private void WireBlocker()
        {
            var collider = GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<BoxCollider2D>();
            }

            // 全屏模态阻塞：按世界 60x40 折算到本地，盖满可视区。
            var lossy = transform.lossyScale;
            collider.size = new Vector2(
                60f / Mathf.Max(0.01f, Mathf.Abs(lossy.x)),
                40f / Mathf.Max(0.01f, Mathf.Abs(lossy.y)));
            collider.offset = Vector2.zero;
            collider.isTrigger = false;
            collider.enabled = true;

            var blocker = GetComponent<WorldUiHitButton>();
            if (blocker == null)
            {
                blocker = gameObject.AddComponent<WorldUiHitButton>();
            }

            // onClick 为空 = 纯吞点击。
            blocker.Configure(null, BlockerHitSort, PointerHitSurfacePriorities.Overlay);
        }
    }
}
