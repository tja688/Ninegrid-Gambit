using System;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;

namespace NineGrid.Core
{
    /// <summary>
    /// 卡面数值的统一有效值口径（oracle）与生成类事件绝对值写入。
    /// <para>
    /// 生成类事件（CardSpawned / 带 uid 的 CardDealt / AvatarAppeared）在造卡 / 发牌时写入
    /// 攻=ResultValue、血/甲=Remaining*（ADR-0005）；参与敌方行动的怪物另附行动倒计时事件。
    /// </para>
    /// <para>
    /// 生成之后的卡面数值完整性由 Core 统一对账缝自动保证（<see cref="CardFaceReconciliation"/>，
    /// ADR-0045）：任何动作改动有效攻 / 当前甲 / 血 / 倒计时，都会在动作边界 diff-emit 绝对值
    /// 提交事件，无需手工补发。历史上的 Append*FaceCommit 补扫家族已废除。
    /// </para>
    /// </summary>
    public static class CardFaceEventValues
    {
        /// <summary>
        /// 卡面攻与伤害结算共用的唯一口径：有效攻；怪物另加 <see cref="RuleId.EnemyAttackDelta"/>
        /// 规则修正（龙鳞甲全场-1、邻接光环+1 等）。PhaseSystem.GetAttackDamage 直接复用本函数。
        /// </summary>
        public static int GetFaceAttack(IStatSystem stats, CardInstance card)
        {
            if (card == null)
            {
                return 0;
            }

            if (stats == null)
            {
                return Math.Max(0, (int)card.Stats.GetBase(StatId.Attack));
            }

            var attack = stats.GetEffectiveInt(card, StatId.Attack);
            if (card.Kind == CardKind.Monster)
            {
                attack += (int)Math.Round(
                    stats.EvaluateRule(RuleId.EnemyAttackDelta, 0f, stats.CreateContext(card)));
            }

            return Math.Max(0, attack);
        }

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
            var attack = GetFaceAttack(stats, card);
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
        /// 盘面无怪或规则不改伤时退化为有效攻；也是玩家卡面攻的对账 oracle（ADR-0045）。
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
