using DG.Tweening;
using NineGrid.Core.Systems;
using NineGrid.Presentation.Visuals;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Shell
{
    /// <summary>
    /// 订阅 <see cref="Evt_ActionRejected"/>，在局内信息区显示轻提示。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActionRejectedNoticePresenter : MonoBehaviour
    {
        [SerializeField] private TableNineInfoTextView noticeTextView;
        [SerializeField, Min(0.5f)] private float displayDuration = 2f;

        private IUnRegister mUnregister;
        private Tween hideTween;

        public void Bind(IArchitecture architecture)
        {
            Unbind();
            EnsureNoticeTextView();
            if (architecture == null)
            {
                return;
            }

            mUnregister = architecture.RegisterEvent<Evt_ActionRejected>(OnActionRejected);
        }

        private void EnsureNoticeTextView()
        {
            if (noticeTextView != null)
            {
                return;
            }

            noticeTextView = FindObjectOfType<TableNineInfoTextView>();
        }

        public void Unbind()
        {
            mUnregister?.UnRegister();
            mUnregister = null;
            CancelHideTween();
            noticeTextView?.Clear();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void OnActionRejected(Evt_ActionRejected evt)
        {
            if (string.IsNullOrEmpty(evt.Reason))
            {
                return;
            }

            noticeTextView?.SetDescription(evt.Reason);
            ScheduleHide();
        }

        private void ScheduleHide()
        {
            CancelHideTween();
            hideTween = DOVirtual.DelayedCall(displayDuration, () => noticeTextView?.Clear());
        }

        private void CancelHideTween()
        {
            if (hideTween != null && hideTween.IsActive())
            {
                hideTween.Kill();
            }

            hideTween = null;
        }
    }
}
