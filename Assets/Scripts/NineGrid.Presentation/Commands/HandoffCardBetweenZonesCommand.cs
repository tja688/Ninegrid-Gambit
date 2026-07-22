using NineGrid.Cards;
using NineGrid.Cards.Convergence;
using NineGrid.Core;
using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 净土域 C 阶段交接：经 Lifecycle System 做 Evict→Admit（含战斗端），不触碰 Manager.Instance。
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

            var lifecycle = ResolveLifecycle();
            if (lifecycle == null)
            {
                return false;
            }

            if (!TryEvict(lifecycle, out var state))
            {
                return false;
            }

            return TryAdmit(lifecycle, in state);
        }

        private bool TryEvict(ICardEntityLifecycleSystem lifecycle, out HandoffState state)
        {
            switch (mFrom)
            {
                case SanctuaryEndpoint.Hand:
                    if (!lifecycle.IsBound)
                    {
                        state = default;
                        return false;
                    }

                    state = lifecycle.EvictFromHand(mCard);
                    return true;
                case SanctuaryEndpoint.Deck:
                    if (!lifecycle.IsBound)
                    {
                        state = default;
                        return false;
                    }

                    state = lifecycle.EvictFromDeck(mCard);
                    return true;
                case SanctuaryEndpoint.Battle:
                    state = lifecycle.EvictFromBattle(mCard);
                    return true;
                default:
                    state = default;
                    return false;
            }
        }

        private bool TryAdmit(ICardEntityLifecycleSystem lifecycle, in HandoffState state)
        {
            switch (mTo)
            {
                case SanctuaryEndpoint.Hand:
                    if (!lifecycle.IsBound)
                    {
                        return false;
                    }

                    lifecycle.AdmitToHand(mCard, in state);
                    return true;
                case SanctuaryEndpoint.Deck:
                    if (!lifecycle.IsBound)
                    {
                        return false;
                    }

                    lifecycle.AdmitToDeck(mCard, in state);
                    return true;
                case SanctuaryEndpoint.Battle:
                    lifecycle.AdmitToBattle(mCard, in state);
                    return true;
                default:
                    return false;
            }
        }

        private ICardEntityLifecycleSystem ResolveLifecycle()
        {
            var existing = this.GetSystem<ICardEntityLifecycleSystem>();
            if (existing != null)
            {
                return existing;
            }

            var architecture = NineGridArchitecture.Interface;
            if (architecture == null)
            {
                return null;
            }

            ICardEntityLifecycleSystem created = new CardEntityLifecycleSystem();
            architecture.RegisterSystem(created);
            return created;
        }
    }
}
