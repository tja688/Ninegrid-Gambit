using System;
using DG.Tweening;
using NineGrid.Presentation.Contracts;
using UnityEngine;

namespace NineGrid.Presentation.Flow.Shell
{
    /// <summary>
    /// 局内 HUD 入离场：淡入淡出根节点，配合 <see cref="Visuals.InGameHudPhasePolicy"/> 阶段切换。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InGameUiFlow : MonoBehaviour, IDirectedFlow
    {
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.22f;
        [SerializeField] private Ease fadeEase = Ease.OutQuad;

        private GameObject[] mHudRoots;
        private CanvasGroup[] mCanvasGroups;
        private Sequence mActiveSequence;
        private bool mIsPlaying;

        public bool IsPlaying => mIsPlaying;
        public float ExpectedDuration => fadeDuration;

        public void Configure(GameObject[] hudRoots)
        {
            mHudRoots = hudRoots;
            mCanvasGroups = null;
        }

        public void ApplyImmediateVisibility(bool visible)
        {
            EnsureCanvasGroups();
            for (var i = 0; i < mCanvasGroups.Length; i++)
            {
                CanvasGroup group = mCanvasGroups[i];
                if (group == null)
                {
                    continue;
                }

                group.alpha = visible ? 1f : 0f;
                group.gameObject.SetActive(visible);
                group.interactable = visible;
                group.blocksRaycasts = visible;
            }
        }

        public void PlayEntrance(Action onComplete = null)
        {
            PlayFade(show: true, onComplete);
        }

        public void PlayExit(Action onComplete = null)
        {
            PlayFade(show: false, onComplete);
        }

        public void StopAndRestore()
        {
            if (mActiveSequence != null && mActiveSequence.IsActive())
            {
                mActiveSequence.Kill();
            }

            mActiveSequence = null;
            mIsPlaying = false;
        }

        private void PlayFade(bool show, Action onComplete)
        {
            if (!isActiveAndEnabled)
            {
                onComplete?.Invoke();
                return;
            }

            StopAndRestore();
            EnsureCanvasGroups();
            if (mCanvasGroups == null || mCanvasGroups.Length == 0)
            {
                onComplete?.Invoke();
                return;
            }

            if (show)
            {
                for (var i = 0; i < mCanvasGroups.Length; i++)
                {
                    CanvasGroup group = mCanvasGroups[i];
                    if (group == null)
                    {
                        continue;
                    }

                    group.gameObject.SetActive(true);
                    group.alpha = 0f;
                    group.interactable = false;
                    group.blocksRaycasts = false;
                }
            }

            mActiveSequence = DOTween.Sequence().SetTarget(this);
            for (var i = 0; i < mCanvasGroups.Length; i++)
            {
                CanvasGroup group = mCanvasGroups[i];
                if (group == null)
                {
                    continue;
                }

                float target = show ? 1f : 0f;
                Tween tween = DOTween.To(() => group.alpha, value => group.alpha = value, target, fadeDuration)
                    .SetEase(fadeEase)
                    .SetTarget(group);
                mActiveSequence.Join(tween);
            }

            mIsPlaying = true;
            mActiveSequence.OnComplete(() =>
            {
                mIsPlaying = false;
                mActiveSequence = null;

                for (var i = 0; i < mCanvasGroups.Length; i++)
                {
                    CanvasGroup group = mCanvasGroups[i];
                    if (group == null)
                    {
                        continue;
                    }

                    if (!show)
                    {
                        group.gameObject.SetActive(false);
                    }
                    else
                    {
                        group.interactable = true;
                        group.blocksRaycasts = true;
                    }
                }

                onComplete?.Invoke();
            });
        }

        private void EnsureCanvasGroups()
        {
            if (mCanvasGroups != null && mCanvasGroups.Length == mHudRoots?.Length)
            {
                return;
            }

            if (mHudRoots == null || mHudRoots.Length == 0)
            {
                mCanvasGroups = Array.Empty<CanvasGroup>();
                return;
            }

            mCanvasGroups = new CanvasGroup[mHudRoots.Length];
            for (var i = 0; i < mHudRoots.Length; i++)
            {
                GameObject root = mHudRoots[i];
                if (root == null)
                {
                    continue;
                }

                CanvasGroup group = root.GetComponent<CanvasGroup>();
                if (group == null)
                {
                    group = root.AddComponent<CanvasGroup>();
                }

                mCanvasGroups[i] = group;
            }
        }

        private void OnDisable()
        {
            StopAndRestore();
        }
    }
}
