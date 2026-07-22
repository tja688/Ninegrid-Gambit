using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace NineGrid.Cards
{
    [CreateAssetMenu(
        fileName = "CardDOTweenSequenceEffect",
        menuName = "NineGrid/Cards/Effects/DOTween Sequence")]
    public sealed class CardDOTweenSequenceEffectSO : CardEffectSO
    {
        [Tooltip("并行 Insert(0) 策略的 Tween 片段列表；数据完全内化，运行时无需 GO 组件。")]
        [SerializeField] private List<CardTweenClip> clips = new();

        public IReadOnlyList<CardTweenClip> Clips => clips;

        public override async UniTask PlayAsync(CardEffectPlayContext context)
        {
            var root = context.Root;
            if (root == null || clips == null || clips.Count == 0)
            {
                return;
            }

            CardDeckTween.KillMotion(root);
            var startLocalPosition = root.localPosition;
            var completed = false;
            var sequence = DOTween.Sequence()
                .SetLink(root.gameObject, LinkBehaviour.KillOnDestroy);

            for (var i = 0; i < clips.Count; i++)
            {
                var clipTween = BuildClipTween(root, clips[i], context.Invoke.SelfDirection);
                if (clipTween == null)
                {
                    continue;
                }

                sequence.Insert(0, clipTween.SetDelay(Mathf.Max(0f, clips[i].delay)));
            }

            sequence.OnComplete(() => completed = true);
            sequence.OnKill(() => completed = true);
            sequence.Play();

            try
            {
                await UniTask.WaitUntil(() => completed, cancellationToken: context.CancellationToken);
            }
            finally
            {
                if (root != null)
                {
                    root.localPosition = startLocalPosition;
                }
            }
        }

        public override void Stop(CardEffectPlayContext context)
        {
            base.Stop(context);
            if (context.Root != null)
            {
                CardDeckTween.KillMotion(context.Root);
            }
        }

        public static float ComputeEstimatedDuration(IReadOnlyList<CardTweenClip> source)
        {
            if (source == null || source.Count == 0)
            {
                return 0f;
            }

            var max = 0f;
            for (var i = 0; i < source.Count; i++)
            {
                max = Mathf.Max(max, source[i].FullDuration);
            }

            return max;
        }

        private static Tween BuildClipTween(Transform root, CardTweenClip clip, CardBoardDirection direction)
        {
            if (clip.duration <= 0f)
            {
                return null;
            }

            var endValue = CardDOTweenDirectionUtility.ApplyHorizontalSign(clip.endValue, direction);

            return clip.type switch
            {
                CardTweenClipType.LocalMove when clip.isRelative =>
                    root.DOLocalMove(endValue, clip.duration)
                        .SetRelative(true)
                        .SetEase(clip.ease),
                CardTweenClipType.LocalMove =>
                    root.DOLocalMove(endValue, clip.duration)
                        .SetEase(clip.ease),
                _ => null,
            };
        }

        private void OnValidate()
        {
            SetEstimatedDuration(ComputeEstimatedDuration(clips));
        }

#if UNITY_EDITOR
        public void EditorSetClips(IReadOnlyList<CardTweenClip> source)
        {
            clips = source != null ? new List<CardTweenClip>(source) : new List<CardTweenClip>();
            SetEstimatedDuration(ComputeEstimatedDuration(clips));
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
