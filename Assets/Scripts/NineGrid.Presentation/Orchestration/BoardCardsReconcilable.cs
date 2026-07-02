using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.Interaction;
using NineGrid.Presentation.Visuals;
using UnityEngine;

namespace NineGrid.Presentation.Orchestration
{
    /// <summary>
    /// 批末对齐棋盘卡演员：按 <see cref="CoreViewSnapshot"/> 生成/回收/落位。
    /// </summary>
    public sealed class BoardCardsReconcilable : IReconcilable
    {
        private readonly TableNineViewRegistry mViewRegistry;
        private readonly TableNineActorFactory mActorFactory;

        public BoardCardsReconcilable(TableNineViewRegistry viewRegistry, TableNineActorFactory actorFactory)
        {
            mViewRegistry = viewRegistry;
            mActorFactory = actorFactory;
        }

        public void ApplySnapshot(CoreViewSnapshot snapshot)
        {
            if (snapshot?.Board == null || mViewRegistry == null || mActorFactory == null)
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
                EnsureBoardCard(slotView, snapshot.Cards);
            }

            mActorFactory.DespawnExcept(liveUids);
        }

        private void EnsureBoardCard(BoardSlotView slotView, IReadOnlyDictionary<int, CardView> cards)
        {
            int uid = slotView.CardUid;
            string defId = slotView.DefId;
            if (cards != null && cards.TryGetValue(uid, out CardView cardView))
            {
                defId = cardView.DefId;
            }

            Transform anchor = mViewRegistry.ResolveAnchor(slotView.Slot);
            Transform actor;
            if (!mActorFactory.TryGet(uid, out actor) || actor == null)
            {
                actor = mActorFactory.Spawn(defId, uid);
            }

            if (anchor != null)
            {
                TableNineActorFactory.PlaceAtAnchor(actor, anchor);
            }

            mViewRegistry.RegisterActor(uid, actor);
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
