using System;
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

        /// <summary>商店货架草案（随机属性 / 药水 / 食品）；宝箱卡不上架（仅宝箱房渠道，ADR-0033 修订）。</summary>
        IReadOnlyList<RewardEntry> BuildShopShelves();

        /// <summary>
        /// 卡店服务草案（数值强化 / 固定 / 扩容）。
        /// <paramref name="excludeDefIds"/> 为本货架已购项，刷新后清空再全量补货。
        /// </summary>
        IReadOnlyList<RewardEntry> BuildTavernServices(IReadOnlyCollection<string> excludeDefIds = null);

        /// <summary>宝箱奖励房：1 宝箱 + 3 随机道具。</summary>
        IReadOnlyList<RewardEntry> BuildTreasureRewardShelves();

        /// <summary>道具奖励房：2 属性道具 + 3 随机道具。</summary>
        IReadOnlyList<RewardEntry> BuildItemRewardShelves();

        /// <summary>
        /// 卡店「道具卡固定」二级候选：从来源池均匀随机抽 N 张（可重复、可含已固定 defId）。
        /// </summary>
        IReadOnlyList<RewardEntry> BuildTavernFixItemCandidates(int count = 3);

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
                        RoomKind.Gold);
                case NodeOfferFamily.ConsumerRooms:
                    return PadRoomChoices(
                        RollRoomChoicesFromPool(
                            count: 2,
                            includeAllWeighted: false,
                            allowElite: false,
                            predicate: IsConsumerOfferRoom),
                        2,
                        RoomKind.Shop);
                case NodeOfferFamily.SpecialRooms:
                    return PadRoomChoices(
                        RollRoomChoicesFromPool(
                            count: 2,
                            includeAllWeighted: false,
                            allowElite: false,
                            predicate: IsSpecialOfferRoom),
                        2,
                        RoomKind.TreasureReward);
                case NodeOfferFamily.Boss:
                    return new[] { RoomKind.Boss };
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
            return kind == RoomKind.Attribute
                || kind == RoomKind.Gold
                || kind == RoomKind.Fountain
                || kind == RoomKind.Treasure
                || kind == RoomKind.Elite;
        }

        private static bool IsConsumerOfferRoom(RoomKind kind)
        {
            return kind == RoomKind.Shop || kind == RoomKind.Tavern;
        }

        private static bool IsSpecialOfferRoom(RoomKind kind)
        {
            return kind == RoomKind.TreasureReward || kind == RoomKind.ItemReward;
        }

        public NodeDeckOptions BuildNodeDeckOptions(int nodeIndex, string monsterDeckId)
        {
            var catalog = CatalogOrNull();
            if (catalog == null)
            {
                return NodeDeckOptions.CreateDefaultBattle();
            }

            EnsureOpeningRoomAssigned(catalog);

            // shell 层把整局全局节点索引（1..FinalFloor*NodesPerFloor）传入；规则表按层内节点定义。
            // 归一化为层内展示节点，避免层 2/3 落入 fallback（无怪物/无层主）。
            var floorNodeIndex = ((nodeIndex - 1) % RunModel.NodesPerFloor) + 1;
            var rule = FindNodeRule(catalog, floorNodeIndex);
            if (rule == null)
            {
                var fallback = new NodeDeckOptions();
                AddRunPlayerSideCards(catalog, fallback);
                AppendRegularTrapCards(catalog, fallback);
                AppendOpeningLeaveTrapCard(catalog, fallback);
                return fallback;
            }

            MonsterDeckDefinition deck = FindMonsterDeck(catalog, monsterDeckId);
            if (deck == null)
            {
                var fallback = new NodeDeckOptions();
                AddRunPlayerSideCards(catalog, fallback);
                AppendRegularTrapCards(catalog, fallback);
                AppendOpeningLeaveTrapCard(catalog, fallback);
                return fallback;
            }

            var options = new NodeDeckOptions
            {
                PlayerOpeningCount = 3,
                EnemyOpeningCount = 3,
                // 序列 5 = 层主；RequireElite 时 OpeningDeal 必选全部层主（精英）且第一波直摆上盘（Boss Counter 亦计 Elite）。
                RequireElite = rule.Seq5Count > 0
            };

            AddRunPlayerSideCards(catalog, options);

            for (var sequence = 1; sequence <= 5; sequence++)
            {
                AddSequenceCards(catalog, deck, options, sequence, rule.GetSequenceCount(sequence));
            }

            AppendRoomOpeningInjectMonsterCards(catalog, deck, options);
            AppendRegularTrapCards(catalog, options);
            AppendOpeningLeaveTrapCard(catalog, options);
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

            // ADR-0022：进房即改数值已退役；开局注入在 BuildNodeDeckOptions 执行。
            // 房间内交互（奖励池 / 商店货架）仍可在此 enqueue。

            if (room.Kind == RoomKind.Shop)
            {
                var shelves = BuildShopShelves();
                pipeline.Enqueue(new OfferShopSessionAction(
                    shelves,
                    OfferShopSessionAction.DefaultRefreshPriceGold));
                return pipeline.RunToCompletion();
            }

            if (room.Kind == RoomKind.Tavern)
            {
                pipeline.Enqueue(new OfferTavernSessionAction(
                    BuildTavernServices(),
                    OfferShopSessionAction.DefaultRefreshPriceGold));
                return pipeline.RunToCompletion();
            }

            if (room.Kind == RoomKind.TreasureReward)
            {
                pipeline.Enqueue(new OfferSpecialRewardSessionAction(
                    PendingChoiceModel.TreasureRewardPoolId,
                    BuildTreasureRewardShelves()));
                return pipeline.RunToCompletion();
            }

            if (room.Kind == RoomKind.ItemReward)
            {
                pipeline.Enqueue(new OfferSpecialRewardSessionAction(
                    PendingChoiceModel.ItemRewardPoolId,
                    BuildItemRewardShelves()));
                return pipeline.RunToCompletion();
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

        /// <summary>
        /// 商店货架：随机属性道具 / 恢复药水 / 食品；容量未满时追加「道具牌格升级」（#109 / ADR-0025）。
        /// 宝箱卡不上商店货架（仅宝箱房 / 宝箱奖励房 / 击杀掉落渠道，ADR-0033 修订）。
        /// </summary>
        public IReadOnlyList<RewardEntry> BuildShopShelves()
        {
            var shelves = new List<RewardEntry>(4)
            {
                new RewardEntry(RollShopAttributeDefId(), CardKind.HelpCard, 1, 1),
                new RewardEntry(ShopPotionDefId, CardKind.HelpCard, 1, 1),
                new RewardEntry(ShopFoodDefId, CardKind.HelpCard, 1, 1),
            };
            var player = this.GetModel<PlayerModel>();
            if (player != null && player.ItemSlotsCapacity < PlayerModel.MaxItemSlotsCapacity)
            {
                shelves.Add(new RewardEntry(ShopExpandItemSlotsDefId, CardKind.HelpCard, 1, 1));
            }

            return shelves;
        }

        /// <summary>
        /// 卡店服务：道具数值强化 / 道具卡固定 / 道具卡扩容（设计案房间.md · #93）。
        /// 本货架已购项由 <paramref name="excludeDefIds"/> 省略，刷新货架后恢复。
        /// </summary>
        public IReadOnlyList<RewardEntry> BuildTavernServices(IReadOnlyCollection<string> excludeDefIds = null)
        {
            var services = new List<RewardEntry>(3);
            TryAddTavernService(services, TavernUpgradeDefId, excludeDefIds);
            TryAddTavernService(services, TavernFixItemDefId, excludeDefIds);
            TryAddTavernService(services, TavernExpandDefId, excludeDefIds);
            return services;
        }

        private static void TryAddTavernService(
            List<RewardEntry> services,
            string defId,
            IReadOnlyCollection<string> excludeDefIds)
        {
            if (excludeDefIds != null)
            {
                foreach (var sold in excludeDefIds)
                {
                    if (string.Equals(sold, defId, StringComparison.Ordinal))
                    {
                        return;
                    }
                }
            }

            services.Add(new RewardEntry(defId, CardKind.HelpCard, 1, 1));
        }

        /// <summary>宝箱奖励房：1 宝箱卡 + 3 随机道具卡（#94）。</summary>
        public IReadOnlyList<RewardEntry> BuildTreasureRewardShelves()
        {
            var shelves = new List<RewardEntry>(4)
            {
                new RewardEntry(ShopChestDefId, CardKind.HelpCard, 1, 1),
            };
            AppendRandomItemShelves(shelves, 3);
            return shelves;
        }

        /// <summary>道具奖励房：2 属性道具卡 + 3 随机道具卡（#94）。</summary>
        public IReadOnlyList<RewardEntry> BuildItemRewardShelves()
        {
            var shelves = new List<RewardEntry>(5)
            {
                new RewardEntry(RollShopAttributeDefId(), CardKind.HelpCard, 1, 1),
                new RewardEntry(RollShopAttributeDefId(), CardKind.HelpCard, 1, 1),
            };
            AppendRandomItemShelves(shelves, 3);
            return shelves;
        }

        public IReadOnlyList<RewardEntry> BuildTavernFixItemCandidates(int count = 3)
        {
            if (count <= 0)
            {
                return new List<RewardEntry>();
            }

            var pool = this.GetModel<PlayerModel>().ItemSourcePoolDefIds;
            if (pool == null || pool.Count == 0)
            {
                return new List<RewardEntry>();
            }

            var rng = this.GetUtility<IRngUtility>();
            var list = new List<RewardEntry>(count);
            for (var i = 0; i < count; i++)
            {
                var defId = pool[rng.Range(0, pool.Count)];
                if (string.IsNullOrEmpty(defId))
                {
                    continue;
                }

                list.Add(new RewardEntry(defId, CardKind.HelpCard, 1, 1));
            }

            return list;
        }

        public const string ShopChestDefId = "help.common_chest_card";
        public const string ShopPotionDefId = "help.healing_potion";
        public const string ShopFoodDefId = "help.food_card";
        /// <summary>商店「道具牌格升级」选项（#109）；勿与卡店 <see cref="TavernExpandDefId"/> 混淆。</summary>
        public const string ShopExpandItemSlotsDefId = "ExpandItemSlots";
        public const int ShopExpandItemSlotsPriceGold = 50;

        public const string TavernUpgradeDefId = "UpgradeItemStats";
        public const string TavernFixItemDefId = "FixItem";
        public const string TavernExpandDefId = "ExpandItemCapacity";
        public const int TavernServicePriceGold = 50;
        public const int TavernUpgradeStatDelta = 3;

        /// <summary>属性房三选二会话（#136，已退役流程）遗留常量。</summary>
        public const int AttributePickCount = 2;

        private void AppendRandomItemShelves(List<RewardEntry> shelves, int count)
        {
            for (var i = 0; i < count; i++)
            {
                shelves.Add(new RewardEntry(RollRandomItemDefId(), CardKind.HelpCard, 1, 1));
            }
        }

        private string RollShopAttributeDefId()
        {
            // 血量 40% / 加甲 40% / 加攻 20%（与属性房池一致）
            var roll = this.GetUtility<IRngUtility>().Range(0, 100);
            if (roll < 40)
            {
                return "help.hp_card";
            }

            if (roll < 80)
            {
                return "help.armor_card";
            }

            return "help.attack_card";
        }

        /// <summary>从玩家道具来源池均匀抽一张；池空时回退属性三卡。</summary>
        private string RollRandomItemDefId()
        {
            var pool = this.GetModel<PlayerModel>().ItemSourcePoolDefIds;
            if (pool != null && pool.Count > 0)
            {
                return pool[this.GetUtility<IRngUtility>().Range(0, pool.Count)];
            }

            return RollShopAttributeDefId();
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

                if (card.Counters.Get(CoreCounterKeys.Boss) > 0)
                {
                    // 设计案：层主死亡洗入 1 金色宝箱卡 + 2 金币卡（固定，不走奖池随机）。
                    result.Add(new ShuffleIntoDrawPileAction("help.golden_chest_card", CardKind.HelpCard, 1, false));
                    result.Add(new ShuffleIntoDrawPileAction("help.gold_card", CardKind.HelpCard, 2, false));
                    continue;
                }

                if (card.Counters.Get(CoreCounterKeys.Elite) <= 0)
                {
                    continue;
                }

                var rewards = RollPool("kill.elite");
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
            var rng = this.GetUtility<IRngUtility>();

            var fixedCards = player.FixedItemCardDefIds;
            var fixedCount = fixedCards.Count;
            var randomCount = Math.Max(0, player.ItemDeckCapacity - fixedCount);

            // 1) 固定卡占塞卡预算（ItemDeckCapacity）；扩容加总量、固定加可控性，二者互斥权衡。
            for (var i = 0; i < fixedCount; i++)
            {
                TryAddPlayerCard(catalog, content, options, fixedCards[i]);
            }

            // 2) 剩余预算从来源池随机生成
            var pool = player.ItemSourcePoolDefIds;
            if (pool.Count > 0 && randomCount > 0)
            {
                for (var i = 0; i < randomCount; i++)
                {
                    var defId = pool[rng.Range(0, pool.Count)];
                    TryAddPlayerCard(catalog, content, options, defId);
                }
            }

            // 3) 房间注入卡不占 ItemDeckCapacity（ADR-0022 / #95）
            AppendRoomOpeningInjectPlayerCards(catalog, content, options);
        }

        /// <summary>
        /// 节点 1/5 随机战斗房、节点 8 层主：在装填前把 <see cref="RunModel.Room"/> 对齐到本节点房间来源。
        /// </summary>
        private void EnsureOpeningRoomAssigned(GameContentCatalog catalog)
        {
            if (catalog == null)
            {
                return;
            }

            var run = this.GetModel<RunModel>();
            var schedule = MapNodeProgression.GetScheduleOrDefault(run.NodeIndex.Value);
            if (schedule.RoomSource == NodeRoomSource.Boss)
            {
                run.Room.Value = RoomKind.Boss;
                return;
            }

            if (schedule.RoomSource != NodeRoomSource.RandomBattle)
            {
                return;
            }

            if (IsBattleOfferRoom(run.Room.Value))
            {
                return;
            }

            var rolled = RollRoomChoicesFromPool(
                count: 1,
                includeAllWeighted: false,
                allowElite: false,
                predicate: kind => IsBattleOfferRoom(kind) && kind != RoomKind.Elite);
            if (rolled.Count > 0)
            {
                run.Room.Value = rolled[0];
            }
            else
            {
                run.Room.Value = RoomKind.Gold;
            }
        }

        private void AppendRoomOpeningInjectPlayerCards(
            GameContentCatalog catalog,
            IContentSystem content,
            NodeDeckOptions options)
        {
            if (catalog == null || content == null || options == null)
            {
                return;
            }

            RoomDefinition room;
            if (!TryGetOpeningRoom(catalog, out room))
            {
                return;
            }

            var injects = room.OpeningInjects;
            for (var i = 0; i < injects.Count; i++)
            {
                var inject = injects[i];
                if (inject == null || inject.Side != RoomInjectSide.Player)
                {
                    continue;
                }

                var defIds = RollInjectCardDefIds(inject);
                for (var j = 0; j < defIds.Count; j++)
                {
                    TryAddPlayerCard(catalog, content, options, defIds[j]);
                }
            }
        }

        private void AppendRoomOpeningInjectMonsterCards(
            GameContentCatalog catalog,
            MonsterDeckDefinition deck,
            NodeDeckOptions options)
        {
            if (catalog == null || deck == null || options == null)
            {
                return;
            }

            RoomDefinition room;
            if (!TryGetOpeningRoom(catalog, out room))
            {
                return;
            }

            var injects = room.OpeningInjects;
            for (var i = 0; i < injects.Count; i++)
            {
                var inject = injects[i];
                if (inject == null || inject.Side != RoomInjectSide.Monster)
                {
                    continue;
                }

                if (inject.SourceKind == RoomInjectSourceKind.FloorMonsterSequence)
                {
                    AddSequenceCards(catalog, deck, options, inject.MonsterSequence, inject.Count);
                    continue;
                }

                var defIds = RollInjectCardDefIds(inject);
                var content = this.GetSystem<IContentSystem>();
                for (var j = 0; j < defIds.Count; j++)
                {
                    if (string.IsNullOrEmpty(defIds[j]) || !catalog.Cards.ContainsKey(defIds[j]))
                    {
                        continue;
                    }

                    options.AddEnemyCard(content.CreateDraft(defIds[j]));
                }
            }
        }

        /// <summary>
        /// #112 / ADR-0026：普通战斗房开局编入离开机关（层主房改击破开局层主后洗入）。
        /// </summary>
        private void AppendOpeningLeaveTrapCard(GameContentCatalog catalog, NodeDeckOptions options)
        {
            if (catalog == null || options == null)
            {
                return;
            }

            var run = this.GetModel<RunModel>();
            if (run.Room.Value == RoomKind.Boss)
            {
                return;
            }

            var content = this.GetSystem<IContentSystem>();
            var draft = content.CreateDraft(RegularTrapPool.LeaveTrapDefId);
            if (draft == null || draft.Kind != CardKind.Trap)
            {
                return;
            }

            options.AddEnemyCard(draft);
        }

        /// <summary>
        /// #135：正式战斗开局随机注入三张常规机关（Core 契约 <see cref="RegularTrapPool"/>）。
        /// 离开机关/特殊机关不入池；不足三张按池量；QuickTest \1–\9 定向注入在此之上叠加。
        /// </summary>
        private void AppendRegularTrapCards(GameContentCatalog catalog, NodeDeckOptions options)
        {
            if (catalog == null || options == null)
            {
                return;
            }

            var defIds = RegularTrapPool.RollRegularTrapDefIds(
                catalog,
                this.GetUtility<IRngUtility>(),
                RegularTrapPool.PerBattleCount);
            if (defIds == null || defIds.Count == 0)
            {
                return;
            }

            var content = this.GetSystem<IContentSystem>();
            for (var i = 0; i < defIds.Count; i++)
            {
                var draft = content.CreateDraft(defIds[i]);
                if (draft == null || draft.Kind != CardKind.Trap)
                {
                    continue;
                }

                options.AddEnemyCard(draft);
            }
        }

        private bool TryGetOpeningRoom(GameContentCatalog catalog, out RoomDefinition room)
        {
            room = null;
            var kind = this.GetModel<RunModel>().Room.Value;
            if (kind == RoomKind.None || catalog == null)
            {
                return false;
            }

            return catalog.Rewards.TryGetRoom(kind, out room) && room != null;
        }

        private List<string> RollInjectCardDefIds(RoomInjectDeclaration inject)
        {
            var result = new List<string>();
            if (inject == null || inject.Count <= 0)
            {
                return result;
            }

            switch (inject.SourceKind)
            {
                case RoomInjectSourceKind.FixedCard:
                    if (!string.IsNullOrEmpty(inject.CardDefId))
                    {
                        for (var i = 0; i < inject.Count; i++)
                        {
                            result.Add(inject.CardDefId);
                        }
                    }

                    break;

                case RoomInjectSourceKind.WeightedPool:
                    RollWeightedInjectPool(inject, result);
                    break;
            }

            return result;
        }

        private void RollWeightedInjectPool(RoomInjectDeclaration inject, List<string> result)
        {
            var pool = new List<RoomInjectPoolOption>();
            for (var i = 0; i < inject.Pool.Count; i++)
            {
                var option = inject.Pool[i];
                if (option != null && !string.IsNullOrEmpty(option.CardDefId) && option.Weight > 0)
                {
                    pool.Add(option);
                }
            }

            for (var n = 0; n < inject.Count && pool.Count > 0; n++)
            {
                var index = RollWeightedInjectPoolIndex(pool);
                result.Add(pool[index].CardDefId);
                if (!inject.AllowDuplicates)
                {
                    pool.RemoveAt(index);
                }
            }
        }

        private int RollWeightedInjectPoolIndex(IReadOnlyList<RoomInjectPoolOption> pool)
        {
            var total = 0;
            for (var i = 0; i < pool.Count; i++)
            {
                total += pool[i].Weight;
            }

            var roll = this.GetUtility<IRngUtility>().Range(0, total);
            var cursor = 0;
            for (var i = 0; i < pool.Count; i++)
            {
                cursor += pool[i].Weight;
                if (roll < cursor)
                {
                    return i;
                }
            }

            return pool.Count - 1;
        }

        private static void TryAddPlayerCard(
            GameContentCatalog catalog,
            IContentSystem content,
            NodeDeckOptions options,
            string defId)
        {
            if (string.IsNullOrEmpty(defId) || catalog == null || !catalog.Cards.ContainsKey(defId))
            {
                return;
            }

            options.AddPlayerCard(content.CreateDraft(defId));
        }

        private int AddSequenceCards(
            GameContentCatalog catalog,
            MonsterDeckDefinition deck,
            NodeDeckOptions options,
            int sequence,
            int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            var candidates = FilterMonstersBySequence(catalog, deck, sequence);
            for (var i = 0; i < count && candidates.Count > 0; i++)
            {
                var selected = candidates[this.GetUtility<IRngUtility>().Range(0, candidates.Count)];
                options.AddEnemyCard(this.GetSystem<IContentSystem>().CreateDraft(selected.DefId));
            }

            return count;
        }

        private static List<CardContentDefinition> FilterMonstersBySequence(
            GameContentCatalog catalog,
            MonsterDeckDefinition deck,
            int sequence)
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

                if (card.Sequence == sequence)
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

        private static List<MonsterDeckDefinition> CollectFloorMonsterDeckCandidates(
            GameContentCatalog catalog,
            RunModel run,
            int floor,
            bool excludeUsed)
        {
            var matches = new List<MonsterDeckDefinition>();
            foreach (var pair in catalog.MonsterDecks)
            {
                var deck = pair.Value;
                if (deck == null || !MonsterDeckFloorPool.IsPlayableDifficulty(deck.Kind))
                {
                    continue;
                }

                if (!MonsterDeckFloorPool.MatchesFloor(deck.Kind, floor))
                {
                    continue;
                }

                if (excludeUsed && run.IsMonsterDeckUsed(deck.Id))
                {
                    continue;
                }

                matches.Add(deck);
            }

            // Dictionary 枚举序依赖装载插入；按 Id 排序后同种子跨平台抽组一致。
            matches.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return matches;
        }

        private MonsterDeckDefinition FindMonsterDeck(GameContentCatalog catalog, string deckId)
        {
            MonsterDeckDefinition deck;
            if (!string.IsNullOrEmpty(deckId) && catalog.MonsterDecks.TryGetValue(deckId, out deck))
            {
                return deck;
            }

            var run = this.GetModel<RunModel>();
            if (!string.IsNullOrEmpty(run.FloorMonsterDeckId.Value)
                && catalog.MonsterDecks.TryGetValue(run.FloorMonsterDeckId.Value, out deck))
            {
                return deck;
            }

            var floor = run.Floor != null ? run.Floor.Value : 1;
            var matches = CollectFloorMonsterDeckCandidates(catalog, run, floor, excludeUsed: true);

            if (matches.Count == 0)
            {
                // 本层难度池用尽时回退：同层难度档内允许复用（仍排除 Reserve / Unknown）。
                matches = CollectFloorMonsterDeckCandidates(catalog, run, floor, excludeUsed: false);
            }

            if (matches.Count == 0)
            {
                return null;
            }

            var pick = matches[this.GetUtility<IRngUtility>().Range(0, matches.Count)];
            run.TryBindFloorMonsterDeck(pick.Id);
            return pick;
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
