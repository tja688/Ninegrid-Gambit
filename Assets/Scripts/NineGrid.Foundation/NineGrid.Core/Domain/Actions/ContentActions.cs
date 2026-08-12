using System;
using System.Collections.Generic;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Stats;
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
            // 基础甲变更前先读当前甲：无 CurrentArmor 键时 GetCurrentArmor 回退基础甲，
            // 必须在 SetBase(Armor) 之前取样，再用实际基础变化量同步缓冲。
            var currentArmorBefore = Stat == StatId.Armor
                ? StatArmorUtility.GetCurrentArmor(card)
                : 0;
            var previous = (int)Math.Round(card.Stats.GetBase(Stat));
            // 损耗类「掉自己血量」技能（tpl.skill.attrition.remove 等）不得把血扣到 0：
            // 0 血卡不会因此死亡，会以 0 血卡在场上（击破只由伤害结算走 Kill）。负增量钳到最低 1。
            var next = Stat == StatId.Hp && Delta < 0
                ? Math.Max(1, previous + Delta)
                : Math.Max(0, previous + Delta);
            card.Stats.SetBase(Stat, next);

            // 加上限同时加等量当前血；降上限则把当前血钳到新上限（局内统一约定）。
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

            int currentArmorAfter = currentArmorBefore;
            int currentArmorDelta = 0;
            if (Stat == StatId.Armor)
            {
                // 永久成长立刻成为本关可消耗缓冲；卡面只认 ArmorChanged，不认基础甲 ResultValue。
                var appliedBaseDelta = next - previous;
                currentArmorAfter = Math.Max(0, currentArmorBefore + appliedBaseDelta);
                currentArmorDelta = currentArmorAfter - currentArmorBefore;
                StatArmorUtility.SetCurrentArmor(card, currentArmorAfter);
            }

            // 攻按 ADR-0005 Permanent 有效攻旁路：ResultValue 携带含常驻/条件修饰器的有效攻
            // （与 AppendPermanentAttackFaceCommit 同构，怪物含 EnemyAttackDelta 规则），
            // 否则加攻卡等路径会提交基础值、与伤害结算（有效攻）错位——带遗物/光环时显示落后于真实攻击力。
            var resultValue = Stat == StatId.Attack
                ? CardFaceEventValues.GetFaceAttack(context.GetSystem<IStatSystem>(), card)
                : next;

            var evt = new CoreGameEvent(CoreEventType.BaseStatModified, context.ActionId, ActionName)
                .WithCard(TargetUid)
                .WithTarget(TargetUid)
                .WithAmount((int)Stat)
                .WithDelta(Delta)
                .WithResultValue(resultValue)
                .WithMessage(Reason)
                .WithSource(SourceDefId, Reason);

            // MaxHp 变更后的当前血绝对值：表现层按 ADR-0005 用指令赋值，禁止自行累加。
            if (Stat == StatId.MaxHp)
            {
                var hpAfter = (int)Math.Round(card.Stats.GetBase(StatId.Hp));
                evt.WithRemaining(hpAfter, StatArmorUtility.GetCurrentArmor(card));
            }

            var result = new GameActionResult().AddEvent(evt);
            if (Stat == StatId.Armor && currentArmorDelta != 0)
            {
                var hp = Math.Max(0, (int)Math.Round(card.Stats.GetBase(StatId.Hp)));
                result.AddEvent(new CoreGameEvent(CoreEventType.ArmorChanged, context.ActionId, ActionName)
                    .WithTarget(TargetUid)
                    .WithCard(TargetUid)
                    .WithDelta(currentArmorDelta)
                    .WithRemaining(hp, currentArmorAfter)
                    .WithSource(SourceDefId, Reason));
            }

            // ADR-0039：ModifyBaseStat 扣血/钳血至 0 须闭合 Defeat follow-up（不限 DealDamage）。
            return AvatarDefeatFollowUp.AppendIfAvatarHpZero(card, result);
        }
    }

    /// <summary>#119：遗物关卡累计倒计时清零（恐怖面罩「本关卡」）。</summary>
    public sealed class SetCounterAction : GameAction
    {
        public SetCounterAction(int targetUid, string key, int value)
        {
            TargetUid = targetUid;
            Key = key ?? string.Empty;
            Value = value;
        }

        public int TargetUid { get; private set; }
        public string Key { get; private set; }
        public int Value { get; private set; }
        public override string ActionName { get { return "SetCounter"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            if (string.IsNullOrEmpty(Key) || TargetUid == 0)
            {
                return GameActionResult.Empty;
            }

            var card = context.GetModel<CardRegistry>().Get(TargetUid);
            if (Value == 0)
            {
                card.Counters.Remove(Key);
            }
            else
            {
                card.Counters.Set(Key, Value);
            }

            return GameActionResult.Empty;
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
            var content = context.GetSystem<IContentSystem>();
            // #115：奖池已排除归档遗物；授予路径同样跳过，避免误接线。
            if (content != null
                && content.Catalog != null
                && content.Catalog.Relics.TryGetValue(RelicDefId, out var relic)
                && RelicDecks.IsArchive(relic.DeckId))
            {
                return GameActionResult.Empty;
            }

            var player = context.GetModel<PlayerModel>();
            if (player.IsRelicInventoryFull && !PlayerOwnsRelic(player, RelicDefId))
            {
                return GameActionResult.Empty;
            }

            player.AddRelic(RelicDefId);
            if (content != null)
            {
                content.ActivateRelic(RelicDefId);
            }

            var result = new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.RelicGranted, context.ActionId, ActionName)
                    .WithMessage(RelicDefId));

            // 遗物带 EnemyAttackDelta 规则（如龙鳞甲全场怪物攻击-1）：战斗中获得（宝箱等）
            // 立即刷新场上怪物卡面攻，与伤害结算口径对齐。
            if (RelicEnemyAttackDeltaRules.Has(context.GetSystem<IEffectSystem>(), RelicDefId))
            {
                CardFaceEventValues.AppendEnemyAttackDeltaFaceCommitsForBoardMonsters(
                    result,
                    context,
                    ActionName,
                    "enemyAttackDelta",
                    RelicDefId);
            }

            return result;
        }

        private static bool PlayerOwnsRelic(PlayerModel player, string defId)
        {
            var relics = player.RelicDefIds;
            for (var i = 0; i < relics.Count; i++)
            {
                if (relics[i] == defId)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 玩家丢弃已装备遗物：反激活遗物效果并从装备栏移除（金币由 EconomySystem 另发）。
    /// </summary>
    public sealed class DiscardRelicAction : GameAction
    {
        public DiscardRelicAction(string relicDefId)
        {
            RelicDefId = relicDefId ?? string.Empty;
        }

        public string RelicDefId { get; private set; }
        public override string ActionName { get { return "DiscardRelic"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            if (string.IsNullOrEmpty(RelicDefId))
            {
                return GameActionResult.Empty;
            }

            var effectSystem = context.GetSystem<IEffectSystem>();
            // 反激活前取样：卸载后规则已不在注册表，事后查不到。
            var hadEnemyAttackDeltaRule = RelicEnemyAttackDeltaRules.Has(effectSystem, RelicDefId);
            var instances = effectSystem.Instances;
            for (var i = instances.Count - 1; i >= 0; i--)
            {
                var instance = instances[i];
                if (instance == null
                    || instance.Owner == null
                    || instance.Owner.ContainerType != EffectContainerType.Relic
                    || !string.Equals(instance.Owner.SourceDefId, RelicDefId, System.StringComparison.Ordinal))
                {
                    continue;
                }

                effectSystem.Deactivate(instance.InstanceId);
            }

            context.GetModel<PlayerModel>().RemoveRelic(RelicDefId);

            // #120：丢弃时清掉本遗物 run 贡献，并拆掉汇入 Avatar 的 Persistent modifier。
            var contributions = context.GetModel<RelicRunContributionModel>();
            contributions.ClearRelic(RelicDefId);
            var board = context.GetModel<BoardModel>();
            var avatarUid = board != null ? board.AvatarUid.Value : 0;
            CardInstance avatar;
            if (avatarUid != 0 && context.GetModel<CardRegistry>().TryGet(avatarUid, out avatar))
            {
                var stats = context.GetSystem<IStatSystem>();
                stats.RemoveModifiersBySource(
                    avatar,
                    new ModifierSource(RelicRunContributionModel.BuildModifierSourceId(RelicDefId, StatId.Attack)));
                stats.RemoveModifiersBySource(
                    avatar,
                    new ModifierSource(RelicRunContributionModel.BuildModifierSourceId(RelicDefId, StatId.Armor)));
            }

            var result = new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.EffectDeactivated, context.ActionId, ActionName)
                    .WithMessage(RelicDefId));

            // 卸下 EnemyAttackDelta 规则遗物（如龙鳞甲）后，场上怪物卡面攻回到无修正值。
            if (hadEnemyAttackDeltaRule)
            {
                CardFaceEventValues.AppendEnemyAttackDeltaFaceCommitsForBoardMonsters(
                    result,
                    context,
                    ActionName,
                    "enemyAttackDelta",
                    RelicDefId);
            }

            return result;
        }
    }

    /// <summary>
    /// 遗物效果集是否含 <see cref="RuleId.EnemyAttackDelta"/> 规则修正（全场怪物攻击±N 类），
    /// 用于装卸时决定是否补扫场上怪物卡面攻。
    /// </summary>
    internal static class RelicEnemyAttackDeltaRules
    {
        internal static bool Has(IEffectSystem effectSystem, string relicDefId)
        {
            if (effectSystem == null || string.IsNullOrEmpty(relicDefId))
            {
                return false;
            }

            var instances = effectSystem.Instances;
            for (var i = 0; i < instances.Count; i++)
            {
                var instance = instances[i];
                if (instance == null
                    || instance.Owner == null
                    || instance.Owner.ContainerType != EffectContainerType.Relic
                    || !string.Equals(instance.Owner.SourceDefId, relicDefId, System.StringComparison.Ordinal)
                    || instance.Definition == null
                    || instance.Definition.Kind != EffectKind.RuleModifier
                    || instance.Definition.RuleModifier == null)
                {
                    continue;
                }

                if (instance.Definition.RuleModifier.Get("rule").AsEnum(RuleId.RecoveryMultiplier)
                    == RuleId.EnemyAttackDelta)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// #120：改写遗物 run 内攻/甲贡献，并以 replaceSameSource Persistent modifier 同步到 Avatar 有效属性。
    /// </summary>
    public sealed class ModifyRelicRunContributionAction : GameAction
    {
        public ModifyRelicRunContributionAction(
            string relicDefId,
            StatId stat,
            int delta,
            bool absolute,
            int absoluteValue,
            int floor)
        {
            RelicDefId = relicDefId ?? string.Empty;
            Stat = stat;
            Delta = delta;
            Absolute = absolute;
            AbsoluteValue = absoluteValue;
            Floor = floor;
        }

        public string RelicDefId { get; private set; }
        public StatId Stat { get; private set; }
        public int Delta { get; private set; }
        public bool Absolute { get; private set; }
        public int AbsoluteValue { get; private set; }
        public int Floor { get; private set; }
        public override string ActionName { get { return "ModifyRelicRunContribution"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            if (string.IsNullOrEmpty(RelicDefId)
                || (Stat != StatId.Attack && Stat != StatId.Armor))
            {
                return GameActionResult.Empty;
            }

            var model = context.GetModel<RelicRunContributionModel>();
            var next = Absolute
                ? Math.Max(Floor, AbsoluteValue)
                : model.Adjust(RelicDefId, Stat, Delta, Floor);
            if (Absolute)
            {
                model.Set(RelicDefId, Stat, next);
            }

            var board = context.GetModel<BoardModel>();
            var avatarUid = board != null ? board.AvatarUid.Value : 0;
            CardInstance avatar;
            if (avatarUid == 0 || !context.GetModel<CardRegistry>().TryGet(avatarUid, out avatar))
            {
                return GameActionResult.Empty;
            }

            var sourceId = RelicRunContributionModel.BuildModifierSourceId(RelicDefId, Stat);
            var source = new ModifierSource(sourceId);
            var stats = context.GetSystem<IStatSystem>();
            stats.RemoveModifiersBySource(avatar, source);
            if (next > 0)
            {
                stats.AddModifier(
                    avatar,
                    new StatModifier(
                        Stat,
                        ModifierOp.Add,
                        next,
                        ModifierLayer.Persistent,
                        source,
                        ModifierScope.Permanent,
                        null));
            }

            var result = new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.EffectModifierApplied, context.ActionId, ActionName)
                    .WithCard(avatarUid)
                    .WithTarget(avatarUid)
                    .WithAmount((int)Stat)
                    .WithDelta(next)
                    .WithMessage(sourceId)
                    .WithSource(RelicDefId, sourceId)
                    .WithResultValue(next));

            if (Stat == StatId.Attack)
            {
                CardFaceEventValues.AppendPermanentAttackFaceCommit(
                    result,
                    context,
                    avatar,
                    ActionName,
                    sourceId,
                    RelicDefId,
                    next);
            }

            return result;
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
            if (string.IsNullOrEmpty(DefId))
            {
                return GameActionResult.Empty;
            }

            var content = context.GetSystem<IContentSystem>();
            var registry = context.GetModel<CardRegistry>();
            var deck = context.GetModel<DeckModel>();
            var player = context.GetModel<PlayerModel>();
            if (player.IsItemSlotsFull(deck))
            {
                // 满格：静默丢弃，不兑金。
                return GameActionResult.Empty;
            }

            var result = new GameActionResult();
            var accepted = 0;
            for (var i = 0; i < Count; i++)
            {
                if (player.IsItemSlotsFull(deck))
                {
                    break;
                }

                var draft = content.CreateDraft(DefId);
                var card = draft.Create(registry);
                content.ApplyContentToCard(card);
                deck.AddToItemSlots(card);
                accepted++;
                result.AddWithFaceAbsolutes(
                    context,
                    card,
                    new CoreGameEvent(CoreEventType.CardSpawned, context.ActionId, ActionName)
                        .WithCard(card.Uid)
                        .WithMessage(DefId));
            }

            if (accepted <= 0)
            {
                return GameActionResult.Empty;
            }

            result.AddEvent(new CoreGameEvent(CoreEventType.RewardSelected, context.ActionId, ActionName)
                .WithAmount(accepted)
                .WithMessage(DefId + ":HelpCard:" + accepted));
            return result;
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

            var toItemSlots = HelpCardGrantRouting.ShouldGrantToItemSlots(context);
            for (var i = 0; i < offered.Count; i++)
            {
                RewardGrantActionSupport.AddGrantFollowUp(result, offered[i], toItemSlots);
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

    /// <summary>属性房三选二会话（#136）：进房即 offer 3 个加权候选。</summary>
    public sealed class OfferAttributePickSessionAction : GameAction
    {
        public OfferAttributePickSessionAction(IReadOnlyList<RewardEntry> candidates)
        {
            Candidates = candidates;
        }

        public IReadOnlyList<RewardEntry> Candidates { get; private set; }
        public override string ActionName { get { return "OfferAttributePickSession"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var projected = RewardOfferFaceProjection.Project(context, Candidates);
            context.GetModel<PendingChoiceModel>().OfferAttributePick(projected);
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.RewardOffered, context.ActionId, ActionName)
                    .WithAmount(projected != null ? projected.Count : 0)
                    .WithMessage(RewardOfferFaceEncoding.Format(PendingChoiceModel.AttributePickPoolId, projected)));
        }
    }

    /// <summary>属性房三选二（#136）：记录一次候选选择并移除该候选实例（不可重复点同实例）。</summary>
    public sealed class SelectAttributeCandidateAction : GameAction
    {
        public SelectAttributeCandidateAction(int optionIndex, string defId)
        {
            OptionIndex = optionIndex;
            DefId = defId ?? string.Empty;
        }

        public int OptionIndex { get; private set; }
        public string DefId { get; private set; }
        public override string ActionName { get { return "SelectAttributeCandidate"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var pending = context.GetModel<PendingChoiceModel>();
            if (pending.Kind.Value != PendingChoiceKind.AttributePick)
            {
                return GameActionResult.Empty;
            }

            pending.AddAttributeSelection(DefId);
            pending.RemoveRewardOptionAt(OptionIndex);
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.RewardSelected, context.ActionId, ActionName)
                    .WithAmount(OptionIndex)
                    .WithMessage(DefId));
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
                HelpCardGrantRouting.ShouldGrantToItemSlots(context));
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
        /// 局外授予（商店/特殊房/节点末）直写道具卡格；局内 InteractionLoop / DealOpeningCards 进战斗卡组。
        /// </summary>
        public static bool ShouldGrantToItemSlots(GameActionContext context)
        {
            var phase = context.GetModel<RunModel>().Phase.Value;
            return phase != GamePhase.InteractionLoop && phase != GamePhase.DealOpeningCards;
        }
    }

    internal static class RewardGrantActionSupport
    {
        public static void AddGrantFollowUp(GameActionResult result, RewardEntry entry, bool toItemSlots)
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
                    if (toItemSlots)
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
