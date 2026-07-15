using Cysharp.Threading.Tasks;
using DG.Tweening;
using NineGrid.Cards.Convergence;
using UnityEngine;

namespace NineGrid.Cards
{
    [CreateAssetMenu(
        fileName = "CardTweenDirectionalLungeEffect",
        menuName = "NineGrid/Cards/Effects/Tween Directional Lunge")]
    public sealed class CardTweenDirectionalLungeEffectSO : CardEffectSO
    {
        [Tooltip("冲刺距离（世界单位）。")]
        [SerializeField] private float lungeDistance = 0.18f;

        [Tooltip("单程时长（秒）；往返总时长约为两倍。")]
        [SerializeField] private float halfDuration = 0.1f;

        public override async UniTask PlayAsync(CardEffectPlayContext context)
        {
            var root = context.Root;
            if (root == null)
            {
                return;
            }

            var direction = context.Invoke.SelfDirection;
            if (direction == CardBoardDirection.None)
            {
                direction = CardBoardDirection.Up;
            }

            var offset = CardBoardDirectionUtility.ToLocalOffset(direction, lungeDistance);
            var duration = halfDuration > 0f ? halfDuration : EstimatedDuration * 0.5f;

            if (await EffectFrameConvergence.TryPlayOutAndBackAsync(
                    root,
                    offset,
                    duration,
                    context.CancellationToken))
            {
                return;
            }

            // 无塔兜底：旧 DOMove 往返（净土/未挂塔对象）。
            CardDeckTween.KillMotion(root);
            var start = root.position;
            var target = start + offset;

            var completed = false;
            var sequence = DOTween.Sequence()
                .SetLink(root.gameObject, LinkBehaviour.KillOnDestroy);
            sequence.Append(root.DOMove(target, duration).SetEase(Ease.OutQuad));
            sequence.Append(root.DOMove(start, duration).SetEase(Ease.InQuad));
            sequence.OnComplete(() => completed = true);
            sequence.OnKill(() => completed = true);

            await UniTask.WaitUntil(() => completed, cancellationToken: context.CancellationToken);
        }

        public override void Stop(CardEffectPlayContext context)
        {
            base.Stop(context);
            EffectFrameConvergence.SnapHome(context.Root, "Lunge.Stop");
        }
    }
}
