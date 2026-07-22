using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace NineGrid.Cards
{
    [CreateAssetMenu(
        fileName = "CardTweenScaleOutEffect",
        menuName = "NineGrid/Cards/Effects/Tween Scale Out")]
    public sealed class CardTweenScaleOutEffectSO : CardEffectSO
    {
        [Tooltip("缩小消失时长（秒）。")]
        [SerializeField] private float duration = 0.18f;

        public override async UniTask PlayAsync(CardEffectPlayContext context)
        {
            var root = context.Root;
            if (root == null)
            {
                return;
            }

            var initialScale = root.localScale;
            var elapsed = 0f;
            var playDuration = duration > 0f ? duration : EstimatedDuration;

            while (elapsed < playDuration)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / playDuration);
                root.localScale = initialScale * (1f - EaseInBack(t));
                await UniTask.Yield(PlayerLoopTiming.Update, context.CancellationToken);
            }

            root.localScale = Vector3.zero;
        }

        private static float EaseInBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return c3 * t * t * t - c1 * t * t;
        }
    }
}
