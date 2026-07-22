using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace NineGrid.Cards
{
    [CreateAssetMenu(
        fileName = "CardTweenDirectionalPunchEffect",
        menuName = "NineGrid/Cards/Effects/Tween Directional Punch")]
    public sealed class CardTweenDirectionalPunchEffectSO : CardEffectSO
    {
        [Tooltip("Punch 强度（scale 维度）。")]
        [SerializeField] private float punchIntensity = 0.22f;

        [Tooltip("Punch 时长（秒）。")]
        [SerializeField] private float duration = 0.24f;

        [Tooltip("振动次数感（DOPunchScale vibrato）。")]
        [SerializeField] private int vibrato = 8;

        [Tooltip("按八向分配 punch 向量时的基准强度。")]
        [SerializeField] private float directionalWeight = 0.35f;

        public override async UniTask PlayAsync(CardEffectPlayContext context)
        {
            var root = context.Root;
            if (root == null)
            {
                return;
            }

            CardDeckTween.KillMotion(root);
            var punch = BuildPunchVector(context.Invoke.SelfDirection);
            var playDuration = duration > 0f ? duration : EstimatedDuration;

            var completed = false;
            var tween = root
                .DOPunchScale(punch, playDuration, vibrato, 0.5f)
                .SetLink(root.gameObject, LinkBehaviour.KillOnDestroy)
                .OnComplete(() => completed = true)
                .OnKill(() => completed = true);

            await UniTask.WaitUntil(() => completed, cancellationToken: context.CancellationToken);
            if (tween.IsActive())
            {
                tween.Kill();
            }
        }

        private Vector3 BuildPunchVector(CardBoardDirection direction)
        {
            var basePunch = Vector3.one * punchIntensity;
            if (direction == CardBoardDirection.None)
            {
                return basePunch;
            }

            var offset = CardBoardDirectionUtility.ToLocalOffset(direction, directionalWeight);
            return basePunch + new Vector3(offset.x, offset.y, 0f);
        }
    }
}
