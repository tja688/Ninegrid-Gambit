using System.Collections.Generic;
using NineGrid.Core.Content;
using QFramework;

namespace NineGrid.Core.Systems
{
    public interface IEconomySystem : ISystem
    {
        int AwardSkipHelpChoice();
        int AwardSkipRelicChoice();
        int DeleteHelpCard(int cardUid);
    }

    public sealed class EconomySystem : AbstractSystem, IEconomySystem
    {
        private int mLastNodeEndActionId;

        protected override void OnInit()
        {
            this.GetSystem<ITriggerSystem>().Register(
                TriggerPoint.OnRemove,
                TriggerTiming.Post,
                new DelegateTriggerReaction("economy.removeGold", ReactToRemovedCards));

            this.GetSystem<ITriggerSystem>().Register(
                TriggerPoint.OnNodeEnd,
                TriggerTiming.Post,
                new DelegateTriggerReaction("economy.unusedHelpCards", ReactToNodeEnd));
        }

        public int AwardSkipHelpChoice()
        {
            var catalog = CatalogOrNull();
            if (catalog == null)
            {
                return 0;
            }

            return ExecuteGold(catalog.Economy.SkipHelpChoiceGold, "skipHelpChoice");
        }

        public int AwardSkipRelicChoice()
        {
            var catalog = CatalogOrNull();
            if (catalog == null)
            {
                return 0;
            }

            return ExecuteGold(catalog.Economy.SkipRelicChoiceGold, "skipRelicChoice");
        }

        public int DeleteHelpCard(int cardUid)
        {
            var catalog = CatalogOrNull();
            if (catalog == null || cardUid <= 0)
            {
                return 0;
            }

            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new RemoveCardAction(cardUid, ZoneId.Removed, "shopDelete"));
            pipeline.Enqueue(new ModifyGoldAction(catalog.Economy.ShopDeleteHelpCardGold, "shopDelete"));
            return pipeline.RunToCompletion();
        }

        private IEnumerable<GameAction> ReactToRemovedCards(TriggerContext context)
        {
            var catalog = CatalogOrNull();
            if (catalog == null || catalog.Economy.MonsterRemovedGold == 0 || context.Events == null)
            {
                return null;
            }

            var registry = this.GetModel<CardRegistry>();
            var result = new List<GameAction>();
            for (var i = 0; i < context.Events.Count; i++)
            {
                var evt = context.Events[i];
                if (evt.Type != CoreEventType.CardRemoved || evt.CardUid == 0)
                {
                    continue;
                }

                CardInstance card;
                if (!registry.TryGet(evt.CardUid, out card) || card.Kind != CardKind.Monster)
                {
                    continue;
                }

                // Legacy tests can still attach explicit GoldReward; those actions already pay it.
                if (card.Counters.Get(CoreCounterKeys.GoldReward) != 0)
                {
                    continue;
                }

                result.Add(new ModifyGoldAction(catalog.Economy.MonsterRemovedGold, "remove:" + card.DefId));
            }

            return result;
        }

        private IEnumerable<GameAction> ReactToNodeEnd(TriggerContext context)
        {
            var catalog = CatalogOrNull();
            if (catalog == null
                || catalog.Economy.UnusedHelpCardGold == 0
                || context == null
                || context.ActionContext == null
                || context.ActionContext.ActionId == mLastNodeEndActionId)
            {
                return null;
            }

            mLastNodeEndActionId = context.ActionContext.ActionId;
            var count = CountUnusedHelpCards();
            if (count <= 0)
            {
                return null;
            }

            return new[]
            {
                new ModifyGoldAction(count * catalog.Economy.UnusedHelpCardGold, "unusedHelpCards")
            };
        }

        private int CountUnusedHelpCards()
        {
            var registry = this.GetModel<CardRegistry>();
            var board = this.GetModel<BoardModel>();
            var deck = this.GetModel<DeckModel>();
            var seen = new HashSet<int>();

            AddHelpCardsFromList(registry, deck.DrawPileUids, seen);
            AddHelpCardsFromList(registry, deck.PlayerCardPoolUids, seen);
            AddHelpCardsFromList(registry, deck.ItemSlotUids, seen);
            foreach (var uid in board.BoardCardUids())
            {
                AddHelpCard(registry, uid, seen);
            }

            return seen.Count;
        }

        private static void AddHelpCardsFromList(CardRegistry registry, IReadOnlyList<int> uids, HashSet<int> seen)
        {
            for (var i = 0; i < uids.Count; i++)
            {
                AddHelpCard(registry, uids[i], seen);
            }
        }

        private static void AddHelpCard(CardRegistry registry, int uid, HashSet<int> seen)
        {
            CardInstance card;
            if (uid != 0 && registry.TryGet(uid, out card) && card.Kind == CardKind.HelpCard)
            {
                seen.Add(uid);
            }
        }

        private int ExecuteGold(int amount, string reason)
        {
            if (amount == 0)
            {
                return 0;
            }

            return this.GetSystem<IActionPipelineSystem>().Execute(new ModifyGoldAction(amount, reason));
        }

        private GameContentCatalog CatalogOrNull()
        {
            var content = this.GetSystem<IContentSystem>();
            content.TryReloadFromConfig();
            return content.HasCatalog ? content.Catalog : null;
        }
    }
}
