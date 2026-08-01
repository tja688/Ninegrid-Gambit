using QFramework;

namespace NineGrid.Core.Systems
{
    public interface IDeckSystem : ISystem
    {
        int SetupNode(NodeDeckOptions options);
        bool HasEnemyOnBoard();
        bool HasEnemyInDrawPile();
        bool HasPendingEnemyCards();
        bool IsNodeCleared();
    }

    public sealed class DeckSystem : AbstractSystem, IDeckSystem
    {
        protected override void OnInit()
        {
        }

        public int SetupNode(NodeDeckOptions options)
        {
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            options = options ?? NodeDeckOptions.CreateDefaultBattle();
            pipeline.Enqueue(new SetupNodeDeckAction(options));
            pipeline.Enqueue(new OpeningDealAction(options));
            pipeline.Enqueue(new FillEmptySlotsAction());
            return pipeline.RunToCompletion();
        }

        public bool HasEnemyOnBoard()
        {
            var registry = this.GetModel<CardRegistry>();
            var board = this.GetModel<BoardModel>();
            foreach (var uid in board.BoardCardUids())
            {
                CardInstance card;
                if (registry.TryGet(uid, out card) && CardCombatRules.IsTrueMonster(card.Kind))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasEnemyInDrawPile()
        {
            var registry = this.GetModel<CardRegistry>();
            var deck = this.GetModel<DeckModel>();
            for (var i = 0; i < deck.DrawPileUids.Count; i++)
            {
                var card = registry.Get(deck.DrawPileUids[i]);
                if (CardCombatRules.IsTrueMonster(card.Kind))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasPendingEnemyCards()
        {
            // ADR-0017：敌池/抽牌/场上残留 Trap 不算「还有敌」。
            return HasEnemyInEnemyCardPool() || HasEnemyInDrawPile() || HasEnemyOnBoard();
        }

        public bool IsNodeCleared()
        {
            // ADR-0017：清关只看真怪物；抽牌堆/敌池/场上残留 Trap 不挡关。
            return !HasEnemyInDrawPile()
                && !HasEnemyInEnemyCardPool()
                && !HasEnemyOnBoard();
        }

        private bool HasEnemyInEnemyCardPool()
        {
            var registry = this.GetModel<CardRegistry>();
            var deck = this.GetModel<DeckModel>();
            for (var i = 0; i < deck.EnemyCardPoolUids.Count; i++)
            {
                var card = registry.Get(deck.EnemyCardPoolUids[i]);
                if (CardCombatRules.IsTrueMonster(card.Kind))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
