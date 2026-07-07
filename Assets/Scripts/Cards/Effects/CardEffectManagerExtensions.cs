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
    }
}
