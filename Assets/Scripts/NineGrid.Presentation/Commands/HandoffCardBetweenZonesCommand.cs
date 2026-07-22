using NineGrid.Cards;
using NineGrid.Cards.Convergence;
using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 净土域 C 阶段交接：经 Lifecycle / Battle System 做 Evict→Admit，不触碰 Manager.Instance。
    /// </summary>
    public sealed class HandoffCardBetweenZonesCommand : AbstractCommand<bool>
    {
        /// <summary>手牌/牌库/战斗净土域交接端（非 Core ZoneId）。</summary>
        public enum SanctuaryEndpoint
        {
            Hand = 0,
            Deck = 1,
            Battle = 2,
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

            if (!TryEvict(out var state))
            {
                return false;
            }

            return TryAdmit(in state);
        }

        private bool TryEvict(out HandoffState state)
        {
            switch (mFrom)
            {
                case SanctuaryEndpoint.Hand:
                case SanctuaryEndpoint.Deck:
                {
                    var lifecycle = this.GetSystem<ICardEntityLifecycleSystem>();
                    if (lifecycle == null || !lifecycle.IsBound)
                    {
                        state = default;
                        return false;
                    }

                    state = mFrom == SanctuaryEndpoint.Hand
                        ? lifecycle.EvictFromHand(mCard)
                        : lifecycle.EvictFromDeck(mCard);
                    return true;
                }
                case SanctuaryEndpoint.Battle:
                {
                    var battle = this.GetSystem<IFieldBattlePresentationSystem>();
                    if (battle == null || !battle.IsBound)
                    {
                        state = default;
                        return false;
                    }

                    state = battle.EvictCard(mCard);
                    return true;
                }
                default:
                    state = default;
                    return false;
            }
        }

        private bool TryAdmit(in HandoffState state)
        {
            switch (mTo)
            {
                case SanctuaryEndpoint.Hand:
                case SanctuaryEndpoint.Deck:
                {
                    var lifecycle = this.GetSystem<ICardEntityLifecycleSystem>();
                    if (lifecycle == null || !lifecycle.IsBound)
                    {
                        return false;
                    }

                    if (mTo == SanctuaryEndpoint.Hand)
                    {
                        lifecycle.AdmitToHand(mCard, in state);
                    }
                    else
                    {
                        lifecycle.AdmitToDeck(mCard, in state);
                    }

                    return true;
                }
                case SanctuaryEndpoint.Battle:
                {
                    var battle = this.GetSystem<IFieldBattlePresentationSystem>();
                    if (battle == null || !battle.IsBound)
                    {
                        return false;
                    }

                    battle.AdmitCard(mCard, in state);
                    return true;
                }
                default:
                    return false;
            }
        }
    }
}
