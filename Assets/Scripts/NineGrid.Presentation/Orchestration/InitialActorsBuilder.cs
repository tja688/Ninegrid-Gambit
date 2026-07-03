using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.Interaction;
using NineGrid.Presentation.Visuals;
using UnityEngine;

namespace NineGrid.Presentation.Orchestration
{
    /// <summary>
    /// 开局一次性铺场：棋盘卡 spawn + 落位 + 初始数值（非批末兜底）。
    /// </summary>
    public static class InitialActorsBuilder
    {
        public static void BuildBoardCards(
            CoreViewSnapshot snapshot,
            TableNineViewRegistry viewRegistry,
            TableNineActorFactory actorFactory)
        {
            if (snapshot?.Board == null || viewRegistry == null || actorFactory == null)
            {
                return;
            }

            var liveUids = new HashSet<int>();
            IReadOnlyList<BoardSlotView> slots = snapshot.Board.Slots;
            for (var i = 0; i < slots.Count; i++)
            {
                BoardSlotView slotView = slots[i];
                if (slotView.CardUid <= 0)
                {
                    continue;
                }

                liveUids.Add(slotView.CardUid);
                EnsureBoardCard(slotView, snapshot.Cards, viewRegistry, actorFactory);
            }

            actorFactory.DespawnExcept(liveUids);
        }

        private static void EnsureBoardCard(
            BoardSlotView slotView,
            IReadOnlyDictionary<int, CardView> cards,
            TableNineViewRegistry viewRegistry,
            TableNineActorFactory actorFactory)
        {
            int uid = slotView.CardUid;
            string defId = slotView.DefId;
            if (cards != null && cards.TryGetValue(uid, out CardView cardView))
            {
                defId = cardView.DefId;
            }

            Transform anchor = viewRegistry.ResolveAnchor(slotView.Slot);
            Transform actor;
            if (!actorFactory.TryGet(uid, out actor) || actor == null)
            {
                actor = actorFactory.Spawn(defId, uid);
            }

            if (anchor != null)
            {
                TableNineActorFactory.PlaceAtAnchor(actor, anchor);
            }

            viewRegistry.RegisterActor(uid, actor);
            ApplyCardStatus(actor, slotView);
            EnsureBoardRelay(actor);
        }

        private static void EnsureBoardRelay(Transform actor)
        {
            if (actor == null)
            {
                return;
            }

            if (actor.GetComponent<BoardCardInputRelay>() == null)
            {
                actor.gameObject.AddComponent<BoardCardInputRelay>();
            }
        }

        private static void ApplyCardStatus(Transform actor, BoardSlotView slotView)
        {
            if (actor == null || slotView == null)
            {
                return;
            }

            TableNineCardStatusView view = actor.GetComponentInChildren<TableNineCardStatusView>(true);
            if (view == null)
            {
                return;
            }

            view.PlayAttackTo(slotView.EffectiveAttack, animate: false);
            view.PlayLifeTo(slotView.EffectiveHp, animate: false);
            view.PlayArmorTo(slotView.EffectiveArmor, animate: false);
        }
    }
}
