using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.Interaction;
using NineGrid.Presentation.Shared;
using UnityEngine;

namespace NineGrid.Presentation.Orchestration
{
    /// <summary>
    /// 手牌道具演员：一次性 spawn + 扇形布局 + 输入中继注册（非批末兜底）。
    /// </summary>
    public sealed class HandItemsPresenter
    {
        private readonly TableNineViewRegistry mViewRegistry;
        private readonly TableNineActorFactory mActorFactory;
        private readonly Transform mHandActorsRoot;
        private readonly HandLayoutPresenter mLayoutPresenter;
        private readonly HandCardLayoutSolver mLayoutSolver;
        private InGameInteractionCoordinator mCoordinator;

        public HandItemsPresenter(
            TableNineViewRegistry viewRegistry,
            TableNineActorFactory actorFactory,
            Transform handActorsRoot,
            HandLayoutPresenter layoutPresenter,
            HandCardLayoutSolver layoutSolver)
        {
            mViewRegistry = viewRegistry;
            mActorFactory = actorFactory;
            mHandActorsRoot = handActorsRoot;
            mLayoutPresenter = layoutPresenter;
            mLayoutSolver = layoutSolver;
        }

        public void BindCoordinator(InGameInteractionCoordinator coordinator)
        {
            mCoordinator = coordinator;
        }

        public void BuildInitialHandItems(CoreViewSnapshot snapshot)
        {
            if (snapshot?.Deck == null || mActorFactory == null || mViewRegistry == null)
            {
                return;
            }

            IReadOnlyList<int> itemUids = snapshot.Deck.ItemSlotUids;
            var actors = new List<Transform>(itemUids.Count);
            var targets = new List<HandCardLayoutTarget>(itemUids.Count);

            for (var i = 0; i < itemUids.Count; i++)
            {
                int uid = itemUids[i];
                if (uid <= 0)
                {
                    continue;
                }

                string defId = ResolveDefId(snapshot, uid);
                Transform actor = EnsureHandActor(uid, defId);
                if (actor != null)
                {
                    actors.Add(actor);
                }
            }

            if (mLayoutSolver != null && mLayoutPresenter != null && actors.Count > 0)
            {
                mLayoutSolver.BuildLayout(actors.Count, targets);
                for (var i = 0; i < actors.Count; i++)
                {
                    HandCardLayoutTarget target = targets[i];
                    mLayoutPresenter.RegisterActor(actors[i], target.LocalPosition, target.SortingOrder);
                }

                mLayoutPresenter.Relayout(actors, targets, durationOverride: 0f);
            }

            mCoordinator?.SetHandActors(actors);
        }

        private Transform EnsureHandActor(int uid, string defId)
        {
            if (!mActorFactory.TryGet(uid, out Transform actor) || actor == null)
            {
                actor = mActorFactory.Spawn(defId, uid, mHandActorsRoot);
            }
            else if (mHandActorsRoot != null)
            {
                actor.SetParent(mHandActorsRoot, false);
            }

            if (actor == null)
            {
                return null;
            }

            mViewRegistry.RegisterActor(uid, actor);
            EnsureHandRelay(actor, uid);
            return actor;
        }

        private static void EnsureHandRelay(Transform actor, int uid)
        {
            if (actor == null)
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

            if (actor.GetComponent<HandCardInputRelay>() == null)
            {
                actor.gameObject.AddComponent<HandCardInputRelay>();
            }
        }

        private static string ResolveDefId(CoreViewSnapshot snapshot, int uid)
        {
            if (snapshot.TryGetCard(uid, out CardView card))
            {
                return card.DefId;
            }

            return string.Empty;
        }
    }

}
