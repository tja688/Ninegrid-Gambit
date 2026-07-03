using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.Orchestration;
using UnityEngine;

namespace NineGrid.Presentation.Interaction
{
    /// <summary>
    /// 棋盘演员输入中继：Flow/ActorFactory spawn 后补齐 Collider + <see cref="BoardCardInputRelay"/>。
    /// 取代已删除的批末 <c>BoardCardsReconcilable</c> 兜底。
    /// </summary>
    public static class BoardInteractionActorWiring
    {
        public static void WireAllBoardActors(TableNineViewRegistry viewRegistry, CoreViewSnapshot snapshot)
        {
            if (viewRegistry == null || snapshot?.Board == null)
            {
                return;
            }

            IReadOnlyList<BoardSlotView> slots = snapshot.Board.Slots;
            for (var i = 0; i < slots.Count; i++)
            {
                BoardSlotView slotView = slots[i];
                if (slotView.CardUid <= 0)
                {
                    continue;
                }

                Transform actor = viewRegistry.ResolveActor(slotView.CardUid);
                EnsureBoardRelay(actor);
            }
        }

        public static void EnsureBoardRelay(Transform actor)
        {
            if (actor == null)
            {
                return;
            }

            TableNineActorBinding binding = actor.GetComponent<TableNineActorBinding>();
            if (binding != null && binding.IsHandItem)
            {
                return;
            }

            InteractionColliderUtility.EnsureCollider2D(actor.gameObject);

            if (actor.GetComponent<BoardCardInputRelay>() == null)
            {
                actor.gameObject.AddComponent<BoardCardInputRelay>();
            }
        }
    }
}
