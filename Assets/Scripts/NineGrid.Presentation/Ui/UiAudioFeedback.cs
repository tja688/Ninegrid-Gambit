using NineGrid.Flow.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NineGrid.Presentation.Ui
{
    /// <summary>uGUI Selectable 的统一声音出口；控件只声明语义，素材与冷却由 AudioSystem 解析。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Selectable))]
    public sealed class UiAudioFeedback : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler
    {
        [SerializeField] private string contentId;
        [SerializeField] private string acceptedCueId;

        private Selectable mSelectable;

        public void Configure(string stableContentId, string acceptedCue)
        {
            contentId = stableContentId ?? string.Empty;
            acceptedCueId = acceptedCue ?? string.Empty;
            mSelectable = GetComponent<Selectable>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            InteractionAudioCues.Pulse(
                InteractionAudioCues.MainMenuHover,
                "UiAudioFeedback.OnPointerEnter",
                contentId);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            mSelectable ??= GetComponent<Selectable>();
            var cueId = mSelectable != null && mSelectable.IsInteractable()
                ? InteractionAudioCues.UiPress
                : InteractionAudioCues.UiReject;
            InteractionAudioCues.Pulse(cueId, "UiAudioFeedback.OnPointerDown", contentId);
        }

        public void PulseAccepted()
        {
            if (string.IsNullOrEmpty(acceptedCueId))
            {
                return;
            }

            InteractionAudioCues.Pulse(
                acceptedCueId,
                "UiAudioFeedback.PulseAccepted",
                contentId);
        }
    }
}
