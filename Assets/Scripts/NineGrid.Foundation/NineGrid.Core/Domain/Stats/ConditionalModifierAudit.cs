using System;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;

namespace NineGrid.Core
{
    /// <summary>
    /// 条件修饰符审计缝（诊断）：动作边界采样盘面九格 + Avatar 上所有带条件的
    /// <see cref="StatModifier"/> 的激活态，首见（route=initial）与翻转（route=flip）时
    /// 各落一条 <see cref="CoreEventType.ConditionalModifierAudited"/> 事件。
    /// <para>
    /// 背景：Conditional 层修饰符按查询惰性求值，本身不发任何事件——「遗物挂上了
    /// 但条件从未满足 / 激活后又悄然失活」在日志里完全隐形（血液暴力 HpBelow 排查痛点）。
    /// 本缝让 corelog 能直接回答：条件修饰符何时激活、何时失活、从未激活。
    /// </para>
    /// <para>只审计卡上的 StatModifier；全局 RuleModifier 的条件依赖调用方 Owner/Target
    /// 语境，离开真实查询采样会给出误导结果，不纳入。只读诊断，异常一律吞掉。</para>
    /// </summary>
    public static class ConditionalModifierAudit
    {
        public const string CauseInitial = "initial";
        public const string CauseFlip = "flip";

        public static void AuditAfterAction(GameActionContext context, EventLog log, string actionName)
        {
            if (context == null || log == null)
            {
                return;
            }

            try
            {
                var board = context.GetModel<BoardModel>();
                var registry = context.GetModel<CardRegistry>();
                var stats = context.GetSystem<IStatSystem>();
                if (board == null || registry == null || stats == null)
                {
                    return;
                }

                var avatarUid = board.AvatarUid.Value;
                for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
                {
                    var uid = board.GetCardUid(SlotId.Board(i));
                    CardInstance card;
                    if (uid <= 0 || uid == avatarUid || !registry.TryGet(uid, out card) || card == null)
                    {
                        continue;
                    }

                    AuditCard(context, log, stats, card, actionName);
                }

                CardInstance avatar;
                if (avatarUid > 0 && registry.TryGet(avatarUid, out avatar) && avatar != null)
                {
                    AuditCard(context, log, stats, avatar, actionName);
                }
            }
            catch (Exception)
            {
                // 诊断缝不得干扰游戏路径。
            }
        }

        private static void AuditCard(
            GameActionContext context,
            EventLog log,
            IStatSystem stats,
            CardInstance card,
            string actionName)
        {
            var modifiers = card.Stats.Modifiers;
            StatEvaluationContext evalContext = null;
            for (var i = 0; i < modifiers.Count; i++)
            {
                var modifier = modifiers[i];
                if (modifier == null || modifier.Condition == null)
                {
                    continue;
                }

                evalContext = evalContext ?? stats.CreateContext(card);
                var active = modifier.IsActive(evalContext);
                var last = modifier.DiagLastActive;
                if (last != null && last.Value == active)
                {
                    continue;
                }

                modifier.DiagLastActive = active;
                log.Append(BuildEvent(context, actionName, card, evalContext, modifier, active, initial: last == null));
            }
        }

        private static CoreGameEvent BuildEvent(
            GameActionContext context,
            string actionName,
            CardInstance card,
            StatEvaluationContext evalContext,
            StatModifier modifier,
            bool active,
            bool initial)
        {
            // hp/effMaxHp 快照覆盖最常见的 HpBelow 类条件语境，其他条件也能借此定位时点。
            var hp = (int)Math.Round(card.Stats.GetBase(StatId.Hp));
            var effectiveMaxHp = (int)Math.Round(StatConditionMaxHp.GetEffectiveMaxHp(evalContext));
            var detail = modifier.Stat + " " + modifier.Op + " " + modifier.Value
                + " @" + modifier.Layer
                + " active=" + (active ? "1" : "0")
                + " hp=" + hp + "/" + effectiveMaxHp;

            return new CoreGameEvent(CoreEventType.ConditionalModifierAudited, context.ActionId, actionName)
                .WithCard(card.Uid)
                .WithTarget(card.Uid)
                .WithAmount((int)modifier.Stat)
                .WithDelta(initial ? 0 : (active ? 1 : -1))
                .WithResultValue(active ? 1 : 0)
                .WithMessage(detail)
                .WithSource(modifier.Source.Id, initial ? CauseInitial : CauseFlip);
        }
    }
}
