using System.Threading;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 效果 SO 播放时的运行时上下文，由 CardEffectManager 组装后传入。
    /// </summary>
    public readonly struct CardEffectPlayContext
    {
        public CardEffectPlayContext(
            ManagedCard card,
            Transform root,
            StandardCardView view,
            CardEffectInvokeContext invoke,
            Vector3 selfWorldPosition,
            Vector3? otherWorldPosition,
            CancellationToken cancellationToken)
        {
            Card = card;
            Root = root;
            View = view;
            Invoke = invoke;
            SelfWorldPosition = selfWorldPosition;
            OtherWorldPosition = otherWorldPosition;
            CancellationToken = cancellationToken;
        }

        public ManagedCard Card { get; }

        public Transform Root { get; }

        public StandardCardView View { get; }

        public CardEffectInvokeContext Invoke { get; }

        public Vector3 SelfWorldPosition { get; }

        public Vector3? OtherWorldPosition { get; }

        public CancellationToken CancellationToken { get; }
    }
}
