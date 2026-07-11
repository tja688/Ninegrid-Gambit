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
        NodeDeckOptions BuildNodeDeckOptions(int nodeIndex, string monsterDeckId);
        int ResolveRoom(RoomKind roomKind);

        /// <summary>
        /// 重绑系统级 Trigger（InitialGameFactory 若 Clear 了 TriggerSystem 后必须调用）。
        /// </summary>
        void RebindSystemTriggers();
    }

    public sealed class RewardSystem : AbstractSystem, IRewardSystem
    {
        private IUnRegister mKillRewardUnregister;

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

            var result = new List<RewardEntry>();
            var candidates = new List<RewardEntry>(pool.Entries);
            var count = pool.PickCount;
            for (var i = 0; i < count && candidates.Count > 0; i++)
            {
                var index = RollWeightedIndex(candidates);
                result.Add(candidates[index]);
                candidates.RemoveAt(index);
            }

            return result;
        }

        public IReadOnlyList<RoomKind> RollRoomChoices(int count)
        {
            var catalog = CatalogOrNull();
            if (catalog == null || count <= 0)
            {
                return new RoomKind[0];
            }

            var rooms = new List<RoomDefinition>();
            foreach (var pair in catalog.Rewards.Rooms)
            {
                if (pair.Value.Weight > 0)
                {
                    rooms.Add(pair.Value);
                }
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
                return NodeDeckOptions.CreateDefaultBattle();
            }

            MonsterDeckDefinition deck = FindMonsterDeck(catalog, monsterDeckId, rule.DeckKind);
            if (deck == null)
            {
                return NodeDeckOptions.CreateDefaultBattle();
            }

            var options = new NodeDeckOptions
            {
                PlayerOpeningCount = 3,
                EnemyOpeningCount = 3,
                RequireElite = rule.EliteCount > 0 || rule.BossCount > 0
            };

            AddDefaultPlayerCards(catalog, options);

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

        private void AddDefaultPlayerCards(GameContentCatalog catalog, NodeDeckOptions options)
        {
            var player = this.GetModel<PlayerModel>();
            var professionId = string.IsNullOrEmpty(player.ProfessionId.Value)
                ? ProfessionCatalog.Default.DefId
                : player.ProfessionId.Value;
            ProfessionCatalog.AppendInitialPlayerCards(
                this.GetSystem<IContentSystem>(),
                catalog,
                options,
                professionId);
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

                if (card.Kind != CardKind.Monster || card.IsReserve)
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

            foreach (var pair in catalog.MonsterDecks)
            {
                if (pair.Value.Kind == kind)
                {
                    return pair.Value;
                }
            }

            return null;
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

        private int RollWeightedIndex(IReadOnlyList<RewardEntry> entries)
        {
            var total = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                total += entries[i].Weight > 0 ? entries[i].Weight : 0;
            }

            if (total <= 0)
            {
                return 0;
            }

            var roll = this.GetUtility<IRngUtility>().Range(0, total);
            var cursor = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                cursor += entries[i].Weight > 0 ? entries[i].Weight : 0;
                if (roll < cursor)
                {
                    return i;
                }
            }

            return entries.Count - 1;
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
