using DG.Tweening;
using UnityEngine;

namespace Dott.Sample
{
    public class TimelineTest : MonoBehaviour
    {
        [SerializeField] private DOTweenTimeline timeline;

        private DOTweenTimeline boundTimeline;
        private DOTweenCallback boundCallback;
        private Sequence activeSequence;
        private bool isLooping;

        private void Update()
        {
            if (timeline == null)
                return;

            if (timeline != boundTimeline)
                OnTimelineReferenceChanged();

            if (Input.GetKeyDown(KeyCode.Space))
                PlayOnce();

            if (Input.GetKeyDown(KeyCode.R))
                ToggleLoop();
        }

        private void OnTimelineReferenceChanged()
        {
            StopPlayback();
            BindToTimeline(timeline);
        }

        private void BindToTimeline(DOTweenTimeline target)
        {
            boundTimeline = target;
            BindCallback(target != null ? target.GetComponent<DOTweenCallback>() : null);
        }

        private void BindCallback(DOTweenCallback newCallback)
        {
            if (boundCallback == newCallback)
                return;

            if (boundCallback != null)
                boundCallback.onCallback.RemoveListener(OnCallbackTriggered);

            boundCallback = newCallback;

            if (boundCallback != null)
                boundCallback.onCallback.AddListener(OnCallbackTriggered);
        }

        private void PlayOnce()
        {
            if (timeline == null)
                return;

            isLooping = false;

            if (boundTimeline != timeline)
                BindToTimeline(timeline);

            StopCurrentSequence();
            activeSequence = timeline.Restart();
            if (activeSequence != null)
                activeSequence.SetLoops(1);
        }

        private void ToggleLoop()
        {
            if (timeline == null)
                return;

            if (isLooping)
            {
                StopPlayback();
                return;
            }

            if (boundTimeline != timeline)
                BindToTimeline(timeline);

            StopCurrentSequence();
            activeSequence = timeline.Restart();
            if (activeSequence == null)
                return;

            activeSequence.SetLoops(-1);
            isLooping = true;
        }

        private void StopPlayback()
        {
            isLooping = false;
            StopCurrentSequence();
        }

        private void StopCurrentSequence()
        {
            if (activeSequence != null && activeSequence.IsActive())
                activeSequence.Kill();

            activeSequence = null;
        }

        private void OnDestroy()
        {
            if (boundCallback != null)
                boundCallback.onCallback.RemoveListener(OnCallbackTriggered);
        }

        private static void OnCallbackTriggered()
        {
            Debug.Log("Callback triggered!");
        }
    }
}
