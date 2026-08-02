using System.Collections.Generic;
using NineGrid.Core.Content;
using NineGrid.Core.Utilities;
using QFramework;

namespace NineGrid.Core.Systems
{
    public interface IRewardSystem : ISystem
    {
        IReadOnlyList<RewardEntry> RollPool(string poolId);
        IReadOnlyList<RoomKind> RollRoomChoices(int count);
        IReadOnlyList<RoomKind> RollPostClearRoomChoices(int nodeIndex);
        NodeDeckOptions BuildNodeDeckOptions(int nodeIndex, string monsterDeckId);
        int ResolveRoom(RoomKind roomKind);

        /// <summary>
        /// 遗物三选一结算后：未选中的遗物记入「连续再出现」降权，仅作用于下一次遗物池抽取。
        /// </summary>
        /// <param name="offered">本次选项。</param>
        /// <param name="selectedDefId">选中的遗物 DefId；跳过时传 null/空。</param>
        void RememberUnselectedRelics(IReadOnlyList<RewardEntry> offered, string selectedDefId);

        /// <summary>
        /// 重绑系统级 Trigger（InitialGameFactory 若 Clear 了 TriggerSystem 后必须调用）。
        /// </summary>
        void RebindSystemTriggers();
    }

    public sealed class RewardSystem : AbstractSystem, IRewardSystem
    {
        /// <summary>
        /// 上一次出现但未选中的遗物，在紧接着的下一次遗物池抽取中权重降为 1/2；抽完即清除，后续不再降权。
        /// </summary>
        private const int ConsecutiveAppearanceWeightNumerator = 1;
        private const int ConsecutiveAppearanceWeightDenominator = 2;

        private IUnRegister mKillRewardUnregister;
        private readonly Dictionary<string, string> mFloorMonsterDeckCache = new Dictionary<string, string>();
        private ulong mFloorMonsterDeckCacheSeed = ulong.MaxValue;
        private readonly HashSet<string> mConsecutiveAppearancePenaltyRelics = new HashSet<string>();

        protected override void OnInit()
        {
            RebindSystemTriggers();
        }

        public void RebindSystemTriggers()
        {
            if (mKillRewardUnregister != null)
            {
                mKillRewardUnregister.UnRegister();
                mKillRewardUnregister = null;
            }

            mKillRewardUnregister = this.GetSystem<ITriggerSystem>().Register(
                TriggerPoint.OnKill,
                TriggerTiming.Post,
                new DelegateTriggerReaction("reward.killRewards", ReactToKill));
        }

        public IReadOnlyList<RewardEntry> RollPool(string poolId)
        {
            var catalog = CatalogOrNull();
            RewardPoolDefinition pool;
            if (catalog == null || !catalog.Rewards.TryGetPool(poolId, out pool))
            {
                return new RewardEntry[0];
            }

            var isRelicPool = IsRelicRewardPool(poolId);
            var applyConsecutivePenalty = isRelicPool && mConsecutiveAppearancePenaltyRelics.Count > 0;
            var candidates = BuildRollCandidates(pool.Entries, excludeOwnedRelics: true);
            var count = pool.PickCount;
            IReadOnlyList<RewardEntry> rolled;
            if (TryGetRarityWeights(pool, out var white, out var blue, out var gold, out var red))
            {
                rolled = RollPoolByRarity(
                    catalog,
                    candidates,
                    count,
                    white,
                    blue,
                    gold,
                    red,
                    applyConsecutivePenalty);
            }
            else
            {
                var result = new List<RewardEntry>();
                for (var i = 0; i < count && candidates.Count > 0; i++)
                {
                    var index = RollWeightedIndex(candidates, applyConsecutivePenalty);
                    result.Add(candidates[index]);
                    candidates.RemoveAt(index);
                }

                rolled = result;
            }

            rolled = EnforceRoleBalance(catalog, pool, candidates, rolled, applyConsecutivePenalty);

            if (applyConsecutivePenalty)
            {
                mConsecutiveAppearancePenaltyRelics.Clear();
            }

            return rolled;
        }

        public void RememberUnselectedRelics(IReadOnlyList<RewardEntry> offered, string selectedDefId)
        {
            mConsecutiveAppearancePenaltyRelics.Clear();
            if (offered == null || offered.Count == 0)
            {
                return;
            }

            var selected = selectedDefId ?? string.Empty;
            for (var i = 0; i < offered.Count; i++)
            {
                var entry = offered[i];
                if (entry == null
                    || entry.Kind != CardKind.Relic
                    || string.IsNullOrEmpty(entry.DefId)
                    || entry.DefId == selected)
                {
                    continue;
                }

                mConsecutiveAppearancePenaltyRelics.Add(entry.DefId);
            }
        }

        public IReadOnlyList<RoomKind> RollRoomChoices(int count)
        {
            return RollRoomChoicesFromPool(count, includeAllWeighted: true, allowElite: true);
        }

        public IReadOnlyList<RoomKind> RollPostClearRoomChoices(int nodeIndex)
        {
            var family = MapNodeProgression.GetPostClearOfferFamily(nodeIndex);
            switch (family)
            {
                case NodeOfferFamily.BattleRooms:
                    return PadRoomChoices(
                        RollRoomChoicesFromPool(
                            count: 2,
                            includeAllWeighted: false,
                            allowElite: MapNodeProgression.AllowsEliteInBattleOffers(nodeIndex),
                            predicate: IsBattleOfferRoom),
                        2,
                        RoomKind.Battle);
                case NodeOfferFamily.ConsumerRooms:
                    return PadRoomChoices(
                        RollRoomChoicesFromPool(
                            count: 2,
                            includeAllWeighted: false,
                            allowElite: false,
                            predicate: IsConsumerOfferRoom),
                        2,
                        RoomKind.Gold);
                case NodeOfferFamily.SpecialRooms:
                    return PadRoomChoices(
                        RollRoomChoicesFromPool(
                            count: 2,
                            includeAllWeighted: false,
                            allowElite: false,
                            predicate: IsSpecialOfferRoom),
                        2,
                        RoomKind.Treasure);
                default:
                    return new RoomKind[0];
            }
        }

        private static IReadOnlyList<RoomKind> PadRoomChoices(
            IReadOnlyList<RoomKind> rolled,
            int count,
            RoomKind pad)
        {
            if (rolled == null || rolled.Count == 0)
            {
                return new RoomKind[0];
            }

            if (rolled.Count >= count)
            {
                return rolled;
            }

            var result = new List<RoomKind>(rolled);
            while (result.Count < count)
            {
                result.Add(pad);
            }

            return result;
        }

        private IReadOnlyList<RoomKind> RollRoomChoicesFromPool(
            int count,
            bool includeAllWeighted,
            bool allowElite,
            System.Func<RoomKind, bool> predicate = null)
        {
            var catalog = CatalogOrNull();
            if (catalog == null || count <= 0)
            {
                return new RoomKind[0];
            }

            var rooms = new List<RoomDefinition>();
            foreach (var pair in catalog.Rewards.Rooms)
            {
                var room = pair.Value;
                if (room.Weight <= 0)
                {
                    continue;
                }

                if (!includeAllWeighted)
                {
                    if (predicate != null && !predicate(room.Kind))
                    {
                        continue;
                    }

                    if (room.Kind == RoomKind.Elite && !allowElite)
                    {
                        continue;
                    }
                }

                rooms.Add(room);
            }

            var result = new List<RoomKind>();
            for (var i = 0; i < count && rooms.Count > 0; i++)
            {
                var index = RollWeightedRoomIndex(rooms);
                result.Add(rooms[index].Kind);
                rooms.RemoveAt(index);
            }

            return result;
        }

        private static bool IsBattleOfferRoom(RoomKind kind)
        {
            return kind == RoomKind.Battle || kind == RoomKind.Elite;
        }

        private static bool IsConsumerOfferRoom(RoomKind kind)
        {
            return kind == RoomKind.Shop
                || kind == RoomKind.Tavern
                || kind == RoomKind.Fountain
                || kind == RoomKind.Gold;
        }

        private static bool IsSpecialOfferRoom(RoomKind kind)
        {
            return kind == RoomKind.Treasure || kind == RoomKind.Event;
        }

        public NodeDeckOptions BuildNodeDeckOptions(int nodeIndex, string monsterDeckId)
        {
            var catalog = CatalogOrNull();
            if (catalog == null)
            {
                return NodeDeckOptions.CreateDefaultBattle();
            }

            var rule = FindNodeRule(catalog, nodeIndex);
            if (rule == null)
            {
                var fallback = new NodeDeckOptions();
                AddRunPlayerSideCards(catalog, fallback);
                return fallback;
            }

            MonsterDeckDefinition deck = FindMonsterDeck(catalog, monsterDeckId, rule.DeckKind);
            if (deck == null)
            {
                var fallback = new NodeDeckOptions();
                AddRunPlayerSideCards(catalog, fallback);
                return fallback;
            }

            var options = new NodeDeckOptions
            {
                PlayerOpeningCount = 3,
                EnemyOpeningCount = 3,
                RequireElite = rule.EliteCount > 0 || rule.BossCount > 0
            };

            AddRunPlayerSideCards(catalog, options);

            var selectedNormalCount = 0;
            selectedNormalCount += AddLevelCards(catalog, deck, options, 1, RollRange(rule.Level1Min, rule.Level1Max));
            selectedNormalCount += AddLevelCards(catalog, deck, options, 2, RollRange(rule.Level2Min, rule.Level2Max));

            var remaining = rule.TotalMonsterCount - selectedNormalCount;
            if (rule.Level3Max > 0)
            {
                var rolled = RollRange(rule.Level3Min, rule.Level3Max);
                remaining = remaining < rolled ? remaining : rolled;
            }

            AddLevelCards(catalog, deck, options, 3, remaining);
            AddSpecialCards(catalog, deck, options, true, false, rule.EliteCount);
            AddSpecialCards(catalog, deck, options, false, true, rule.BossCount);
            return options;
        }

        public int ResolveRoom(RoomKind roomKind)
        {
            var catalog = CatalogOrNull();
            RoomDefinition room;
            if (catalog == null || !catalog.Rewards.TryGetRoom(roomKind, out room))
            {
                return 0;
            }

            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new ResolveRoomAction(room.Kind, room.DisplayName));

            if (room.GoldDelta != 0)
            {
                pipeline.Enqueue(new ModifyGoldAction(room.GoldDelta, "room:" + room.Kind));
            }

            var avatarUid = this.GetModel<BoardModel>().AvatarUid.Value;
            if (room.MaxHpDelta != 0)
            {
                pipeline.Enqueue(new ModifyBaseStatAction(avatarUid, StatId.MaxHp, room.MaxHpDelta, "room:" + room.Kind));
            }

            if (room.HealToFull)
            {
                pipeline.Enqueue(new HealAction(avatarUid, avatarUid, 9999));
            }

            if (!string.IsNullOrEmpty(room.RewardPoolId))
            {
                pipeline.Enqueue(new OfferRewardChoiceAction(room.RewardPoolId, 0));
            }

            if (room.ShopOfferCount > 0)
            {
                pipeline.Enqueue(new OfferRewardChoiceAction("shop.helpCards", room.ShopOfferCount));
            }

            return pipeline.RunToCompletion();
        }

        private IEnumerable<GameAction> ReactToKill(TriggerContext context)
        {
            var catalog = CatalogOrNull();
            if (catalog == null || context.Events == null)
            {
                return null;
            }

            var registry = this.GetModel<CardRegistry>();
            var result = new List<GameAction>();
            for (var i = 0; i < context.Events.Count; i++)
            {
                var evt = context.Events[i];
                if (evt.Type != CoreEventType.CardKilled || evt.CardUid == 0)
                {
                    continue;
                }

                CardInstance card;
                if (!registry.TryGet(evt.CardUid, out card))
                {
                    continue;
                }

                var poolId = card.Counters.Get(CoreCounterKeys.Boss) > 0
                    ? "kill.boss"
                    : card.Counters.Get(CoreCounterKeys.Elite) > 0 ? "kill.elite" : string.Empty;
                if (string.IsNullOrEmpty(poolId))
                {
                    continue;
                }

                var rewards = RollPool(poolId);
                for (var j = 0; j < rewards.Count; j++)
                {
                    result.Add(new ShuffleIntoDrawPileAction(rewards[j].DefId, rewards[j].Kind, rewards[j].Count, false));
                }
            }

            return result;
        }

        private void AddRunPlayerSideCards(GameContentCatalog catalog, NodeDeckOptions options)
        {
            var player = this.GetModel<PlayerModel>();
            var content = this.GetSystem<IContentSystem>();
            var stacks = player.ConsumeHelpCardStacksForNode();
            for (var i = 0; i < stacks.Count; i++)
            {
                var stack = stacks[i];
                if (string.IsNullOrEmpty(stack.DefId) || !catalog.Cards.ContainsKey(stack.DefId))
                {
                    continue;
                }

                for (var count = 0; count < stack.Count; count++)
                {
                    options.AddPlayerCard(content.CreateDraft(stack.DefId));
                }
            }
        }

        private int AddLevelCards(
            GameContentCatalog catalog,
            MonsterDeckDefinition deck,
            NodeDeckOptions options,
            int level,
            int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            var candidates = FilterMonsters(catalog, deck, level, false, false);
            for (var i = 0; i < count && candidates.Count > 0; i++)
            {
                var selected = candidates[this.GetUtility<IRngUtility>().Range(0, candidates.Count)];
                options.AddEnemyCard(this.GetSystem<IContentSystem>().CreateDraft(selected.DefId));
            }

            return count;
        }

        private void AddSpecialCards(
            GameContentCatalog catalog,
            MonsterDeckDefinition deck,
            NodeDeckOptions options,
            bool elite,
            bool boss,
            int count)
        {
            if (count <= 0)
            {
                return;
            }

            var candidates = FilterMonsters(catalog, deck, 0, elite, boss);
            for (var i = 0; i < count && candidates.Count > 0; i++)
            {
                var selected = candidates[this.GetUtility<IRngUtility>().Range(0, candidates.Count)];
                options.AddEnemyCard(this.GetSystem<IContentSystem>().CreateDraft(selected.DefId));
            }
        }

        private static List<CardContentDefinition> FilterMonsters(
            GameContentCatalog catalog,
            MonsterDeckDefinition deck,
            int level,
            bool elite,
            bool boss)
        {
            var result = new List<CardContentDefinition>();
            for (var i = 0; i < deck.MonsterDefIds.Count; i++)
            {
                CardContentDefinition card;
                if (!catalog.TryGetCard(deck.MonsterDefIds[i], out card))
                {
                    continue;
                }

                if (!CardCombatRules.IsBoardCombatTarget(card.Kind) || card.IsReserve)
                {
                    continue;
                }

                if (boss)
                {
                    if (card.IsBoss)
                    {
                        result.Add(card);
                    }

                    continue;
                }

                if (elite)
                {
                    if (card.IsElite && !card.IsBoss)
                    {
                        result.Add(card);
                    }

                    continue;
                }

                if (!card.IsElite && !card.IsBoss && card.Level == level)
                {
                    result.Add(card);
                }
            }

            return result;
        }

        private NodeDeckRule FindNodeRule(GameContentCatalog catalog, int nodeIndex)
        {
            for (var i = 0; i < catalog.Rewards.NodeDeckRules.Count; i++)
            {
                if (catalog.Rewards.NodeDeckRules[i].NodeIndex == nodeIndex)
                {
                    return catalog.Rewards.NodeDeckRules[i];
                }
            }

            return null;
        }

        private MonsterDeckDefinition FindMonsterDeck(GameContentCatalog catalog, string deckId, MonsterDeckKind kind)
        {
            MonsterDeckDefinition deck;
            if (!string.IsNullOrEmpty(deckId) && catalog.MonsterDecks.TryGetValue(deckId, out deck))
            {
                return deck;
            }

            EnsureFloorMonsterDeckCache();
            var run = this.GetModel<RunModel>();
            var cacheKey = run.Floor.Value + ":" + ((int)kind).ToString();
            string cachedId;
            if (mFloorMonsterDeckCache.TryGetValue(cacheKey, out cachedId)
                && catalog.MonsterDecks.TryGetValue(cachedId, out deck))
            {
                return deck;
            }

            var matches = new List<MonsterDeckDefinition>();
            foreach (var pair in catalog.MonsterDecks)
            {
                if (pair.Value.Kind == kind)
                {
                    matches.Add(pair.Value);
                }
            }

            if (matches.Count == 0)
            {
                return null;
            }

            var pick = matches[this.GetUtility<IRngUtility>().Range(0, matches.Count)];
            mFloorMonsterDeckCache[cacheKey] = pick.Id;
            return pick;
        }

        private void EnsureFloorMonsterDeckCache()
        {
            var seed = this.GetModel<RunModel>().Seed.Value;
            if (seed == mFloorMonsterDeckCacheSeed)
            {
                return;
            }

            mFloorMonsterDeckCache.Clear();
            mFloorMonsterDeckCacheSeed = seed;
        }

        /// <summary>
        /// 设计品质表：通关/商店帮助卡白65蓝30金5；普通箱/血液转换同；蓝箱白40蓝50金10；金箱蓝50金50。
        /// </summary>
        private static bool TryGetRarityWeights(
            RewardPoolDefinition pool,
            out int white,
            out int blue,
            out int gold,
            out int red)
        {
            white = 0;
            blue = 0;
            gold = 0;
            red = 0;
            if (pool == null)
            {
                return false;
            }

            if (pool.Query != null && pool.Query.HasRarityWeights)
            {
                white = pool.Query.RarityWeightWhite;
                blue = pool.Query.RarityWeightBlue;
                gold = pool.Query.RarityWeightGold;
                red = pool.Query.RarityWeightRed;
                return true;
            }

            // legacy poolId 硬编码回退（测试夹具仍可无 Query）。
            return TryGetLegacyRarityWeights(pool.Id, out white, out blue, out gold, out red);
        }

        private static bool TryGetLegacyRarityWeights(
            string poolId,
            out int white,
            out int blue,
            out int gold,
            out int red)
        {
            white = 0;
            blue = 0;
            gold = 0;
            red = 0;
            if (string.IsNullOrEmpty(poolId))
            {
                return false;
            }

            if (poolId == "help.choice" || poolId == "shop.helpCards")
            {
                white = 65;
                blue = 30;
                gold = 5;
                return true;
            }

            if (poolId == "help.white.choice")
            {
                white = 100;
                return true;
            }

            if (poolId == "relic.common_chest" || poolId == "relic.blood_conversion")
            {
                white = 65;
                blue = 30;
                gold = 5;
                return true;
            }

            if (poolId == "relic.blue_chest")
            {
                white = 40;
                blue = 50;
                gold = 10;
                return true;
            }

            if (poolId == "relic.golden_chest")
            {
                blue = 50;
                gold = 50;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 投放均衡：若 Query 要求最少 Attack/Defense 张数，而当前抽签不足，则从剩余候选中替换补足。
        /// </summary>
        private IReadOnlyList<RewardEntry> EnforceRoleBalance(
            GameContentCatalog catalog,
            RewardPoolDefinition pool,
            List<RewardEntry> remainingCandidates,
            IReadOnlyList<RewardEntry> rolled,
            bool applyConsecutivePenalty)
        {
            if (catalog == null || pool == null || pool.Query == null || rolled == null)
            {
                return rolled;
            }

            var minAttack = pool.Query.BalanceMinAttack;
            var minDefense = pool.Query.BalanceMinDefense;
            if (minAttack <= 0 && minDefense <= 0)
            {
                return rolled;
            }

            var result = new List<RewardEntry>(rolled);
            var leftover = new List<RewardEntry>();
            if (remainingCandidates != null)
            {
                leftover.AddRange(remainingCandidates);
            }

            // 把已抽中的从 leftover 去掉（BuildRollCandidates 已就地 Remove，但 rarity 路径可能不同）。
            for (var i = 0; i < result.Count; i++)
            {
                leftover.RemoveAll(e => e != null && e.DefId == result[i].DefId);
            }

            EnsureRoleCount(catalog, result, leftover, ContentRole.Attack, minAttack);
            EnsureRoleCount(catalog, result, leftover, ContentRole.Defense, minDefense);
            return result;
        }

        private void EnsureRoleCount(
            GameContentCatalog catalog,
            List<RewardEntry> result,
            List<RewardEntry> leftover,
            ContentRole role,
            int minCount)
        {
            if (minCount <= 0 || result == null)
            {
                return;
            }

            var have = 0;
            for (var i = 0; i < result.Count; i++)
            {
                if (ResolveEntryRole(catalog, result[i]) == role)
                {
                    have++;
                }
            }

            while (have < minCount && leftover.Count > 0)
            {
                var replaceIndex = -1;
                for (var i = 0; i < leftover.Count; i++)
                {
                    if (ResolveEntryRole(catalog, leftover[i]) == role)
                    {
                        replaceIndex = i;
                        break;
                    }
                }

                if (replaceIndex < 0)
                {
                    break;
                }

                var donor = leftover[replaceIndex];
                leftover.RemoveAt(replaceIndex);

                var victim = -1;
                for (var i = 0; i < result.Count; i++)
                {
                    var existingRole = ResolveEntryRole(catalog, result[i]);
                    if (existingRole != ContentRole.Attack && existingRole != ContentRole.Defense)
                    {
                        victim = i;
                        break;
                    }
                }

                if (victim < 0)
                {
                    for (var i = 0; i < result.Count; i++)
                    {
                        if (ResolveEntryRole(catalog, result[i]) != role)
                        {
                            victim = i;
                            break;
                        }
                    }
                }

                if (victim < 0)
                {
                    break;
                }

                leftover.Add(result[victim]);
                result[victim] = donor;
                have++;
            }
        }

        private static ContentRole ResolveEntryRole(GameContentCatalog catalog, RewardEntry entry)
        {
            if (entry == null || catalog == null)
            {
                return ContentRole.None;
            }

            if (entry.Kind == CardKind.Relic)
            {
                RelicContentDefinition relic;
                if (catalog.Relics.TryGetValue(entry.DefId, out relic) && relic != null)
                {
                    return relic.Role;
                }

                return ContentRole.None;
            }

            CardContentDefinition card;
            if (catalog.Cards.TryGetValue(entry.DefId, out card) && card != null)
            {
                return card.Role;
            }

            return ContentRole.None;
        }

        private static bool IsRelicRewardPool(string poolId)
        {
            return !string.IsNullOrEmpty(poolId)
                && poolId.StartsWith("relic.", System.StringComparison.Ordinal);
        }

        private List<RewardEntry> BuildRollCandidates(IReadOnlyList<RewardEntry> entries, bool excludeOwnedRelics)
        {
            var candidates = new List<RewardEntry>();
            if (entries == null || entries.Count == 0)
            {
                return candidates;
            }

            IReadOnlyList<string> ownedRelics = null;
            if (excludeOwnedRelics)
            {
                ownedRelics = this.GetModel<PlayerModel>().RelicDefIds;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.DefId))
                {
                    continue;
                }

                if (excludeOwnedRelics
                    && entry.Kind == CardKind.Relic
                    && ContainsDefId(ownedRelics, entry.DefId))
                {
                    continue;
                }

                candidates.Add(entry);
            }

            return candidates;
        }

        private static bool ContainsDefId(IReadOnlyList<string> defIds, string defId)
        {
            if (defIds == null || defIds.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < defIds.Count; i++)
            {
                if (defIds[i] == defId)
                {
                    return true;
                }
            }

            return false;
        }

        private IReadOnlyList<RewardEntry> RollPoolByRarity(
            GameContentCatalog catalog,
            List<RewardEntry> candidates,
            int count,
            int whiteWeight,
            int blueWeight,
            int goldWeight,
            int redWeight,
            bool applyConsecutivePenalty)
        {
            var result = new List<RewardEntry>();
            for (var i = 0; i < count && candidates.Count > 0; i++)
            {
                var rarity = RollAvailableRarity(
                    catalog,
                    candidates,
                    whiteWeight,
                    blueWeight,
                    goldWeight,
                    redWeight);
                var ofRarity = new List<RewardEntry>();
                for (var c = 0; c < candidates.Count; c++)
                {
                    if (ResolveEntryRarity(catalog, candidates[c]) == rarity)
                    {
                        ofRarity.Add(candidates[c]);
                    }
                }

                if (ofRarity.Count == 0)
                {
                    ofRarity.AddRange(candidates);
                }

                var index = RollWeightedIndex(ofRarity, applyConsecutivePenalty);
                var picked = ofRarity[index];
                result.Add(picked);
                candidates.Remove(picked);
            }

            return result;
        }

        private ContentRarity RollAvailableRarity(
            GameContentCatalog catalog,
            List<RewardEntry> candidates,
            int whiteWeight,
            int blueWeight,
            int goldWeight,
            int redWeight)
        {
            var hasWhite = false;
            var hasBlue = false;
            var hasGold = false;
            var hasRed = false;
            for (var i = 0; i < candidates.Count; i++)
            {
                switch (ResolveEntryRarity(catalog, candidates[i]))
                {
                    case ContentRarity.White:
                        hasWhite = true;
                        break;
                    case ContentRarity.Blue:
                        hasBlue = true;
                        break;
                    case ContentRarity.Gold:
                        hasGold = true;
                        break;
                    case ContentRarity.Red:
                        hasRed = true;
                        break;
                }
            }

            var w = hasWhite ? whiteWeight : 0;
            var b = hasBlue ? blueWeight : 0;
            var g = hasGold ? goldWeight : 0;
            var r = hasRed ? redWeight : 0;
            var total = w + b + g + r;
            if (total <= 0)
            {
                if (hasWhite)
                {
                    return ContentRarity.White;
                }

                if (hasBlue)
                {
                    return ContentRarity.Blue;
                }

                if (hasGold)
                {
                    return ContentRarity.Gold;
                }

                return ContentRarity.Red;
            }

            var roll = this.GetUtility<IRngUtility>().Range(0, total);
            if (roll < w)
            {
                return ContentRarity.White;
            }

            roll -= w;
            if (roll < b)
            {
                return ContentRarity.Blue;
            }

            roll -= b;
            if (roll < g)
            {
                return ContentRarity.Gold;
            }

            return ContentRarity.Red;
        }

        private static ContentRarity ResolveEntryRarity(GameContentCatalog catalog, RewardEntry entry)
        {
            if (entry == null || catalog == null)
            {
                return ContentRarity.None;
            }

            if (entry.Kind == CardKind.Relic)
            {
                RelicContentDefinition relic;
                if (catalog.Relics.TryGetValue(entry.DefId, out relic))
                {
                    return relic.Rarity;
                }

                return ContentRarity.None;
            }

            CardContentDefinition card;
            if (catalog.Cards.TryGetValue(entry.DefId, out card))
            {
                return card.Rarity;
            }

            return ContentRarity.None;
        }

        private int RollRange(int min, int max)
        {
            if (max <= 0)
            {
                return 0;
            }

            if (max <= min)
            {
                return min;
            }

            return this.GetUtility<IRngUtility>().Range(min, max + 1);
        }

        private int RollWeightedIndex(IReadOnlyList<RewardEntry> entries, bool applyConsecutivePenalty)
        {
            var total = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                total += ResolveRollWeight(entries[i], applyConsecutivePenalty);
            }

            if (total <= 0)
            {
                return this.GetUtility<IRngUtility>().Range(0, entries.Count);
            }

            var roll = this.GetUtility<IRngUtility>().Range(0, total);
            var cursor = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                cursor += ResolveRollWeight(entries[i], applyConsecutivePenalty);
                if (roll < cursor)
                {
                    return i;
                }
            }

            return entries.Count - 1;
        }

        private int ResolveRollWeight(RewardEntry entry, bool applyConsecutivePenalty)
        {
            var weight = entry != null && entry.Weight > 0 ? entry.Weight : 0;
            if (!applyConsecutivePenalty || weight <= 0 || entry == null)
            {
                return weight;
            }

            if (mConsecutiveAppearancePenaltyRelics.Contains(entry.DefId))
            {
                return weight * ConsecutiveAppearanceWeightNumerator;
            }

            return weight * ConsecutiveAppearanceWeightDenominator;
        }

        private int RollWeightedRoomIndex(IReadOnlyList<RoomDefinition> rooms)
        {
            var total = 0;
            for (var i = 0; i < rooms.Count; i++)
            {
                total += rooms[i].Weight;
            }

            var roll = this.GetUtility<IRngUtility>().Range(0, total);
            var cursor = 0;
            for (var i = 0; i < rooms.Count; i++)
            {
                cursor += rooms[i].Weight;
                if (roll < cursor)
                {
                    return i;
                }
            }

            return rooms.Count - 1;
        }

        private GameContentCatalog CatalogOrNull()
        {
            var content = this.GetSystem<IContentSystem>();
            content.TryReloadFromConfig();
            return content.HasCatalog ? content.Catalog : null;
        }
    }
}
