namespace NineGrid.Core.Stats
{
    public interface IStatCondition
    {
        bool IsMet(StatEvaluationContext context);
    }

    public sealed class StatEvaluationContext
    {
        public StatEvaluationContext(CardInstance owner, CardRegistry registry, BoardModel board, PlayerModel player)
        {
            Owner = owner;
            Registry = registry;
            Board = board;
            Player = player;
        }

        public CardInstance Owner { get; private set; }
        public CardRegistry Registry { get; private set; }
        public BoardModel Board { get; private set; }
        public PlayerModel Player { get; private set; }

        public SlotId OwnerSlot
        {
            get { return Owner == null ? SlotId.None : Owner.Slot.Value; }
        }
    }

    public sealed class AlwaysStatCondition : IStatCondition
    {
        public bool IsMet(StatEvaluationContext context)
        {
            return true;
        }
    }

    public sealed class AtSlotCondition : IStatCondition
    {
        public AtSlotCondition(SlotId slot)
        {
            Slot = slot;
        }

        public SlotId Slot { get; private set; }

        public bool IsMet(StatEvaluationContext context)
        {
            return context != null && context.OwnerSlot == Slot;
        }
    }

    public sealed class HpBelowPctCondition : IStatCondition
    {
        public HpBelowPctCondition(float pct)
        {
            Pct = pct;
        }

        public float Pct { get; private set; }

        public bool IsMet(StatEvaluationContext context)
        {
            if (context == null || context.Owner == null)
            {
                return false;
            }

            var maxHp = context.Owner.Stats.GetBase(StatId.MaxHp);
            if (maxHp <= 0f)
            {
                return false;
            }

            var hp = context.Owner.Stats.GetBase(StatId.Hp);
            return hp / maxHp < Pct;
        }
    }

    public sealed class AdjacentCondition : IStatCondition
    {
        public AdjacentCondition(SlotId targetSlot)
        {
            TargetSlot = targetSlot;
        }

        public SlotId TargetSlot { get; private set; }

        public bool IsMet(StatEvaluationContext context)
        {
            return context != null && context.OwnerSlot.IsAdjacentTo(TargetSlot);
        }
    }

    public sealed class AdjacentToUidCondition : IStatCondition
    {
        public AdjacentToUidCondition(int targetUid)
        {
            TargetUid = targetUid;
        }

        public int TargetUid { get; private set; }

        public bool IsMet(StatEvaluationContext context)
        {
            if (context == null || context.Owner == null || context.Registry == null)
            {
                return false;
            }

            CardInstance target;
            return context.Registry.TryGet(TargetUid, out target) && context.OwnerSlot.IsAdjacentTo(target.Slot.Value);
        }
    }

    public sealed class SourceAdjacentToUidCondition : IStatCondition
    {
        public SourceAdjacentToUidCondition(int sourceUid, int targetUid)
        {
            SourceUid = sourceUid;
            TargetUid = targetUid;
        }

        public int SourceUid { get; private set; }
        public int TargetUid { get; private set; }

        public bool IsMet(StatEvaluationContext context)
        {
            if (context == null || context.Registry == null)
            {
                return false;
            }

            CardInstance source;
            CardInstance target;
            return context.Registry.TryGet(SourceUid, out source)
                && context.Registry.TryGet(TargetUid, out target)
                && source.Slot.Value.IsAdjacentTo(target.Slot.Value);
        }
    }

    public sealed class TargetUidCondition : IStatCondition
    {
        public TargetUidCondition(int targetUid)
        {
            TargetUid = targetUid;
        }

        public int TargetUid { get; private set; }

        public bool IsMet(StatEvaluationContext context)
        {
            return context != null && context.Owner != null && context.Owner.Uid == TargetUid;
        }
    }

    public sealed class TargetUidOrZeroCondition : IStatCondition
    {
        public TargetUidOrZeroCondition(int targetUid)
        {
            TargetUid = targetUid;
        }

        public int TargetUid { get; private set; }

        public bool IsMet(StatEvaluationContext context)
        {
            return TargetUid == 0 || (context != null && context.Owner != null && context.Owner.Uid == TargetUid);
        }
    }

    public sealed class ZoneCondition : IStatCondition
    {
        public ZoneCondition(ZoneId zone)
        {
            Zone = zone;
        }

        public ZoneId Zone { get; private set; }

        public bool IsMet(StatEvaluationContext context)
        {
            return Zone == ZoneId.None || (context != null && context.Owner != null && context.Owner.Zone.Value == Zone);
        }
    }
}
