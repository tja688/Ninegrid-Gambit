using System;
using System.Collections;
using Febucci.TextAnimatorForUnity.TextMeshPro;
using TMPro;
using UnityEngine;

namespace NineGrid.UI
{
    /// <summary>
    /// 通知 / 提示文字系统：按 NoticeChannel 路由到 NoticeText 或 FactoryText。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NoticeSystem : MonoBehaviour
    {
        [SerializeField] NoticePool pool;
        [SerializeField] UiSystem ui;
        [SerializeField] TextAnimator_TMP noticeAnimator;
        [SerializeField] TextAnimator_TMP factoryAnimator;

        Coroutine _hideRoutine;
        NoticeChannel _activeChannel;
        bool _isShowing;

        public bool IsShowing => _isShowing;
        public NoticeChannel ActiveChannel => _activeChannel;
        public event Action<NoticeMessage> NoticeShown;
        public event Action NoticeHidden;

        void Awake()
        {
            if (ui == null)
            {
                ui = GetComponent<UiSystem>();
            }

            CacheAnimators();
        }

        void OnDestroy()
        {
            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
            }
        }

        public bool Show(string messageId)
        {
            if (pool == null || !pool.TryGet(messageId, out var message))
            {
                Debug.LogWarning($"[Notice] 消息池中找不到 id={messageId}");
                return false;
            }

            return Show(message);
        }

        public bool Show(NoticeMessage message)
        {
            if (message == null || ui == null)
            {
                return false;
            }

            return Show(message.Channel, message.Text, message.Duration, message);
        }

        public bool Show(NoticeChannel channel, string text, float duration = 2f, NoticeMessage source = null)
        {
            if (ui == null)
            {
                return false;
            }

            CacheAnimators();
            CancelAutoHide();

            _activeChannel = channel;
            _isShowing = true;

            ui.SetOverlayActive(true);
            ui.SetNoticeTextActive(channel == NoticeChannel.Notice);
            ui.SetFactoryTextActive(channel == NoticeChannel.Factory);

            var animator = GetAnimator(channel);
            var textObject = ui.GetNoticeChannelObject(channel);

            if (animator != null)
            {
                animator.SetText(text ?? string.Empty, hideText: true);
                animator.SetVisibilityEntireText(true, canPlayEffects: true);
            }
            else if (textObject != null && textObject.TryGetComponent<TMP_Text>(out var tmp))
            {
                tmp.text = text ?? string.Empty;
            }

            if (source != null)
            {
                NoticeShown?.Invoke(source);
            }

            if (duration > 0f)
            {
                _hideRoutine = StartCoroutine(HideAfter(duration));
            }

            return true;
        }

        public void Hide()
        {
            CancelAutoHide();
            if (!_isShowing || ui == null)
            {
                _isShowing = false;
                return;
            }

            var animator = GetAnimator(_activeChannel);
            if (animator != null)
            {
                // 无出场效果，直接清空。
                animator.SetText(string.Empty);
            }

            ui.SetNoticeTextActive(false);
            ui.SetFactoryTextActive(false);

            var dialogueOpen = ui.Dialogue != null && ui.Dialogue.IsOpen;
            if (!dialogueOpen)
            {
                ui.SetOverlayActive(false);
            }

            _isShowing = false;
            NoticeHidden?.Invoke();
        }

        TextAnimator_TMP GetAnimator(NoticeChannel channel)
        {
            return channel == NoticeChannel.Factory ? factoryAnimator : noticeAnimator;
        }

        void CacheAnimators()
        {
            if (ui == null)
            {
                return;
            }

            if (noticeAnimator == null && ui.NoticeTextObject != null)
            {
                noticeAnimator = ui.NoticeTextObject.GetComponent<TextAnimator_TMP>();
            }

            if (factoryAnimator == null && ui.FactoryTextObject != null)
            {
                factoryAnimator = ui.FactoryTextObject.GetComponent<TextAnimator_TMP>();
            }
        }

        void CancelAutoHide()
        {
            if (_hideRoutine == null)
            {
                return;
            }

            StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }

        IEnumerator HideAfter(float duration)
        {
            yield return new WaitForSecondsRealtime(duration);
            _hideRoutine = null;
            Hide();
        }
    }
}
