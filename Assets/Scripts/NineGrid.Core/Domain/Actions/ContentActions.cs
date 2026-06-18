using System;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;

namespace NineGrid.Core
{
    public sealed class ModifyBaseStatAction : GameAction
    {
        public ModifyBaseStatAction(int targetUid, StatId stat, int delta, string reason)
        {
            TargetUid = targetUid;
            Stat = stat;
            Delta = delta;
            Reason = reason ?? string.Empty;
        }

        public int TargetUid { get; private set; }
        public StatId Stat { get; private set; }
        public int Delta { get; private set; }
        public string Reason { get; private set; }
        public override string ActionName { get { return "ModifyBaseStat"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var card = context.GetModel<CardRegistry>().Get(TargetUid);
            var previous = (int)Math.Round(card.Stats.GetBase(Stat));
            var next = Math.Max(0, previous + Delta);
            card.Stats.SetBase(Stat, next);

            if (Stat == StatId.MaxHp && Delta > 0)
            {
                var hp = (int)Math.Round(card.Stats.GetBase(StatId.Hp));
                card.Stats.SetBase(StatId.Hp, hp + Delta);
            }

            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.BaseStatModified, context.ActionId, ActionName)
                    .WithCard(TargetUid)
                    .WithAmount((int)Stat)
                    .WithDelta(Delta)
                    .WithMessage(Reason));
        }
    }

    public sealed class GrantRelicAction : GameAction
    {
        public GrantRelicAction(string relicDefId)
        {
            RelicDefId = relicDefId ?? string.Empty;
        }

        public string RelicDefId { get; private set; }
        public override string ActionName { get { return "GrantRelic"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            context.GetModel<PlayerModel>().AddRelic(RelicDefId);
            context.GetSystem<IContentSystem>().ActivateRelic(RelicDefId);
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.RelicGranted, context.ActionId, ActionName)
                    .WithMessage(RelicDefId));
        }
    }

    public sealed class GrantPlayerSkillContentAction : GameAction
    {
        public GrantPlayerSkillContentAction(string skillDefId)
        {
            SkillDefId = skillDefId ?? string.Empty;
        }

        public string SkillDefId { get; private set; }
        public override string ActionName { get { return "GrantPlayerSkillContent"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            context.GetModel<PlayerModel>().AddSkill(SkillDefId);
            context.GetSystem<IContentSystem>().ActivatePlayerSkill(SkillDefId);
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.SkillGranted, context.ActionId, ActionName)
                    .WithMessage(SkillDefId));
        }
    }

    public sealed class OfferRewardChoiceAction : GameAction
    {
        public OfferRewardChoiceAction(string poolId, int optionCount)
        {
            PoolId = poolId ?? string.Empty;
            OptionCount = optionCount;
        }

        public string PoolId { get; private set; }
        public int OptionCount { get; private set; }
        public override string ActionName { get { return "OfferRewardChoice"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.RewardOffered, context.ActionId, ActionName)
                    .WithAmount(OptionCount)
                    .WithMessage(PoolId));
        }
    }

    public sealed class ResolveRoomAction : GameAction
    {
        public ResolveRoomAction(RoomKind roomKind, string displayName)
        {
            RoomKind = roomKind;
            DisplayName = displayName ?? string.Empty;
        }

        public RoomKind RoomKind { get; private set; }
        public string DisplayName { get; private set; }
        public override string ActionName { get { return "ResolveRoom"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            context.GetModel<RunModel>().Room.Value = RoomKind;
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.RoomResolved, context.ActionId, ActionName)
                    .WithAmount((int)RoomKind)
                    .WithMessage(DisplayName));
        }
    }
}
