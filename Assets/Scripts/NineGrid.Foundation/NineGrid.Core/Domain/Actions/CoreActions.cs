using System;
using System.Collections.Generic;
using NineGrid.Core.Effects;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;

namespace NineGrid.Core
{
    public sealed class DealDamageAction : GameAction
    {
        private static readonly TriggerPoint[] sPostTriggersInEngagement =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnBattle,
            TriggerPoint.OnDamage,
            TriggerPoint.OnArmorBreak,
            TriggerPoint.OnDamageTaken,
            TriggerPoint.OnFatalDamage,
            TriggerPoint.OnCumulative
        };

        private static readonly TriggerPoint[] sPostTriggersOutsideEngagement =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnDamage,
            TriggerPoint.OnArmorBreak,
            TriggerPoint.OnDamageTaken,
            TriggerPoint.OnFatalDamage,
            TriggerPoint.OnCumulative
        };

        public DealDamageAction(int actorUid, int targetUid, int amount, string sourceDefId = null, string cause = null)
        {
            ActorUid = actorUid;
            TargetUid = targetUid;
            Amount = amount;
            SourceDefId = sourceDefId ?? string.Empty;
            Cause = cause ?? string.Empty;
        }

        public int ActorUid { get; private set; }
        public int TargetUid { get; private set; }
        public int Amount { get; private set; }
        public string SourceDefId { get; private set; }
        public string Cause { get; private set; }
        public override string ActionName { get { return "DealDamage"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var registry = context.GetModel<CardRegistry>();
            var target = registry.Get(TargetUid);
            if (target.Zone.Value == ZoneId.Graveyard || target.Zone.Value == ZoneId.Removed)
            {
                return GameActionResult.Empty;
            }

            // ADR-0016：背面不可被伤害（含爆弹 AllMonsters 等范围伤）；Avatar 恒正面。
            if (!target.FaceUp)
            {
                return GameActionResult.Empty;
            }

            var statSystem = context.GetSystem<IStatSystem>();
            var statContext = statSystem.CreateContext(target)
                .WithActionSource(ActionName, SourceDefId, Cause)
                .WithActor(ActorUid);

            // ADR-0026 / #111：门 — 仅交战中玩家出手可伤。
            if (statSystem.EvaluateRule(RuleId.DoorProtection, 0f, statContext) > 0f
                && !IsEngagementPlayerHit(context, ActorUid))
            {
                return GameActionResult.Empty;
            }

            var baseDamage = Math.Max(0, Amount);
            var multipliedDamage = Math.Max(0, (int)Math.Round(statSystem.EvaluateRule(RuleId.DamageMultiplier, baseDamage, statContext)));
            var flatDamage = baseDamage > 0 ? (int)Math.Round(statSystem.EvaluateRule(RuleId.DamageFlatDelta, 0f, statContext)) : 0;
            var damage = Math.Max(0, multipliedDamage + flatDamage);
            if (baseDamage > 0)
            {
                var consumed = new List<RuleModifier>();
                statSystem.RuleModifiers.Consume(RuleId.DamageMultiplier, ModifierScope.Once, statContext, consumed);
                DeactivateConsumedEffects(context, consumed);
            }

            var armor = StatArmorUtility.GetCurrentArmor(target);
            var hp = Math.Max(0, (int)Math.Round(target.Stats.GetBase(StatId.Hp)));
            var armorDamage = Math.Min(armor, damage);
            var goldAbsorbed = 0;
            if (target.Kind == CardKind.Avatar && armor > 0 && damage > 0 && statSystem.EvaluateRule(RuleId.GoldArmorAbsorb, 0f, statContext) > 0f)
            {
                goldAbsorbed = AbsorbArmorDamageWithGold(context, armorDamage);
            }

            var armorLoss = armorDamage - goldAbsorbed;
            var hpLoss = Math.Min(hp, Math.Max(0, damage - armor));
            var newArmor = armor - armorLoss;
            var newHp = hp - hpLoss;

            StatArmorUtility.SetCurrentArmor(target, newArmor);
            target.Stats.SetBase(StatId.Hp, newHp);

            var result = new GameActionResult();
            if (goldAbsorbed > 0)
            {
                result.AddEvent(new CoreGameEvent(CoreEventType.GoldModified, context.ActionId, ActionName)
                    .WithActor(ActorUid)
                    .WithTarget(TargetUid)
                    .WithCard(TargetUid)
                    .WithAmount(context.GetModel<PlayerModel>().Coins.Value)
                    .WithDelta(-goldAbsorbed * 5)
                    .WithMessage("goldArmor")
                    .WithSource(SourceDefId, Cause));
            }

            if (armorLoss > 0)
            {
                result.AddEvent(new CoreGameEvent(CoreEventType.ArmorChanged, context.ActionId, ActionName)
                    .WithActor(ActorUid)
                    .WithTarget(TargetUid)
                    .WithCard(TargetUid)
                    .WithDelta(-armorLoss)
                    .WithRemaining(newHp, newArmor)
                    .WithSource(SourceDefId, Cause));
            }

            if (hpLoss > 0)
            {
                result.AddEvent(new CoreGameEvent(CoreEventType.HpChanged, context.ActionId, ActionName)
                    .WithActor(ActorUid)
                    .WithTarget(TargetUid)
                    .WithCard(TargetUid)
                    .WithDelta(-hpLoss)
                    .WithRemaining(newHp, newArmor)
                    .WithSource(SourceDefId, Cause));
            }

            result.AddEvent(new CoreGameEvent(CoreEventType.DamageDealt, context.ActionId, ActionName)
                .WithActor(ActorUid)
                .WithTarget(TargetUid)
                .WithCard(TargetUid)
                .WithAmount(damage)
                .WithDelta(armorLoss + hpLoss)
                .WithRemaining(newHp, newArmor)
                .WithSource(SourceDefId, Cause));

            if (newHp <= 0 && target.Kind == CardKind.Avatar)
            {
                result.AddFollowUp(new DefeatIfAvatarDeadAction());
            }
            else if (newHp <= 0)
            {
                result.AddFollowUp(new KillIfDeadAction(ActorUid, TargetUid));
            }

            return result;
        }

        private static int AbsorbArmorDamageWithGold(GameActionContext context, int maxArmorDamage)
        {
            var player = context.GetModel<PlayerModel>();
            var goldToSpend = Math.Min(player.Coins.Value / 5, maxArmorDamage);
            if (goldToSpend <= 0)
            {
                return 0;
            }

            player.AddCoins(-goldToSpend * 5);
            return goldToSpend;
        }

        private static void DeactivateConsumedEffects(GameActionContext context, IReadOnlyList<RuleModifier> consumed)
        {
            if (consumed == null || consumed.Count == 0)
            {
                return;
            }

            var effectSystem = context.GetSystem<IEffectSystem>();
            for (var i = 0; i < consumed.Count; i++)
            {
                var source = consumed[i].Source.Id;
                if (string.IsNullOrEmpty(source) || !source.StartsWith("effect:", StringComparison.Ordinal))
                {
                    continue;
                }

                effectSystem.Deactivate(source.Substring("effect:".Length));
            }
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            // #78 / ADR-0012：OnBattle 仅交战作用域（Begin…End）内 raise；单向打击等非交战伤害不触发。
            var scope = context.GetSystem<IBattleScopeSystem>();
            return scope != null && scope.IsEngagementActive
                ? sPostTriggersInEngagement
                : sPostTriggersOutsideEngagement;
        }

        private static bool IsEngagementPlayerHit(GameActionContext context, int actorUid)
        {
            var scope = context.GetSystem<IBattleScopeSystem>();
            if (scope == null || !scope.IsEngagementActive || actorUid == 0)
            {
                return false;
            }

            var board = context.GetModel<BoardModel>();
            return board != null && actorUid == board.AvatarUid.Value;
        }
    }

    public sealed class HealAction : GameAction
    {
        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnHeal
        };

        public HealAction(int actorUid, int targetUid, int amount, string sourceDefId = null, string cause = null)
        {
            ActorUid = actorUid;
            TargetUid = targetUid;
            Amount = amount;
            SourceDefId = sourceDefId ?? string.Empty;
            Cause = cause ?? string.Empty;
        }

        public int ActorUid { get; private set; }
        public int TargetUid { get; private set; }
        public int Amount { get; private set; }
        public string SourceDefId { get; private set; }
        public string Cause { get; private set; }
        public override string ActionName { get { return "Heal"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var registry = context.GetModel<CardRegistry>();
            var statSystem = context.GetSystem<IStatSystem>();
            var target = registry.Get(TargetUid);
            var maxHp = Math.Max(0, (int)Math.Round(statSystem.GetEffectiveValue(target, StatId.MaxHp)));
            var hp = Math.Max(0, (int)Math.Round(target.Stats.GetBase(StatId.Hp)));
            var modifiedAmount = (int)Math.Round(statSystem.EvaluateRule(RuleId.RecoveryMultiplier, Math.Max(0, Amount), statSystem.CreateContext(target)));
            var healed = Math.Max(0, Math.Min(modifiedAmount, maxHp - hp));
            var newHp = hp + healed;
            target.Stats.SetBase(StatId.Hp, newHp);

            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.Healed, context.ActionId, ActionName)
                    .WithActor(ActorUid)
                    .WithTarget(TargetUid)
                    .WithCard(TargetUid)
                    .WithAmount(modifiedAmount)
                    .WithDelta(healed)
                    .WithRemaining(newHp, StatArmorUtility.GetCurrentArmor(target))
                    .WithSource(SourceDefId, Cause))
                .AddEvent(new CoreGameEvent(CoreEventType.HpChanged, context.ActionId, ActionName)
                    .WithActor(ActorUid)
                    .WithTarget(TargetUid)
                    .WithCard(TargetUid)
                    .WithDelta(healed)
                    .WithRemaining(newHp, StatArmorUtility.GetCurrentArmor(target))
                    .WithSource(SourceDefId, Cause));
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }
    }

    public sealed class GainArmorAction : GameAction
    {
        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnArmorGained
        };

        public GainArmorAction(int targetUid, int amount, string sourceDefId = null, string cause = null)
        {
            TargetUid = targetUid;
            Amount = amount;
            SourceDefId = sourceDefId ?? string.Empty;
            Cause = cause ?? string.Empty;
        }

        public int TargetUid { get; private set; }
        public int Amount { get; private set; }
        public string SourceDefId { get; private set; }
        public string Cause { get; private set; }
        public override string ActionName { get { return "GainArmor"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var target = context.GetModel<CardRegistry>().Get(TargetUid);
            var armor = StatArmorUtility.GetCurrentArmor(target);
            var delta = Math.Max(0, Amount);
            var newArmor = armor + delta;
            StatArmorUtility.SetCurrentArmor(target, newArmor);

            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.ArmorChanged, context.ActionId, ActionName)
                    .WithTarget(TargetUid)
                    .WithCard(TargetUid)
                    .WithDelta(delta)
                    .WithRemaining((int)Math.Round(target.Stats.GetBase(StatId.Hp)), newArmor)
                    .WithSource(SourceDefId, Cause));
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }
    }

    public sealed class TransferArmorAction : GameAction
    {
        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnArmorBreak,
            TriggerPoint.OnArmorGained,
            TriggerPoint.OnCumulative
        };

        public TransferArmorAction(
            int sourceUid,
            int receiverUid,
            int amount,
            bool transferAll,
            string sourceDefId = null,
            string cause = null)
        {
            SourceUid = sourceUid;
            ReceiverUid = receiverUid;
            Amount = amount;
            TransferAll = transferAll;
            SourceDefId = sourceDefId ?? string.Empty;
            Cause = cause ?? string.Empty;
        }

        public int SourceUid { get; private set; }
        public int ReceiverUid { get; private set; }
        public int Amount { get; private set; }
        public bool TransferAll { get; private set; }
        public string SourceDefId { get; private set; }
        public string Cause { get; private set; }
        public override string ActionName { get { return "TransferArmor"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var registry = context.GetModel<CardRegistry>();
            CardInstance source;
            if (!registry.TryGet(SourceUid, out source))
            {
                return GameActionResult.Empty;
            }

            var sourceArmor = StatArmorUtility.GetCurrentArmor(source);
            var requested = TransferAll ? sourceArmor : Math.Max(0, Amount);
            var delta = Math.Min(sourceArmor, requested);
            if (delta <= 0)
            {
                return GameActionResult.Empty;
            }

            var sourceHp = Math.Max(0, (int)Math.Round(source.Stats.GetBase(StatId.Hp)));
            var sourceNewArmor = sourceArmor - delta;
            StatArmorUtility.SetCurrentArmor(source, sourceNewArmor);

            var result = new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.ArmorChanged, context.ActionId, ActionName)
                    .WithActor(ReceiverUid)
                    .WithTarget(SourceUid)
                    .WithCard(SourceUid)
                    .WithDelta(-delta)
                    .WithRemaining(sourceHp, sourceNewArmor)
                    .WithSource(SourceDefId, Cause));

            CardInstance receiver;
            if (ReceiverUid != 0 && registry.TryGet(ReceiverUid, out receiver))
            {
                var receiverArmor = StatArmorUtility.GetCurrentArmor(receiver);
                var receiverHp = Math.Max(0, (int)Math.Round(receiver.Stats.GetBase(StatId.Hp)));
                var receiverNewArmor = receiverArmor + delta;
                StatArmorUtility.SetCurrentArmor(receiver, receiverNewArmor);
                result.AddEvent(new CoreGameEvent(CoreEventType.ArmorChanged, context.ActionId, ActionName)
                    .WithActor(SourceUid)
                    .WithTarget(ReceiverUid)
                    .WithCard(ReceiverUid)
                    .WithDelta(delta)
                    .WithRemaining(receiverHp, receiverNewArmor)
                    .WithSource(SourceDefId, Cause));
            }

            return result;
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }
    }

    public sealed class ModifyGoldAction : GameAction
    {
        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnGoldChanged
        };

        public ModifyGoldAction(int delta, string reason, string sourceDefId = null)
        {
            Delta = delta;
            Reason = reason ?? string.Empty;
            SourceDefId = sourceDefId ?? string.Empty;
        }

        public int Delta { get; private set; }
        public string Reason { get; private set; }
        public string SourceDefId { get; private set; }
        public override string ActionName { get { return "ModifyGold"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var player = context.GetModel<PlayerModel>();
            player.AddCoins(Delta);

            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.GoldModified, context.ActionId, ActionName)
                    .WithDelta(Delta)
                    .WithAmount(player.Coins.Value)
                    .WithMessage(Reason)
                    .WithSource(SourceDefId, Reason));
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }
    }

    public sealed class RemoveCardAction : GameAction
    {
        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnRemove
        };

        public RemoveCardAction(int cardUid, ZoneId destinationZone, string reason, string sourceDefId = null)
        {
            CardUid = cardUid;
            DestinationZone = destinationZone;
            Reason = reason ?? string.Empty;
            SourceDefId = sourceDefId ?? string.Empty;
        }

        public int CardUid { get; private set; }
        public ZoneId DestinationZone { get; private set; }
        public string Reason { get; private set; }
        public string SourceDefId { get; private set; }
        public override string ActionName { get { return "RemoveCard"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var registry = context.GetModel<CardRegistry>();
            var board = context.GetModel<BoardModel>();
            var deck = context.GetModel<DeckModel>();
            var card = registry.Get(CardUid);

            // ADR-0026 / #111：门 — 直接移除/放逐无效（Kill 不经此 Action）。
            var statSystem = context.GetSystem<IStatSystem>();
            if (statSystem.EvaluateRule(RuleId.DoorProtection, 0f, statSystem.CreateContext(card)) > 0f)
            {
                return GameActionResult.Empty;
            }

            var fromSlot = card.Slot.Value;
            var removedAttack = (int)Math.Round(card.Stats.GetBase(StatId.Attack));
            var removedArmor = StatArmorUtility.GetCurrentArmor(card);

            board.RemoveCard(card);
            deck.RemoveCard(card);
            card.Zone.Value = DestinationZone;
            card.Slot.Value = SlotId.None;

            var result = new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.CardRemoved, context.ActionId, ActionName)
                    .WithCard(CardUid)
                    .WithSlots(fromSlot, SlotId.None)
                    .WithRemovedStats(removedAttack, removedArmor)
                    .WithMessage(Reason)
                    .WithSource(SourceDefId, Reason))
                .AddFollowUp(new DeactivateOwnerEffectsAction(CardUid, "remove:" + Reason));
            CardFaceEventValues.AppendConditionalPermanentAttackFaceCommitsForBoard(
                result,
                context,
                ActionName);
            return result;
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }
    }

    public sealed class KillAction : GameAction
    {
        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnKill,
            TriggerPoint.OnRemove
        };

        public KillAction(int killerUid, int targetUid)
        {
            KillerUid = killerUid;
            TargetUid = targetUid;
        }

        public int KillerUid { get; private set; }
        public int TargetUid { get; private set; }
        public override string ActionName { get { return "Kill"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var registry = context.GetModel<CardRegistry>();
            var board = context.GetModel<BoardModel>();
            var deck = context.GetModel<DeckModel>();
            var target = registry.Get(TargetUid);
            if (target.Zone.Value == ZoneId.Graveyard || target.Zone.Value == ZoneId.Removed)
            {
                return GameActionResult.Empty;
            }

            var fromSlot = target.Slot.Value;
            var removedAttack = (int)Math.Round(target.Stats.GetBase(StatId.Attack));
            var removedArmor = StatArmorUtility.GetCurrentArmor(target);
            target.Stats.SetBase(StatId.Hp, 0);
            board.RemoveCard(target);
            deck.RemoveCard(target);
            target.Zone.Value = ZoneId.Graveyard;
            target.Slot.Value = SlotId.None;

            var result = new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.CardKilled, context.ActionId, ActionName)
                    .WithActor(KillerUid)
                    .WithTarget(TargetUid)
                    .WithCard(TargetUid)
                    .WithSlots(fromSlot, SlotId.None)
                    .WithRemaining(0, removedArmor))
                .AddEvent(new CoreGameEvent(CoreEventType.CardRemoved, context.ActionId, ActionName)
                    .WithActor(KillerUid)
                    .WithTarget(TargetUid)
                    .WithCard(TargetUid)
                    .WithSlots(fromSlot, SlotId.None)
                    .WithRemovedStats(removedAttack, removedArmor)
                    .WithMessage("kill"));

            var goldReward = target.Counters.Get(CoreCounterKeys.GoldReward);
            result.AddFollowUp(new DeactivateOwnerEffectsAction(TargetUid, "kill"));
            // ADR-0017：Trap 无击杀赏金（含显式 GoldReward 计数器）。
            if (goldReward != 0 && CardCombatRules.IsTrueMonster(target.Kind))
            {
                result.AddFollowUp(new ModifyGoldAction(goldReward, "kill:" + target.DefId));
            }

            CardFaceEventValues.AppendConditionalPermanentAttackFaceCommitsForBoard(
                result,
                context,
                ActionName);
            return result;
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }
    }
}
