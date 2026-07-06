using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Core
{
    public sealed class EffectiveStatQuery : AbstractQuery<int>
    {
        public EffectiveStatQuery(int cardUid, StatId stat)
        {
            CardUid = cardUid;
            Stat = stat;
        }

        public int CardUid { get; private set; }
        public StatId Stat { get; private set; }

        protected override int OnDo()
        {
            var card = this.GetModel<CardRegistry>().Get(CardUid);
            return this.GetSystem<IStatSystem>().GetEffectiveInt(card, Stat);
        }
    }
}
