using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NineGrid.Cards
{
    public static class CardEffectManagerExtensions
    {
        public static bool TryGetEffectManager(this ManagedCard card, out CardEffectManager manager)
        {
            manager = null;
            if (card?.View == null)
            {
                return false;
            }

            manager = card.View.GetComponent<CardEffectManager>();
            return manager != null;
        }

        public static async UniTask<bool> TryPlayAsync(
            this ManagedCard card,
            CardEffectInvokeContext invoke,
            CancellationToken cancellationToken = default)
        {
            if (!card.TryGetEffectManager(out var manager))
            {
                return false;
            }

            await manager.PlayAsync(invoke, cancellationToken);
            return true;
        }

        public static async UniTask<bool> TryPlayHitFlashAsync(
            this ManagedCard card,
            CardBoardDirection selfDirection = CardBoardDirection.None,
            int selfSlot = 0,
            int? otherSlot = null,
            CancellationToken cancellationToken = default)
        {
            return await card.TryPlayAsync(
                CardEffectInvokeContext.ForHitFlash(selfDirection, selfSlot, otherSlot),
                cancellationToken);
        }
    }
}
