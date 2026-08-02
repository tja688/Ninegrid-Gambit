using System;
using System.Collections.Generic;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;

namespace NineGrid.Core
{
    public sealed class ModifyBaseStatAction : GameAction
    {
        public ModifyBaseStatAction(int targetUid, StatId stat, int delta, string reason, string sourceDefId = null)
        {
            TargetUid = targetUid;
            Stat = stat;
            Delta = delta;
            Reason = reason ?? string.Empty;
            SourceDefId = sourceDefId ?? string.Empty;
        }

        public int TargetUid { get; private set; }
        public StatId Stat { get; private set; }
        public int Delta { get; private set; }
        public string Reason { get; private set; }
        public string SourceDefId { get; private set; }
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
                    .WithTarget(TargetUid)
                    .WithAmount((int)Stat)
                    .WithDelta(Delta)
                    .WithResultValue(next)
                    .WithMessage(Reason)
                    .WithSource(SourceDefId, Reason));
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

    public sealed class GrantHelpCardToPlayerSideDeckAction : GameAction
    {
        public GrantHelpCardToPlayerSideDeckAction(string defId, int count = 1)
        {
            DefId = defId ?? string.Empty;
            Count = count < 1 ? 1 : count;
        }

        public string DefId { get; private set; }
        public int Count { get; private set; }
        public override string ActionName { get { return "GrantHelpCardToPlayerSideDeck"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            context.GetModel<PlayerModel>().AddToCarryPack(DefId, Count);
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.RewardSelected, context.ActionId, ActionName)
                    .WithAmount(Count)
                    .WithMessage(DefId + ":HelpCard:" + Count));
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
            var offered = RewardOfferFaceProjection.Project(context, context.GetSystem<IRewardSystem>().RollPool(PoolId));
            var result = new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.RewardOffered, context.ActionId, ActionName)
                    .WithAmount(offered.Count)
                    .WithMessage(RewardOfferFaceEncoding.Format(PoolId, offered)));

            var toCarryPack = HelpCardGrantRouting.ShouldGrantToCarryPack(context);
            for (var i = 0; i < offered.Count; i++)
            {
                RewardGrantActionSupport.AddGrantFollowUp(result, offered[i], toCarryPack);
            }

            return result;
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
            var offered = RewardOfferFaceProjection.Project(context, context.GetSystem<IRewardSystem>().RollPool(PoolId));
            var amount = offered.Count > 0 ? offered.Count : OptionCount;
            var message = offered.Count > 0 ? RewardOfferFaceEncoding.Format(PoolId, offered) : PoolId;
            context.GetModel<PendingChoiceModel>().OfferRewards(PoolId, offered);
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.RewardOffered, context.ActionId, ActionName)
                    .WithAmount(amount)
                    .WithMessage(message));
        }
    }

    /// <summary>商店四货架会话（#92）：固定角色货架 + 本次进店刷新价。</summary>
    public sealed class OfferShopSessionAction : GameAction
    {
        public const int DefaultRefreshPriceGold = 10;

        public OfferShopSessionAction(IReadOnlyList<RewardEntry> shelves, int refreshPriceGold)
        {
            Shelves = shelves;
            RefreshPriceGold = refreshPriceGold < 0 ? 0 : refreshPriceGold;
        }

        public IReadOnlyList<RewardEntry> Shelves { get; private set; }
        public int RefreshPriceGold { get; private set; }
        public override string ActionName { get { return "OfferShopSession"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var projected = RewardOfferFaceProjection.Project(context, Shelves);
            context.GetModel<PendingChoiceModel>().OfferShop(projected, RefreshPriceGold);
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.RewardOffered, context.ActionId, ActionName)
                    .WithAmount(projected != null ? projected.Count : 0)
                    .WithMessage(RewardOfferFaceEncoding.Format(PendingChoiceModel.ShopPoolId, projected)));
        }
    }

    /// <summary>卡店三项服务会话（#93）：就地选项 + 本次进店刷新价。</summary>
    public sealed class OfferTavernSessionAction : GameAction
    {
        public OfferTavernSessionAction(IReadOnlyList<RewardEntry> services, int refreshPriceGold)
        {
            Services = services;
            RefreshPriceGold = refreshPriceGold < 0 ? 0 : refreshPriceGold;
        }

        public IReadOnlyList<RewardEntry> Services { get; private set; }
        public int RefreshPriceGold { get; private set; }
        public override string ActionName { get { return "OfferTavernSession"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var projected = RewardOfferFaceProjection.Project(context, Services);
            context.GetModel<PendingChoiceModel>().OfferTavern(projected, RefreshPriceGold);
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.RewardOffered, context.ActionId, ActionName)
                    .WithAmount(projected != null ? projected.Count : 0)
                    .WithMessage(RewardOfferFaceEncoding.Format(PendingChoiceModel.TavernPoolId, projected)));
        }
    }

    /// <summary>卡店「道具卡固定」二级选择候选（#93）。</summary>
    public sealed class OfferTavernFixItemAction : GameAction
    {
        public OfferTavernFixItemAction(IReadOnlyList<RewardEntry> candidates)
        {
            Candidates = candidates;
        }

        public IReadOnlyList<RewardEntry> Candidates { get; private set; }
        public override string ActionName { get { return "OfferTavernFixItem"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var projected = RewardOfferFaceProjection.Project(context, Candidates);
            context.GetModel<PendingChoiceModel>().OfferTavernFixItem(projected);
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.RewardOffered, context.ActionId, ActionName)
                    .WithAmount(projected != null ? projected.Count : 0)
                    .WithMessage(RewardOfferFaceEncoding.Format(PendingChoiceModel.TavernFixItemPoolId, projected)));
        }
    }

    /// <summary>特殊奖励房免费货架会话（#94）：宝箱奖励 / 道具奖励。</summary>
    public sealed class OfferSpecialRewardSessionAction : GameAction
    {
        public OfferSpecialRewardSessionAction(string poolId, IReadOnlyList<RewardEntry> shelves)
        {
            PoolId = poolId ?? string.Empty;
            Shelves = shelves;
        }

        public string PoolId { get; private set; }
        public IReadOnlyList<RewardEntry> Shelves { get; private set; }
        public override string ActionName { get { return "OfferSpecialRewardSession"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var projected = RewardOfferFaceProjection.Project(context, Shelves);
            context.GetModel<PendingChoiceModel>().OfferRewards(PoolId, projected);
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.RewardOffered, context.ActionId, ActionName)
                    .WithAmount(projected != null ? projected.Count : 0)
                    .WithMessage(RewardOfferFaceEncoding.Format(PoolId, projected)));
        }
    }

    public sealed class GrantRewardChoiceAction : GameAction
    {
        public GrantRewardChoiceAction(RewardEntry entry, int optionIndex)
        {
            Entry = entry;
            OptionIndex = optionIndex;
        }

        public RewardEntry Entry { get; private set; }
        public int OptionIndex { get; private set; }
        public override string ActionName { get { return "GrantRewardChoice"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            if (Entry == null)
            {
                return GameActionResult.Empty;
            }

            var result = new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.RewardSelected, context.ActionId, ActionName)
                    .WithAmount(OptionIndex)
                    .WithMessage(Entry.DefId + ":" + Entry.Kind + ":" + Entry.Count));
            RewardGrantActionSupport.AddGrantFollowUp(
                result,
                Entry,
                HelpCardGrantRouting.ShouldGrantToCarryPack(context));
            return result;
        }
    }

    public sealed class SkipRewardChoiceAction : GameAction
    {
        public override string ActionName { get { return "SkipRewardChoice"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.RewardSkipped, context.ActionId, ActionName));
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

    internal static class RewardOfferFaceProjection
    {
        /// <summary>
        /// 用 CreateDraft 填「拿了就是」攻/甲/血；不造卡进 Registry。
        /// </summary>
        public static IReadOnlyList<RewardEntry> Project(GameActionContext context, IReadOnlyList<RewardEntry> offered)
        {
            if (offered == null || offered.Count == 0)
            {
                return offered ?? Array.Empty<RewardEntry>();
            }

            var content = context.GetSystem<IContentSystem>();
            var projected = new List<RewardEntry>(offered.Count);
            for (var i = 0; i < offered.Count; i++)
            {
                var entry = offered[i];
                if (entry == null)
                {
                    continue;
                }

                var draft = content.CreateDraft(entry.DefId);
                var hp = draft.Hp > 0 ? draft.Hp : draft.MaxHp;
                if (hp < 0)
                {
                    hp = 0;
                }

                var attack = draft.Attack < 0 ? 0 : draft.Attack;
                var armor = draft.Armor < 0 ? 0 : draft.Armor;
                projected.Add(entry.WithFaceProjection(attack, armor, hp));
            }

            return projected;
        }
    }

    internal static class HelpCardGrantRouting
    {
        /// <summary>
        /// 局内（InteractionLoop / 开局发牌）帮助卡进战斗卡组；通关/商店/节点末进携带卡包。
        /// </summary>
        public static bool ShouldGrantToCarryPack(GameActionContext context)
        {
            var phase = context.GetModel<RunModel>().Phase.Value;
            return phase != GamePhase.InteractionLoop && phase != GamePhase.DealOpeningCards;
        }
    }

    internal static class RewardGrantActionSupport
    {
        public static void AddGrantFollowUp(GameActionResult result, RewardEntry entry, bool toCarryPack)
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
                    if (toCarryPack)
                    {
                        if (i == 0)
                        {
                            result.AddFollowUp(new GrantHelpCardToPlayerSideDeckAction(entry.DefId, entry.Count));
                        }
                    }
                    else
                    {
                        result.AddFollowUp(new ShuffleIntoDrawPileAction(entry.DefId, entry.Kind, 1, false));
                    }
                }
            }
        }
    }
}
