namespace NineGrid.Core.Stats
{
    public interface IStatCondition
    {
        bool IsMet(StatEvaluationContext context);
    }

    public sealed class StatEvaluationContext
    {
        public StatEvaluationContext(CardInstance owner, CardRegistry registry, BoardModel board, PlayerModel player)
            : this(owner, registry, board, player, null)
        {
        }

        public StatEvaluationContext(
            CardInstance owner,
            CardRegistry registry,
            BoardModel board,
            PlayerModel player,
            RuleModifierRegistry ruleModifiers)
        {
            Owner = owner;
            Registry = registry;
            Board = board;
            Player = player;
            RuleModifiers = ruleModifiers;
            ActionName = string.Empty;
            SourceDefId = string.Empty;
            Cause = string.Empty;
            TargetUid = 0;
            ActorUid = 0;
        }

        public CardInstance Owner { get; private set; }
        public CardRegistry Registry { get; private set; }
        public BoardModel Board { get; private set; }
        public PlayerModel Player { get; private set; }
        public RuleModifierRegistry RuleModifiers { get; private set; }
        public string ActionName { get; private set; }
        public string SourceDefId { get; private set; }
        public string Cause { get; private set; }
        public int TargetUid { get; private set; }
        public int ActorUid { get; private set; }

        public SlotId OwnerSlot
        {
            get { return Owner == null ? SlotId.None : Owner.Slot.Value; }
        }

        public StatEvaluationContext WithActionSource(string actionName, string sourceDefId, string cause)
        {
            ActionName = actionName ?? string.Empty;
            SourceDefId = sourceDefId ?? string.Empty;
            Cause = cause ?? string.Empty;
            return this;
        }

        public StatEvaluationContext WithTarget(int targetUid)
        {
            TargetUid = targetUid;
            return this;
        }

        public StatEvaluationContext WithActor(int actorUid)
        {
            ActorUid = actorUid;
            return this;
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

            // 分母口径＝有效上限（与 HealAction 钳血 / UI EffectiveMaxHp 同源）：
            // 遗物等以修饰符形式加的 MaxHp 必须计入，否则触发线与玩家看到的上限脱节
            // （如血液暴力自带 MaxHp+4，用基础上限判「低于50%」会比卡面语义低 2 血）。
            var maxHp = StatConditionMaxHp.GetEffectiveMaxHp(context);
            if (maxHp <= 0f)
            {
                return false;
            }

            var hp = context.Owner.Stats.GetBase(StatId.Hp);
            return hp / maxHp < Pct;
        }
    }

    /// <summary>
    /// 条件求值内取有效 MaxHp 的共用护栏：MaxHp 修饰若自身也挂 HP 百分比类条件，
    /// 全管线求值会无限递归——重入时退回基础上限，保证求值终止。
    /// </summary>
    public static class StatConditionMaxHp
    {
        private static readonly StatPipeline sPipeline = new StatPipeline();

        [System.ThreadStatic]
        private static bool sEvaluating;

        public static float GetEffectiveMaxHp(StatEvaluationContext context)
        {
            if (context == null || context.Owner == null)
            {
                return 0f;
            }

            var block = context.Owner.Stats;
            if (sEvaluating)
            {
                return block.GetBase(StatId.MaxHp);
            }

            sEvaluating = true;
            try
            {
                return sPipeline.Evaluate(block, StatId.MaxHp, context);
            }
            finally
            {
                sEvaluating = false;
            }
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

    /// <summary>
    /// Owner 正交邻接是否存在匹配 defId 和/或 kind 的其他卡（用于 RuleModifier / Conditional 层）。
    /// 怪-怪邻接与 IBoardSystem.AreAdjacent 对齐：VirtualAdjacency + 天涯若比邻（GlobalMonsterAdjacency）。
    /// </summary>
    public sealed class OwnerAdjacentHasCardCondition : IStatCondition
    {
        public OwnerAdjacentHasCardCondition(string defId, CardKind kind)
        {
            DefId = defId ?? string.Empty;
            Kind = kind;
        }

        public string DefId { get; private set; }
        public CardKind Kind { get; private set; }

        public bool IsMet(StatEvaluationContext context)
        {
            if (context == null
                || context.Owner == null
                || context.Board == null
                || context.Registry == null
                || !context.OwnerSlot.IsBoardSlot
                || (string.IsNullOrEmpty(DefId) && Kind == CardKind.Unknown))
            {
                return false;
            }

            var ownerUid = context.Owner.Uid;
            var globalAdjacencyResolved = false;
            var hasGlobalMonsterAdjacency = false;
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                var cardUid = context.Board.GetCardUid(slot);
                if (cardUid == 0 || cardUid == ownerUid)
                {
                    continue;
                }

                CardInstance card;
                if (!context.Registry.TryGet(cardUid, out card))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(DefId)
                    && !string.Equals(card.DefId, DefId, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (Kind != CardKind.Unknown && card.Kind != Kind)
                {
                    continue;
                }

                if (context.OwnerSlot.IsAdjacentTo(slot))
                {
                    return true;
                }

                if (context.Owner.Kind != CardKind.Monster || card.Kind != CardKind.Monster)
                {
                    continue;
                }

                if (MonsterBoardRules.HasVirtualAdjacency(context, context.Owner, card.Uid)
                    || MonsterBoardRules.HasVirtualAdjacency(context, card, ownerUid))
                {
                    return true;
                }

                if (!globalAdjacencyResolved)
                {
                    globalAdjacencyResolved = true;
                    hasGlobalMonsterAdjacency = MonsterBoardRules.HasGlobalMonsterAdjacency(context);
                }

                if (hasGlobalMonsterAdjacency)
                {
                    return true;
                }
            }

            return false;
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
            if (!context.Registry.TryGet(SourceUid, out source)
                || !context.Registry.TryGet(TargetUid, out target)
                || source == null
                || target == null)
            {
                return false;
            }

            if (source.Slot.Value.IsAdjacentTo(target.Slot.Value))
            {
                return true;
            }

            // 与 IBoardSystem.AreAdjacent 对齐：VirtualAdjacency + 天涯若比邻（怪-怪技能邻接）。
            if (source.Kind != CardKind.Monster || target.Kind != CardKind.Monster)
            {
                return false;
            }

            return MonsterBoardRules.HasVirtualAdjacency(context, source, target.Uid)
                || MonsterBoardRules.HasVirtualAdjacency(context, target, source.Uid)
                || MonsterBoardRules.HasGlobalMonsterAdjacency(context);
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

    public sealed class ActorUidCondition : IStatCondition
    {
        public ActorUidCondition(int actorUid)
        {
            ActorUid = actorUid;
        }

        public int ActorUid { get; private set; }

        public bool IsMet(StatEvaluationContext context)
        {
            return ActorUid == 0 || (context != null && context.ActorUid == ActorUid);
        }
    }

    public sealed class CardKindCondition : IStatCondition
    {
        public CardKindCondition(CardKind kind)
        {
            Kind = kind;
        }

        public CardKind Kind { get; private set; }

        public bool IsMet(StatEvaluationContext context)
        {
            return Kind == CardKind.Unknown || (context != null && context.Owner != null && context.Owner.Kind == Kind);
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

    public sealed class ActionSourceCondition : IStatCondition
    {
        public ActionSourceCondition(
            string actionName,
            string sourceDefId,
            string excludeSourceDefId,
            string cause,
            string excludeCause)
        {
            ActionName = actionName ?? string.Empty;
            SourceDefId = sourceDefId ?? string.Empty;
            ExcludeSourceDefId = excludeSourceDefId ?? string.Empty;
            Cause = cause ?? string.Empty;
            ExcludeCause = excludeCause ?? string.Empty;
        }

        public string ActionName { get; private set; }
        public string SourceDefId { get; private set; }
        public string ExcludeSourceDefId { get; private set; }
        public string Cause { get; private set; }
        public string ExcludeCause { get; private set; }

        public bool IsMet(StatEvaluationContext context)
        {
            if (context == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(ActionName) && !Same(context.ActionName, ActionName))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(SourceDefId) && !Same(context.SourceDefId, SourceDefId))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(ExcludeSourceDefId) && Same(context.SourceDefId, ExcludeSourceDefId))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(Cause) && !Same(context.Cause, Cause))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(ExcludeCause) && Same(context.Cause, ExcludeCause))
            {
                return false;
            }

            return true;
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left ?? string.Empty, right ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class ExcludeSourcePrefixCondition : IStatCondition
    {
        public ExcludeSourcePrefixCondition(string prefix)
        {
            Prefix = prefix ?? string.Empty;
        }

        public string Prefix { get; private set; }

        public bool IsMet(StatEvaluationContext context)
        {
            if (context == null || string.IsNullOrEmpty(Prefix))
            {
                return true;
            }

            var sourceDefId = context.SourceDefId ?? string.Empty;
            return !sourceDefId.StartsWith(Prefix, System.StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 仅匹配「来源为空」的结算（玩家直接交战攻击）：效果/遗物/机关发起的伤害都带
    /// 来源 defId（relic.* / trap.* / help.* …），不满足；暴力卡 ×2 只被玩家自己的
    /// 普通攻击消费，捡道具卡 / 使用道具卡引发的遗物齐射等不再误吞（策划回归）。
    /// </summary>
    public sealed class EmptySourceCondition : IStatCondition
    {
        public bool IsMet(StatEvaluationContext context)
        {
            return context == null || string.IsNullOrEmpty(context.SourceDefId);
        }
    }
}
