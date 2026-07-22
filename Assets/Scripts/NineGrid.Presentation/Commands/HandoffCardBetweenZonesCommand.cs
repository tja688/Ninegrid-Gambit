using NineGrid.Cards;
using NineGrid.Cards.Convergence;
using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 净土域 C 阶段交接：经 Lifecycle System 做 Evict→Admit，不触碰 Manager.Instance。
    /// </summary>
    public sealed class HandoffCardBetweenZonesCommand : AbstractCommand<bool>
    {
        /// <summary>手牌/牌库净土域交接端（非 Core ZoneId）。</summary>
        public enum SanctuaryEndpoint
        {
            Hand = 0,
            Deck = 1,
        }

        private readonly ManagedCard mCard;
        private readonly SanctuaryEndpoint mFrom;
        private readonly SanctuaryEndpoint mTo;

        public HandoffCardBetweenZonesCommand(ManagedCard card, SanctuaryEndpoint from, SanctuaryEndpoint to)
        {
            mCard = card;
            mFrom = from;
            mTo = to;
        }

        protected override bool OnExecute()
        {
            if (mCard == null || mFrom == mTo)
            {
                return false;
            }

            var lifecycle = this.GetSystem<ICardEntityLifecycleSystem>();
            if (lifecycle == null || !lifecycle.IsBound)
            {
                return false;
            }

            HandoffState state;
            switch (mFrom)
            {
                case SanctuaryEndpoint.Hand:
                    state = lifecycle.EvictFromHand(mCard);
                    break;
                case SanctuaryEndpoint.Deck:
                    state = lifecycle.EvictFromDeck(mCard);
                    break;
                default:
                    return false;
            }

            switch (mTo)
            {
                case SanctuaryEndpoint.Hand:
                    lifecycle.AdmitToHand(mCard, in state);
                    return true;
                case SanctuaryEndpoint.Deck:
                    lifecycle.AdmitToDeck(mCard, in state);
                    return true;
                default:
                    return false;
            }
        }
    }
}
