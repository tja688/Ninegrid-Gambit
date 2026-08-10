using System.Collections.Generic;
using NineGrid.Core.Content;
using QFramework;

namespace NineGrid.Core.Systems
{
    public interface IEconomySystem : ISystem
    {
        int AwardSkipHelpChoice();
        int AwardSkipRelicChoice();
        int AwardDiscardRelic();
        int AwardRecycleItemSlot();
        int SettleUnusedHelpCards();
        /// <summary>清关时清掉场上残留怪与机关（不兑金）。</summary>
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

        public int AwardDiscardRelic()
        {
            var catalog = CatalogOrNull();
            if (catalog == null)
            {
                return 0;
            }

            return ExecuteGold(catalog.Economy.DiscardRelicGold, "discardRelic");
        }

        public int AwardRecycleItemSlot()
        {
            var catalog = CatalogOrNull();
            if (catalog == null)
            {
                return 0;
            }

            return ExecuteGold(catalog.Economy.RecycleItemSlotGold, "recycleItemSlot");
        }

        public int SettleUnusedHelpCards()
        {
            var battle = this.GetModel<BattleContextModel>();
            return RemoveLingeringHelpCards(battle.AreAllOpeningMonstersDefeated());
        }

        public int ClearResidualTraps()
        {
            // ADR-0026 / #113：清关同拍清掉场上残留怪与机关（不兑金）。
            var registry = this.GetModel<CardRegistry>();
            var board = this.GetModel<BoardModel>();
            var avatarUid = board.AvatarUid.Value;
            var targets = new List<int>();
            var seen = new HashSet<int>();
            foreach (var uid in board.BoardCardUids())
            {
                CardInstance card;
                if (uid == 0
                    || uid == avatarUid
                    || !seen.Add(uid)
                    || !registry.TryGet(uid, out card))
                {
                    continue;
                }

                if (card.Kind != CardKind.Trap && !CardCombatRules.IsTrueMonster(card.Kind))
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
                pipeline.Enqueue(new RemoveCardAction(targets[i], ZoneId.Removed, "clearResidualBoard"));
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

                // ADR-0026 / #113：清关清场移除不兑金（恋战击杀仍走正常移除赏金）。
                if (IsClearResidualRemove(evt))
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

        private static bool IsClearResidualRemove(CoreGameEvent evt)
        {
            if (evt == null)
            {
                return false;
            }

            return evt.Message == "clearResidualBoard"
                || evt.Cause == "clearResidualBoard"
                || evt.Message == "clearResidualTrap"
                || evt.Cause == "clearResidualTrap";
        }

        private int RemoveLingeringHelpCards(bool sellBoardResidual)
        {
            var registry = this.GetModel<CardRegistry>();
            var board = this.GetModel<BoardModel>();
            var deck = this.GetModel<DeckModel>();
            var seen = new HashSet<int>();
            var deckTargets = new List<int>();
            var boardTargets = new List<int>();

            CollectHelpCardUids(registry, deck.DrawPileUids, seen, deckTargets);
            CollectHelpCardUids(registry, deck.PlayerCardPoolUids, seen, deckTargets);
            // ADR-0025 / #107：道具卡格跑图内持续持有，清关不兑不清。
            foreach (var uid in board.BoardCardUids())
            {
                CollectHelpCardUid(registry, uid, seen, boardTargets);
            }

            if (deckTargets.Count == 0 && boardTargets.Count == 0)
            {
                return 0;
            }

            var catalog = CatalogOrNull();
            var goldPerCard = catalog != null && catalog.Economy != null
                ? catalog.Economy.RecycleItemSlotGold
                : 0;
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            var resolved = 0;

            if (sellBoardResidual && goldPerCard > 0)
            {
                for (var i = 0; i < boardTargets.Count; i++)
                {
                    pipeline.Enqueue(new ModifyGoldAction(goldPerCard, "settleSell"));
                    pipeline.Enqueue(new RemoveCardAction(boardTargets[i], ZoneId.Removed, "settleSell"));
                }
            }
            else
            {
                for (var i = 0; i < boardTargets.Count; i++)
                {
                    pipeline.Enqueue(new RemoveCardAction(boardTargets[i], ZoneId.Removed, "settleRemove"));
                }
            }

            for (var i = 0; i < deckTargets.Count; i++)
            {
                pipeline.Enqueue(new RemoveCardAction(deckTargets[i], ZoneId.Removed, "settleRemove"));
            }

            resolved += pipeline.RunToCompletion();
            return resolved;
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
