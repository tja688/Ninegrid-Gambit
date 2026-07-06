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
                if (registry.TryGet(uid, out card) && card.Kind == CardKind.Monster)
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
                if (card.Kind == CardKind.Monster)
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasPendingEnemyCards()
        {
            var deck = this.GetModel<DeckModel>();
            // Enemy staging pool should be empty after opening deal; keep check for setup-phase safety.
            return deck.EnemyCardPoolUids.Count > 0 || HasEnemyInDrawPile() || HasEnemyOnBoard();
        }

        public bool IsNodeCleared()
        {
            var deck = this.GetModel<DeckModel>();
            // FLOW_005 / RUL_发牌: runtime draw pile empty, no board enemies; staging pools empty post-opening.
            return deck.DrawPileUids.Count == 0
                && deck.EnemyCardPoolUids.Count == 0
                && !HasEnemyOnBoard();
        }
    }
}
