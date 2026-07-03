using System;
using DG.Tweening;
using NineGrid.Presentation.Contracts;
using NineGrid.Presentation.Interaction;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace NineGrid.Presentation.Flow.Item
{
    /// <summary>
    /// 道具使用：确认脉冲后淡出；演员回收待 <see cref="Orchestration.StructuralFlowGaps"/> 补 despawn Flow。
    /// </summary>
    [DisallowMultipleComponent]
    [MovedFrom(true, "NineGrid.Presentation.Performance", null, "ItemUsePerformance")]
    public sealed class ItemUseFlow : MonoBehaviour, IDirectedFlow
    {
        [SerializeField] private HandCardReturnPresenter returnPresenter;
        [SerializeField, Min(0.01f)] private float consumeFadeDuration = 0.22f;
        [SerializeField] private Ease consumeEase = Ease.InQuad;

        private Transform mActiveActor;
        private Sequence mActiveSequence;
        private bool mIsPlaying;

        public bool IsPlaying => mIsPlaying;
        public float ExpectedDuration => (returnPresenter != null ? returnPresenter.ReturnDuration : 0.18f) + consumeFadeDuration;

        public void Configure(HandCardReturnPresenter presenter)
        {
            returnPresenter = presenter;
        }

        public void Play(Transform itemActor, Action onComplete = null)
        {
            if (!isActiveAndEnabled || itemActor == null)
            {
                onComplete?.Invoke();
                return;
            }

            StopAndRestore();
            mActiveActor = itemActor;
            mIsPlaying = true;

            if (returnPresenter == null)
            {
                PlayConsumeFade(onComplete);
                return;
            }

            returnPresenter.PlayConfirm(itemActor, () => PlayConsumeFade(onComplete));
        }

        public void StopAndRestore()
        {
            if (mActiveSequence != null && mActiveSequence.IsActive())
            {
                mActiveSequence.Kill();
            }

            mActiveSequence = null;
            mActiveActor = null;
            mIsPlaying = false;
        }

        private void PlayConsumeFade(Action onComplete)
        {
            if (mActiveActor == null)
            {
                mIsPlaying = false;
                onComplete?.Invoke();
                return;
            }

            SpriteRenderer renderer = mActiveActor.GetComponentInChildren<SpriteRenderer>();
            if (renderer == null)
            {
                mActiveActor.gameObject.SetActive(false);
                mIsPlaying = false;
                onComplete?.Invoke();
                return;
            }

            mActiveSequence = DOTween.Sequence().SetTarget(this);
            mActiveSequence.Join(
                DOTween.To(
                    () => renderer.color.a,
                    value =>
                    {
                        Color color = renderer.color;
                        color.a = value;
                        renderer.color = color;
                    },
                    0f,
                    consumeFadeDuration).SetEase(consumeEase));
            mActiveSequence.Join(mActiveActor.DOScale(mActiveActor.localScale * 0.85f, consumeFadeDuration).SetEase(consumeEase));
            mActiveSequence.OnComplete(() =>
            {
                if (mActiveActor != null)
                {
                    mActiveActor.gameObject.SetActive(false);
                }

                mActiveSequence = null;
                mActiveActor = null;
                mIsPlaying = false;
                onComplete?.Invoke();
            });
        }

        private void OnDisable()
        {
            StopAndRestore();
        }
    }
}
