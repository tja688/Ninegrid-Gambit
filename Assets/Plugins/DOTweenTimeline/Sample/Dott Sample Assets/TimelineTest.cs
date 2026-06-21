using DG.Tweening;
using UnityEngine;

namespace Dott.Sample
{
    public class TimelineTest : MonoBehaviour
    {
        [SerializeField] private DOTweenTimeline timeline;
        [SerializeField] private DOTweenCallback callback;

        private Sequence activeSequence;
        private bool stopAfterCurrentLoop;

        private void Awake()
        {
            if (timeline == null)
                timeline = GetComponent<DOTweenTimeline>();

            if (callback == null && timeline != null)
                callback = timeline.GetComponent<DOTweenCallback>();
        }

        private void Start()
        {
            if (callback != null)
                callback.onCallback.AddListener(OnCallbackTriggered);
        }

        private void OnDestroy()
        {
            if (callback != null)
                callback.onCallback.RemoveListener(OnCallbackTriggered);
        }

        private void Update()
        {
            if (timeline == null)
                return;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                stopAfterCurrentLoop = false;
                activeSequence = BeginPlayback();
                if (activeSequence != null)
                    activeSequence.SetLoops(1);
            }

            // 按住第二帧起才切无缝循环，短按仍只播一遍
            if (Input.GetKey(KeyCode.Space) && !Input.GetKeyDown(KeyCode.Space))
                EnsureLoopWhileHeld();

            if (Input.GetKeyUp(KeyCode.Space))
            {
                stopAfterCurrentLoop = true;
                FinishAfterCurrentLoop();
            }
        }

        private Sequence BeginPlayback()
        {
            if (timeline.Sequence != null && timeline.Sequence.IsActive())
                return timeline.Restart();

            return timeline.Play();
        }

        private void EnsureLoopWhileHeld()
        {
            if (activeSequence == null || !activeSequence.IsActive() || stopAfterCurrentLoop)
                return;

            if (activeSequence.Loops() < 0)
                return;

            activeSequence.SetLoops(-1);
        }

        private void FinishAfterCurrentLoop()
        {
            if (activeSequence == null || !activeSequence.IsActive())
                return;

            if (activeSequence.Loops() < 0)
                activeSequence.SetLoops((int)activeSequence.CompletedLoops() + 1);
        }

        private void OnCallbackTriggered()
        {
            Debug.Log("Callback triggered!");
        }
    }
}
