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
        object _heldChannelOwner;
        NoticeChannel? _heldChannel;

        public bool IsShowing => _isShowing;
        public NoticeChannel ActiveChannel => _activeChannel;

        /// <summary>指定通道正被外部独占（如新手教程），其他来源不得写入。</summary>
        public bool IsChannelHeld(NoticeChannel channel) =>
            _heldChannel == channel && _heldChannelOwner != null;
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
            if (IsChannelHeld(channel))
            {
                return false;
            }

            return ApplyShow(channel, text, duration, source);
        }

        /// <summary>独占通道后由持有者强制写入（每帧刷新，压制锻造屏默认文案）。</summary>
        public bool ShowHeld(NoticeChannel channel, string text, object owner, float duration = 0f)
        {
            if (!ReferenceEquals(_heldChannelOwner, owner) || _heldChannel != channel)
            {
                return false;
            }

            if (_isShowing && _activeChannel == channel)
            {
                return ApplyHeldRefresh(channel, text);
            }

            return ApplyShow(channel, text, duration);
        }

        bool ApplyHeldRefresh(NoticeChannel channel, string text)
        {
            if (ui == null)
            {
                return false;
            }

            CacheAnimators();
            ui.SetOverlayActive(true);
            ui.SetNoticeTextActive(channel == NoticeChannel.Notice);
            ui.SetFactoryTextActive(channel == NoticeChannel.Factory);

            var animator = GetAnimator(channel);
            var textObject = ui.GetNoticeChannelObject(channel);

            if (animator != null)
            {
                animator.SetText(text ?? string.Empty, hideText: false);
                animator.SetVisibilityEntireText(true, canPlayEffects: false);
            }
            else if (textObject != null && textObject.TryGetComponent<TMP_Text>(out var tmp))
            {
                tmp.text = text ?? string.Empty;
            }
            else
            {
                return false;
            }

            return true;
        }

        public void AcquireChannel(NoticeChannel channel, object owner)
        {
            _heldChannel = channel;
            _heldChannelOwner = owner;
            CancelAutoHide();
        }

        public void ReleaseChannel(object owner)
        {
            if (ReferenceEquals(_heldChannelOwner, owner))
            {
                _heldChannelOwner = null;
                _heldChannel = null;
            }
        }

        bool ApplyShow(NoticeChannel channel, string text, float duration, NoticeMessage source = null)
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

        /// <summary>已在显示指定通道时原地刷新文案（用于锻造屏等实时数值）。</summary>
        public bool TryUpdateActiveText(NoticeChannel channel, string text)
        {
            if (IsChannelHeld(channel))
            {
                return false;
            }

            if (!_isShowing || _activeChannel != channel || ui == null)
            {
                return false;
            }

            CacheAnimators();
            var animator = GetAnimator(channel);
            var textObject = ui.GetNoticeChannelObject(channel);

            if (animator != null)
            {
                animator.SetText(text ?? string.Empty, hideText: false);
                animator.SetVisibilityEntireText(true, canPlayEffects: false);
            }
            else if (textObject != null && textObject.TryGetComponent<TMP_Text>(out var tmp))
            {
                tmp.text = text ?? string.Empty;
            }
            else
            {
                return false;
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

            ui.SetOverlayActive(false);

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
