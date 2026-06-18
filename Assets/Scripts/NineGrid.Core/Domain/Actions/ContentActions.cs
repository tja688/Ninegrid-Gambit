using System;
using System.Collections.Generic;
using System.Text;
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
            else if (Stat == StatId.MaxHp && Delta < 0)
            {
                var hp = (int)Math.Round(card.Stats.GetBase(StatId.Hp));
                card.Stats.SetBase(StatId.Hp, Math.Min(hp, next));
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

    public sealed class GrantRewardFromPoolAction : GameAction
    {
        public GrantRewardFromPoolAction(string poolId)
        {
            PoolId = poolId ?? string.Empty;
        }

        public string PoolId { get; private set; }
        public override string ActionName { get { return "GrantRewardFromPool"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var offered = context.GetSystem<IRewardSystem>().RollPool(PoolId);
            var result = new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.RewardOffered, context.ActionId, ActionName)
                    .WithAmount(offered.Count)
                    .WithMessage(FormatOfferedRewards(PoolId, offered)));

            for (var i = 0; i < offered.Count; i++)
            {
                AddGrantFollowUp(result, offered[i]);
            }

            return result;
        }

        private static void AddGrantFollowUp(GameActionResult result, RewardEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.DefId))
            {
                return;
            }

            for (var i = 0; i < entry.Count; i++)
            {
                if (entry.Kind == CardKind.Relic)
                {
                    result.AddFollowUp(new GrantRelicAction(entry.DefId));
                }
                else if (entry.Kind == CardKind.HelpCard)
                {
                    result.AddFollowUp(new ShuffleIntoDrawPileAction(entry.DefId, entry.Kind, 1, false));
                }
                else if (entry.Kind == CardKind.PlayerCard)
                {
                    result.AddFollowUp(new GrantPlayerSkillContentAction(entry.DefId));
                }
            }
        }

        private static string FormatOfferedRewards(string poolId, IReadOnlyList<RewardEntry> offered)
        {
            var builder = new StringBuilder(poolId ?? string.Empty);
            builder.Append("|");
            for (var i = 0; i < offered.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(",");
                }

                builder.Append(offered[i].DefId);
                builder.Append(":");
                builder.Append(offered[i].Kind);
                builder.Append(":");
                builder.Append(offered[i].Count);
            }

            return builder.ToString();
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
            var offered = context.GetSystem<IRewardSystem>().RollPool(PoolId);
            var amount = offered.Count > 0 ? offered.Count : OptionCount;
            var message = offered.Count > 0 ? FormatOfferedRewards(PoolId, offered) : PoolId;
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.RewardOffered, context.ActionId, ActionName)
                    .WithAmount(amount)
                    .WithMessage(message));
        }

        private static string FormatOfferedRewards(string poolId, IReadOnlyList<RewardEntry> offered)
        {
            var builder = new StringBuilder(poolId ?? string.Empty);
            builder.Append("|");
            for (var i = 0; i < offered.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(",");
                }

                builder.Append(offered[i].DefId);
                builder.Append(":");
                builder.Append(offered[i].Kind);
                builder.Append(":");
                builder.Append(offered[i].Count);
            }

            return builder.ToString();
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
