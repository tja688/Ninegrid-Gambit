using System.Collections.Generic;
using NineGrid.Core.Content;
using QFramework;

namespace NineGrid.Core.Systems
{
    public interface IEconomySystem : ISystem
    {
        int AwardSkipHelpChoice();
        int AwardSkipRelicChoice();
        int SettleUnusedHelpCards();
        int ClearResidualTraps();
        int DeleteHelpCard(int cardUid);

        /// <summary>
        /// 重绑系统级 Trigger（InitialGameFactory 若 Clear 了 TriggerSystem 后必须调用）。
        /// </summary>
        void RebindSystemTriggers();
    }

    public sealed class EconomySystem : AbstractSystem, IEconomySystem
    {
        private IUnRegister mRemoveGoldUnregister;

        protected override void OnInit()
        {
            RebindSystemTriggers();
        }

        public void RebindSystemTriggers()
        {
            if (mRemoveGoldUnregister != null)
            {
                mRemoveGoldUnregister.UnRegister();
                mRemoveGoldUnregister = null;
            }

            mRemoveGoldUnregister = this.GetSystem<ITriggerSystem>().Register(
                TriggerPoint.OnRemove,
                TriggerTiming.Post,
                new DelegateTriggerReaction("economy.removeGold", ReactToRemovedCards));
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

        public int SettleUnusedHelpCards()
        {
            var catalog = CatalogOrNull();
            if (catalog == null)
            {
                return 0;
            }

            var count = CountUnusedHelpCards();
            ReturnPlayerSideHelpCardsToRunDeck();
            RemoveLingeringHelpCards();

            if (count <= 0 || catalog.Economy.UnusedHelpCardGold == 0)
            {
                return 0;
            }

            return ExecuteGold(count * catalog.Economy.UnusedHelpCardGold, "unusedHelpCards");
        }

        public int ClearResidualTraps()
        {
            var registry = this.GetModel<CardRegistry>();
            var board = this.GetModel<BoardModel>();
            var targets = new List<int>();
            var seen = new HashSet<int>();
            foreach (var uid in board.BoardCardUids())
            {
                CardInstance card;
                if (uid == 0 || !seen.Add(uid) || !registry.TryGet(uid, out card) || card.Kind != CardKind.Trap)
                {
                    continue;
                }

                targets.Add(uid);
            }

            if (targets.Count == 0)
            {
                return 0;
            }

            var pipeline = this.GetSystem<IActionPipelineSystem>();
            for (var i = 0; i < targets.Count; i++)
            {
                pipeline.Enqueue(new RemoveCardAction(targets[i], ZoneId.Removed, "clearResidualTrap"));
            }

            return pipeline.RunToCompletion();
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
            if (catalog == null || context.Events == null)
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
                if (!registry.TryGet(evt.CardUid, out card) || !CardCombatRules.IsTrueMonster(card.Kind))
                {
                    continue;
                }

                // Legacy tests can still attach explicit GoldReward; those actions already pay it.
                if (card.Counters.Get(CoreCounterKeys.GoldReward) != 0)
                {
                    continue;
                }

                var amount = catalog.Economy != null ? catalog.Economy.MonsterRemovedGold : 0;
                CardContentDefinition def;
                if (!string.IsNullOrEmpty(card.DefId)
                    && catalog.Cards != null
                    && catalog.Cards.TryGetValue(card.DefId, out def)
                    && def != null
                    && def.KillGold > 0)
                {
                    amount = def.KillGold;
                }

                if (amount == 0)
                {
                    continue;
                }

                result.Add(new ModifyGoldAction(amount, "remove:" + card.DefId));
            }

            return result;
        }

        private void ReturnPlayerSideHelpCardsToRunDeck()
        {
            var registry = this.GetModel<CardRegistry>();
            var board = this.GetModel<BoardModel>();
            var deck = this.GetModel<DeckModel>();
            var player = this.GetModel<PlayerModel>();
            var seen = new HashSet<int>();
            var returnTargets = new List<ReturnedHelpCard>();

            CollectPlayerSideHelpCards(registry, deck.DrawPileUids, seen, returnTargets);
            CollectPlayerSideHelpCards(registry, deck.PlayerCardPoolUids, seen, returnTargets);
            CollectPlayerSideHelpCards(registry, deck.ItemSlotUids, seen, returnTargets);
            foreach (var uid in board.BoardCardUids())
            {
                CollectPlayerSideHelpCard(registry, uid, seen, returnTargets);
            }

            if (returnTargets.Count == 0)
            {
                return;
            }

            var pipeline = this.GetSystem<IActionPipelineSystem>();
            for (var i = 0; i < returnTargets.Count; i++)
            {
                var target = returnTargets[i];
                player.AddHelpCard(target.DefId, 1);
                pipeline.Enqueue(new RemoveCardAction(target.Uid, ZoneId.Removed, "settleReturn"));
            }

            pipeline.RunToCompletion();
        }

        private void RemoveLingeringHelpCards()
        {
            var registry = this.GetModel<CardRegistry>();
            var board = this.GetModel<BoardModel>();
            var deck = this.GetModel<DeckModel>();
            var seen = new HashSet<int>();
            var targets = new List<int>();

            CollectHelpCardUids(registry, deck.DrawPileUids, seen, targets);
            CollectHelpCardUids(registry, deck.PlayerCardPoolUids, seen, targets);
            CollectHelpCardUids(registry, deck.ItemSlotUids, seen, targets);
            foreach (var uid in board.BoardCardUids())
            {
                CollectHelpCardUid(registry, uid, seen, targets);
            }

            if (targets.Count == 0)
            {
                return;
            }

            var pipeline = this.GetSystem<IActionPipelineSystem>();
            for (var i = 0; i < targets.Count; i++)
            {
                pipeline.Enqueue(new RemoveCardAction(targets[i], ZoneId.Removed, "settleRemove"));
            }

            pipeline.RunToCompletion();
        }

        private static void CollectHelpCardUids(
            CardRegistry registry,
            IReadOnlyList<int> uids,
            HashSet<int> seen,
            List<int> targets)
        {
            for (var i = 0; i < uids.Count; i++)
            {
                CollectHelpCardUid(registry, uids[i], seen, targets);
            }
        }

        private static void CollectHelpCardUid(
            CardRegistry registry,
            int uid,
            HashSet<int> seen,
            List<int> targets)
        {
            CardInstance card;
            if (uid == 0 || !seen.Add(uid) || !registry.TryGet(uid, out card) || card.Kind != CardKind.HelpCard)
            {
                return;
            }

            targets.Add(uid);
        }

        private static void CollectPlayerSideHelpCards(
            CardRegistry registry,
            IReadOnlyList<int> uids,
            HashSet<int> seen,
            List<ReturnedHelpCard> returnTargets)
        {
            for (var i = 0; i < uids.Count; i++)
            {
                CollectPlayerSideHelpCard(registry, uids[i], seen, returnTargets);
            }
        }

        private static void CollectPlayerSideHelpCard(
            CardRegistry registry,
            int uid,
            HashSet<int> seen,
            List<ReturnedHelpCard> returnTargets)
        {
            CardInstance card;
            if (uid == 0
                || !seen.Add(uid)
                || !registry.TryGet(uid, out card)
                || card.Kind != CardKind.HelpCard
                || card.Counters.Get(CoreCounterKeys.PlayerSideDeck) <= 0)
            {
                return;
            }

            returnTargets.Add(new ReturnedHelpCard(uid, card.DefId));
        }

        private sealed class ReturnedHelpCard
        {
            public ReturnedHelpCard(int uid, string defId)
            {
                Uid = uid;
                DefId = defId ?? string.Empty;
            }

            public int Uid { get; private set; }
            public string DefId { get; private set; }
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
