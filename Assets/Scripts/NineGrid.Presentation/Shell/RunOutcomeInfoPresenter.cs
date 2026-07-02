using DG.Tweening;
using NineGrid.Presentation.Visuals;
using UnityEngine;

namespace NineGrid.Presentation.Shell
{
    /// <summary>
    /// Victory/Defeat 简易 InfoPanel：监听 <see cref="ShellPresentationEvents.RunOutcomeAnnounced"/> 并定时隐藏。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunOutcomeInfoPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject infoPanelRoot;
        [SerializeField] private TableNineInfoTextView noticeTextView;
        [SerializeField] private TableNineTextOverlayGate overlayGate;
        [SerializeField, Min(0.5f)] private float autoHideDelay = 3f;
        [SerializeField] private string victoryText = "胜利";
        [SerializeField] private string defeatText = "失败";

        private Tween hideTween;
        private MainFlowFsm flowFsm;

        public void Bind(MainFlowFsm fsm)
        {
            flowFsm = fsm;
        }

        private void OnEnable()
        {
            ShellPresentationEvents.RunOutcomeAnnounced += OnRunOutcomeAnnounced;
        }

        private void OnDisable()
        {
            ShellPresentationEvents.RunOutcomeAnnounced -= OnRunOutcomeAnnounced;
            CancelHideTween();
        }

        private void OnRunOutcomeAnnounced(Evt_RunOutcomeAnnounced evt)
        {
            string message = evt.Outcome == RunOutcome.Victory ? victoryText : defeatText;
            ShowNotice(message);
            ScheduleAutoHide();
        }

        public void ShowNotice(string message)
        {
            if (infoPanelRoot != null)
            {
                infoPanelRoot.SetActive(true);
            }

            noticeTextView?.SetDescription(message);

            overlayGate?.ApplyForScreen(
                flowFsm != null && flowFsm.CurrentScreen == MainFlowScreen.Defeat
                    ? MainFlowScreen.Defeat
                    : MainFlowScreen.Victory);
        }

        public void Hide()
        {
            CancelHideTween();
            noticeTextView?.Clear();
            if (infoPanelRoot != null)
            {
                infoPanelRoot.SetActive(false);
            }
        }

        private void ScheduleAutoHide()
        {
            CancelHideTween();
            hideTween = DOVirtual.DelayedCall(autoHideDelay, () =>
            {
                Hide();
                flowFsm?.RequestTransition(MainFlowTransition.ReturnToMainMenu);
            });
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
