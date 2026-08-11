using System;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;

namespace NineGrid.Core
{
    /// <summary>
    /// 卡面生成类事件的结算后绝对值写入（攻=ResultValue，血/甲=Remaining*）；
    /// 参与敌方行动的怪物另附行动倒计时事件（ADR-0005 / #81）。
    /// <para>
    /// Permanent 有效攻旁路：Conditional/常驻光环会改有效攻但不经血甲事件；
    /// Core 主动发 <see cref="CoreEventType.BaseStatModified"/>（仍走 Settled 指令，表现层不对账）。
    /// Temporary 交战加成默认不上卡面；玩家下一次对怪的规则乘区（如暴力卡 DamageMultiplier）经
    /// <see cref="AppendProjectedBattleAttackFaceCommit"/> 投影到攻击槽，与 ADR-0028 结算一致。
    /// </para>
    /// </summary>
    public static class CardFaceEventValues
    {
        public static CoreGameEvent WithFaceAbsolutes(
            this CoreGameEvent gameEvent,
            GameActionContext context,
            CardInstance card)
        {
            if (gameEvent == null || card == null)
            {
                return gameEvent;
            }

            var stats = context != null ? context.GetSystem<IStatSystem>() : null;
            var hp = stats != null
                ? stats.GetEffectiveInt(card, StatId.Hp)
                : (int)card.Stats.GetBase(StatId.Hp);
            var attack = stats != null
                ? stats.GetEffectiveInt(card, StatId.Attack)
                : (int)card.Stats.GetBase(StatId.Attack);
            var armor = StatArmorUtility.GetCurrentArmor(card);
            return gameEvent.WithRemaining(hp, armor).WithResultValue(attack);
        }

        /// <summary>
        /// 写入卡面绝对值事件，并在怪物参与敌方行动时追加 <see cref="CoreEventType.ActionCountdownChanged"/>。
        /// </summary>
        public static GameActionResult AddWithFaceAbsolutes(
            this GameActionResult result,
            GameActionContext context,
            CardInstance card,
            CoreGameEvent faceEvent)
        {
            if (result == null)
            {
                return result;
            }

            if (faceEvent != null)
            {
                result.AddEvent(faceEvent.WithFaceAbsolutes(context, card));
            }

            TryAppendActionCountdownChanged(result, context, card, faceEvent != null ? faceEvent.ActionName : null);
            return result;
        }

        public static void TryAppendActionCountdownChanged(
            GameActionResult result,
            GameActionContext context,
            CardInstance card,
            string actionName)
        {
            if (result == null
                || card == null
                || !CardRhythmRules.HasActiveRhythm(card))
            {
                return;
            }

            var actionId = context != null ? context.ActionId : 0;
            var remaining = Math.Max(0, card.Counters.Get(CoreCounterKeys.AttackPatternCountdown));
            result.AddEvent(new CoreGameEvent(
                    CoreEventType.ActionCountdownChanged,
                    actionId,
                    actionName ?? "ActionCountdown")
                .WithCard(card.Uid)
                .WithResultValue(remaining));
        }

        /// <summary>
        /// 玩家下一次对怪物普通攻击的规则层伤害（DamageMultiplier + DamageFlatDelta），
        /// 对齐 <see cref="DealDamageAction"/> 的 statContext（Owner=受击怪、Actor=玩家）。
        /// </summary>
        public static int GetProjectedPlayerBattleAttack(GameActionContext context, CardInstance avatar)
        {
            if (context == null || avatar == null || avatar.Kind != CardKind.Avatar)
            {
                return 0;
            }

            var statSystem = context.GetSystem<IStatSystem>();
            var baseDamage = Math.Max(0, statSystem.GetEffectiveInt(avatar, StatId.Attack));
            if (baseDamage <= 0)
            {
                return 0;
            }

            var evaluationContext = CreatePlayerOutgoingDamageContext(context, statSystem, avatar);
            if (evaluationContext == null)
            {
                return baseDamage;
            }

            var multipliedDamage = Math.Max(
                0,
                (int)Math.Round(statSystem.EvaluateRule(RuleId.DamageMultiplier, baseDamage, evaluationContext)));
            var flatDamage = (int)Math.Round(
                statSystem.EvaluateRule(RuleId.DamageFlatDelta, 0f, evaluationContext));
            return Math.Max(0, multipliedDamage + flatDamage);
        }

        /// <summary>
        /// 规则乘区改变玩家下一次对怪交战伤害时，提交投影攻到卡面（如暴力卡 ×2）。
        /// </summary>
        public static void AppendProjectedBattleAttackFaceCommit(
            GameActionResult result,
            GameActionContext context,
            CardInstance avatar,
            string actionName,
            string source = null,
            string sourceDefId = null)
        {
            if (result == null || context == null || avatar == null)
            {
                return;
            }

            var statSystem = context.GetSystem<IStatSystem>();
            var effectiveAttack = Math.Max(0, statSystem.GetEffectiveInt(avatar, StatId.Attack));
            var projectedAttack = GetProjectedPlayerBattleAttack(context, avatar);
            if (projectedAttack <= 0 || projectedAttack == effectiveAttack)
            {
                return;
            }

            result.AddEvent(new CoreGameEvent(CoreEventType.BaseStatModified, context.ActionId, actionName ?? "ProjectedBattleAttackFace")
                .WithCard(avatar.Uid)
                .WithTarget(avatar.Uid)
                .WithAmount((int)StatId.Attack)
                .WithDelta(projectedAttack - effectiveAttack)
                .WithResultValue(projectedAttack)
                .WithMessage(source ?? string.Empty)
                .WithSource(sourceDefId ?? string.Empty, source ?? string.Empty));
        }

        /// <summary>
        /// Permanent Attack 修饰器 Apply/条件翻转后：提交当前有效攻到卡面（ADR-0005 旁路，非直读对账）。
        /// </summary>
        public static void AppendPermanentAttackFaceCommit(
            GameActionResult result,
            GameActionContext context,
            CardInstance card,
            string actionName,
            string source = null,
            string sourceDefId = null,
            int delta = 0)
        {
            if (result == null || card == null || context == null)
            {
                return;
            }

            var effectiveAttack = Math.Max(0, context.GetSystem<IStatSystem>().GetEffectiveInt(card, StatId.Attack));
            result.AddEvent(new CoreGameEvent(CoreEventType.BaseStatModified, context.ActionId, actionName ?? "PermanentAttackFace")
                .WithCard(card.Uid)
                .WithTarget(card.Uid)
                .WithAmount((int)StatId.Attack)
                .WithDelta(delta)
                .WithResultValue(effectiveAttack)
                .WithMessage(source ?? string.Empty)
                .WithSource(sourceDefId ?? string.Empty, source ?? string.Empty));
        }

        /// <summary>
        /// CurrentArmor 变化后提交怪物卡面绝对甲（借甲光环等旁路；玩家仍走 ArmorChanged）。
        /// </summary>
        public static void AppendCurrentArmorFaceCommit(
            GameActionResult result,
            GameActionContext context,
            CardInstance card,
            string actionName,
            string source = null,
            string sourceDefId = null,
            int delta = 0)
        {
            if (result == null || card == null || context == null || card.Kind != CardKind.Monster)
            {
                return;
            }

            var currentArmor = StatArmorUtility.GetCurrentArmor(card);
            result.AddEvent(new CoreGameEvent(CoreEventType.BaseStatModified, context.ActionId, actionName ?? "CurrentArmorFace")
                .WithCard(card.Uid)
                .WithTarget(card.Uid)
                .WithAmount((int)StatId.CurrentArmor)
                .WithDelta(delta)
                .WithResultValue(currentArmor)
                .WithMessage(source ?? string.Empty)
                .WithSource(sourceDefId ?? string.Empty, source ?? string.Empty));
        }

        /// <summary>
        /// 盘面拓扑/宿主移除后：对仍挂着「带条件的 Permanent Attack」修饰器的场上卡提交有效攻。
        /// </summary>
        public static void AppendConditionalPermanentAttackFaceCommitsForBoard(
            GameActionResult result,
            GameActionContext context,
            string actionName)
        {
            if (result == null || context == null)
            {
                return;
            }

            var board = context.GetModel<BoardModel>();
            var registry = context.GetModel<CardRegistry>();
            if (board == null || registry == null)
            {
                return;
            }

            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var uid = board.GetCardUid(SlotId.Board(i));
                CardInstance card;
                if (uid <= 0 || !registry.TryGet(uid, out card) || card == null)
                {
                    continue;
                }

                if (!HasConditionalPermanentAttackModifier(card))
                {
                    continue;
                }

                AppendPermanentAttackFaceCommit(result, context, card, actionName);
            }
        }

        public static bool HasConditionalPermanentAttackModifier(CardInstance card)
        {
            if (card == null || card.Stats == null)
            {
                return false;
            }

            var modifiers = card.Stats.Modifiers;
            for (var i = 0; i < modifiers.Count; i++)
            {
                var modifier = modifiers[i];
                if (modifier.Stat == StatId.Attack
                    && modifier.Scope == ModifierScope.Permanent
                    && modifier.Condition != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static StatEvaluationContext CreatePlayerOutgoingDamageContext(
            GameActionContext context,
            IStatSystem statSystem,
            CardInstance avatar)
        {
            var registry = context.GetModel<CardRegistry>();
            var board = context.GetModel<BoardModel>();
            if (registry == null || board == null || statSystem == null || avatar == null)
            {
                return null;
            }

            CardInstance proxyTarget = null;
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var uid = board.GetCardUid(SlotId.Board(i));
                CardInstance card;
                if (uid <= 0 || !registry.TryGet(uid, out card) || card == null || card.Kind != CardKind.Monster)
                {
                    continue;
                }

                proxyTarget = card;
                break;
            }

            if (proxyTarget == null)
            {
                return null;
            }

            return statSystem
                .CreateContext(proxyTarget)
                .WithActionSource("DealDamage", string.Empty, string.Empty)
                .WithActor(avatar.Uid);
        }
    }
}
