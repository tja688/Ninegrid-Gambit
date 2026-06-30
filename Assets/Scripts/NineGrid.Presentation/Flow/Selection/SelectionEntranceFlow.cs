using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Presentation.Contracts;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;

namespace NineGrid.Presentation.Flow.Selection
{
    /// <summary>
    /// 选择层入场：选项卡从 scale 0 弹性弹出（BounceCards PlayEntryAnimation）。
    /// </summary>
    [DisallowMultipleComponent]
    [MovedFrom(true, "NineGrid.Presentation.Performance", null, "SelectionOverlayEntrancePerformance")]
    public sealed class SelectionEntranceFlow : MonoBehaviour, IDirectedFlow
    {
        [Header("Timing")]
        [SerializeField, Min(0f)] private float entryDelay = 0.42f;
        [SerializeField, Min(0f)] private float entryStagger = 0.08f;
        [SerializeField, Min(0.1f)] private float entryDuration = 0.7f;
        [SerializeField] private Ease entryEase = Ease.OutElastic;

        [Header("Playback")]
        [SerializeField] private bool ignoreTimeScale;
        [SerializeField] private bool deferPlayOneFrame = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onComplete;

        private readonly List<Transform> activeOptions = new();
        private Sequence activeSequence;
        private Coroutine deferCoroutine;

        public bool IsPlaying { get; private set; }
        public float ExpectedDuration { get; private set; }

        public float ComputeTotalDuration(int optionCount)
        {
            if (optionCount <= 0)
            {
                return 0f;
            }

            return entryDelay + ((optionCount - 1) * entryStagger) + entryDuration;
        }

        private void OnDisable()
        {
            StopAndRestore();
        }

        private void OnValidate()
        {
            entryDelay = Mathf.Max(0f, entryDelay);
            entryStagger = Mathf.Max(0f, entryStagger);
            entryDuration = Mathf.Max(0.1f, entryDuration);
        }

        public void Play(IReadOnlyList<Transform> options, Action onFinished = null)
        {
            if (!isActiveAndEnabled || options == null || options.Count == 0)
            {
                onFinished?.Invoke();
                return;
            }

            StopPlaybackOnly();
            activeOptions.Clear();

            for (var i = 0; i < options.Count; i++)
            {
                Transform option = options[i];
                if (option == null)
                {
                    continue;
                }

                option.localScale = Vector3.zero;
                activeOptions.Add(option);
            }

            if (activeOptions.Count == 0)
            {
                onFinished?.Invoke();
                return;
            }

            ExpectedDuration = ComputeTotalDuration(activeOptions.Count);
            activeSequence = BuildSequence(onFinished);
            IsPlaying = true;

            if (deferPlayOneFrame)
            {
                deferCoroutine = StartCoroutine(PlayNextFrame(activeSequence));
            }
            else
            {
                StartSequence(activeSequence);
            }
        }

        [ContextMenu("Stop And Restore")]
        public void StopAndRestore()
        {
            StopPlaybackOnly();
            for (var i = 0; i < activeOptions.Count; i++)
            {
                Transform option = activeOptions[i];
                if (option == null)
                {
                    continue;
                }

                option.localScale = Vector3.one;
            }

            activeOptions.Clear();
            IsPlaying = false;
            ExpectedDuration = 0f;
        }

        private Sequence BuildSequence(Action onFinished)
        {
            var sequence = DOTween.Sequence().SetTarget(this);
            if (ignoreTimeScale)
            {
                sequence.SetUpdate(true);
            }

            for (var i = 0; i < activeOptions.Count; i++)
            {
                Transform option = activeOptions[i];
                if (option == null)
                {
                    continue;
                }

                float delay = entryDelay + (i * entryStagger);
                sequence.Insert(
                    delay,
                    option.DOScale(Vector3.one, entryDuration).SetEase(entryEase));
            }

            sequence.OnComplete(() =>
            {
                IsPlaying = false;
                onComplete?.Invoke();
                onFinished?.Invoke();
            });

            return sequence;
        }

        private IEnumerator PlayNextFrame(Sequence sequence)
        {
            yield return null;
            deferCoroutine = null;
            if (sequence == null || !sequence.IsActive())
            {
                yield break;
            }

            StartSequence(sequence);
        }

        private void StartSequence(Sequence sequence)
        {
            if (sequence == null)
            {
                return;
            }

            sequence.Play();
        }

        private void StopPlaybackOnly()
        {
            if (deferCoroutine != null)
            {
                StopCoroutine(deferCoroutine);
                deferCoroutine = null;
            }

            if (activeSequence != null && activeSequence.IsActive())
            {
                activeSequence.Kill(false);
            }

            activeSequence = null;
            DOTween.Kill(this);
        }
    }
}
