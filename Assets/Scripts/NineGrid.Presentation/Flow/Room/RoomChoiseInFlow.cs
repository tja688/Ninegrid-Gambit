using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NineGrid.Presentation.Contracts;
using UnityEngine;

namespace NineGrid.Presentation.Flow.Room
{
    /// <summary>
    /// 房间选择入场：两卡从 Start 缓动到就位锚点。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomChoiseInFlow : MonoBehaviour, IDirectedFlow
    {
        [Header("Timing")]
        [SerializeField, Min(0f)] private float staggerDelay = 0.08f;
        [SerializeField, Min(0.05f)] private float moveDuration = 0.55f;
        [SerializeField] private Ease moveEase = Ease.OutBack;
        [SerializeField, Min(0f)] private float overshoot = 1.1f;

        [Header("Playback")]
        [SerializeField] private bool ignoreTimeScale;
        [SerializeField] private bool deferPlayOneFrame = true;

        private Sequence activeSequence;
        private Coroutine deferCoroutine;
        private readonly List<Transform> activeCards = new();

        public bool IsPlaying { get; private set; }
        public float ExpectedDuration { get; private set; }

        public float ComputeTotalDuration(int cardCount)
        {
            if (cardCount <= 0)
            {
                return 0f;
            }

            return ((cardCount - 1) * staggerDelay) + moveDuration;
        }

        private void OnDisable()
        {
            StopAndRestore();
        }

        public void Play(
            Transform room1Card,
            Transform room1Start,
            Transform room1Home,
            Transform room2Card,
            Transform room2Start,
            Transform room2Home,
            Action onFinished = null)
        {
            var moves = new List<RoomMove>(2);
            if (room1Card != null)
            {
                moves.Add(new RoomMove(
                    room1Card,
                    room1Start,
                    room1Home != null ? room1Home.position : room1Card.position));
            }

            if (room2Card != null)
            {
                moves.Add(new RoomMove(
                    room2Card,
                    room2Start,
                    room2Home != null ? room2Home.position : room2Card.position));
            }

            Play(moves, onFinished);
        }

        public void Play(IReadOnlyList<RoomMove> moves, Action onFinished = null)
        {
            if (!isActiveAndEnabled || moves == null || moves.Count == 0)
            {
                onFinished?.Invoke();
                return;
            }

            StopPlaybackOnly();
            activeCards.Clear();

            for (var i = 0; i < moves.Count; i++)
            {
                RoomMove move = moves[i];
                if (move.Card == null)
                {
                    continue;
                }

                move.Card.position = move.Start != null ? move.Start.position : move.Card.position;
                activeCards.Add(move.Card);
            }

            if (activeCards.Count == 0)
            {
                onFinished?.Invoke();
                return;
            }

            ExpectedDuration = ComputeTotalDuration(activeCards.Count);
            activeSequence = BuildSequence(moves, onFinished);
            IsPlaying = true;

            if (deferPlayOneFrame)
            {
                deferCoroutine = StartCoroutine(PlayNextFrame(activeSequence));
            }
            else
            {
                activeSequence.Play();
            }
        }

        public void StopAndRestore()
        {
            StopPlaybackOnly();
            IsPlaying = false;
            ExpectedDuration = 0f;
            activeCards.Clear();
        }

        private Sequence BuildSequence(IReadOnlyList<RoomMove> moves, Action onFinished)
        {
            var sequence = DOTween.Sequence().SetTarget(this);
            if (ignoreTimeScale)
            {
                sequence.SetUpdate(true);
            }

            var index = 0;
            for (var i = 0; i < moves.Count; i++)
            {
                RoomMove move = moves[i];
                if (move.Card == null)
                {
                    continue;
                }

                float delay = index * staggerDelay;
                sequence.Insert(
                    delay,
                    move.Card.DOMove(move.HomeWorldPosition, moveDuration)
                        .SetEase(moveEase, overshoot));
                index++;
            }

            sequence.OnComplete(() =>
            {
                IsPlaying = false;
                onFinished?.Invoke();
            });

            return sequence;
        }

        public readonly struct RoomMove
        {
            public RoomMove(Transform card, Transform start, Vector3 homeWorldPosition)
            {
                Card = card;
                Start = start;
                HomeWorldPosition = homeWorldPosition;
            }

            public Transform Card { get; }
            public Transform Start { get; }
            public Vector3 HomeWorldPosition { get; }
        }

        private IEnumerator PlayNextFrame(Sequence sequence)
        {
            yield return null;
            deferCoroutine = null;
            if (sequence != null && sequence.IsActive())
            {
                sequence.Play();
            }
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
