using System;
using System.Collections.Generic;
using NineGrid.Core.Effects;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;

namespace NineGrid.Core
{
    public sealed class DealDamageAction : GameAction
    {
        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnBattle,
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

            var statSystem = context.GetSystem<IStatSystem>();
            var statContext = statSystem.CreateContext(target);
            var baseDamage = Math.Max(0, Amount);
            var damage = Math.Max(0, (int)Math.Round(statSystem.EvaluateRule(RuleId.DamageMultiplier, baseDamage, statContext)));
            if (baseDamage > 0)
            {
                var consumed = new List<RuleModifier>();
                statSystem.RuleModifiers.Consume(RuleId.DamageMultiplier, ModifierScope.Once, statContext, consumed);
                DeactivateConsumedEffects(context, consumed);
            }

            var armor = Math.Max(0, (int)Math.Round(target.Stats.GetBase(StatId.Armor)));
            var hp = Math.Max(0, (int)Math.Round(target.Stats.GetBase(StatId.Hp)));
            var armorLoss = Math.Min(armor, damage);
            var hpLoss = Math.Min(hp, damage - armorLoss);
            var newArmor = armor - armorLoss;
            var newHp = hp - hpLoss;

            target.Stats.SetBase(StatId.Armor, newArmor);
            target.Stats.SetBase(StatId.Hp, newHp);

            var result = new GameActionResult();
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

            if (newHp <= 0 && target.Kind != CardKind.Avatar)
            {
                result.AddFollowUp(new KillIfDeadAction(ActorUid, TargetUid));
            }

            return result;
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
            return sPostTriggers;
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
            var maxHp = Math.Max(0, (int)Math.Round(target.Stats.GetBase(StatId.MaxHp)));
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
                    .WithRemaining(newHp, (int)Math.Round(target.Stats.GetBase(StatId.Armor)))
                    .WithSource(SourceDefId, Cause))
                .AddEvent(new CoreGameEvent(CoreEventType.HpChanged, context.ActionId, ActionName)
                    .WithActor(ActorUid)
                    .WithTarget(TargetUid)
                    .WithCard(TargetUid)
                    .WithDelta(healed)
                    .WithRemaining(newHp, (int)Math.Round(target.Stats.GetBase(StatId.Armor)))
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
            var armor = Math.Max(0, (int)Math.Round(target.Stats.GetBase(StatId.Armor)));
            var delta = Math.Max(0, Amount);
            var newArmor = armor + delta;
            target.Stats.SetBase(StatId.Armor, newArmor);

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
            var fromSlot = card.Slot.Value;

            board.RemoveCard(card);
            deck.RemoveCard(card);
            card.Zone.Value = DestinationZone;
            card.Slot.Value = SlotId.None;

            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.CardRemoved, context.ActionId, ActionName)
                    .WithCard(CardUid)
                    .WithSlots(fromSlot, SlotId.None)
                    .WithMessage(Reason)
                    .WithSource(SourceDefId, Reason));
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
                    .WithSlots(fromSlot, SlotId.None))
                .AddEvent(new CoreGameEvent(CoreEventType.CardRemoved, context.ActionId, ActionName)
                    .WithActor(KillerUid)
                    .WithTarget(TargetUid)
                    .WithCard(TargetUid)
                    .WithSlots(fromSlot, SlotId.None)
                    .WithMessage("kill"));

            var goldReward = target.Counters.Get(CoreCounterKeys.GoldReward);
            if (goldReward != 0)
            {
                result.AddFollowUp(new ModifyGoldAction(goldReward, "kill:" + target.DefId));
            }

            return result;
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }
    }
}
