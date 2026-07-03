using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.Orchestration;
using UnityEngine;

namespace NineGrid.Presentation.Interaction
{
    /// <summary>
    /// 手牌道具演员输入中继：spawn / 批末补齐 Collider + <see cref="HandCardInputRelay"/>。
    /// </summary>
    public static class HandInteractionActorWiring
    {
        public static void WireAllHandActors(
            TableNineViewRegistry viewRegistry,
            CoreViewSnapshot snapshot,
            InGameInteractionCoordinator coordinator = null)
        {
            if (viewRegistry == null || snapshot?.Deck == null)
            {
                return;
            }

            var actors = new List<Transform>();
            IReadOnlyList<int> itemUids = snapshot.Deck.ItemSlotUids;
            for (var i = 0; i < itemUids.Count; i++)
            {
                int uid = itemUids[i];
                if (uid <= 0)
                {
                    continue;
                }

                Transform actor = viewRegistry.ResolveActor(uid);
                if (actor == null)
                {
                    continue;
                }

                EnsureHandRelay(actor, uid);
                actors.Add(actor);
            }

            coordinator?.SetHandActors(actors);
        }

        public static void EnsureHandRelay(Transform actor, int uid)
        {
            if (actor == null || uid <= 0)
            {
                return;
            }

            TableNineActorBinding binding = actor.GetComponent<TableNineActorBinding>();
            if (binding == null)
            {
                binding = actor.gameObject.AddComponent<TableNineActorBinding>();
            }

            binding.Bind(uid, handItem: true);
            InteractionColliderUtility.EnsureCollider2D(actor.gameObject);

            BoardCardInputRelay boardRelay = actor.GetComponent<BoardCardInputRelay>();
            if (boardRelay != null)
            {
                Object.Destroy(boardRelay);
            }

            if (actor.GetComponent<HandCardInputRelay>() == null)
            {
                actor.gameObject.AddComponent<HandCardInputRelay>();
            }
        }
    }
}
