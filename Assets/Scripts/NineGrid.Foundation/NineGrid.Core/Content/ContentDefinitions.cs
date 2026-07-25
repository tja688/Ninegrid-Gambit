using System.Collections.Generic;
using NineGrid.Core.Effects;

namespace NineGrid.Core.Content
{
    public static class ContentConfigKeys
    {
        public const string DefaultCatalog = "tableNine.defaultCatalog";
    }

    public enum ContentRarity
    {
        None,
        White,
        Blue,
        Gold,
        Red
    }

    public enum ContentImplementationState
    {
        Implemented,
        PendingAtom,
        RawDesignOnly
    }

    public enum MonsterDeckKind
    {
        Unknown,
        WeakElite,
        StrongElite,
        Boss,
        Reserve
    }

    public sealed class ContentStatLine
    {
        public int MaxHp { get; set; }
        public int Hp { get; set; }
        public int Attack { get; set; }
        public int Armor { get; set; }
        public int Recovery { get; set; }

        public bool IsEmpty
        {
            get { return MaxHp == 0 && Hp == 0 && Attack == 0 && Armor == 0 && Recovery == 0; }
        }
    }

    public sealed class ContentEffectDefinition
    {
        public ContentEffectDefinition(
            string id,
            EffectContainerType containerType,
            string json,
            ContentImplementationState state,
            string designText)
        {
            Id = id ?? string.Empty;
            ContainerType = containerType;
            Json = json ?? string.Empty;
            State = state;
            DesignText = designText ?? string.Empty;
        }

        public string Id { get; private set; }
        public EffectContainerType ContainerType { get; private set; }
        public string Json { get; private set; }
        public ContentImplementationState State { get; private set; }
        public string DesignText { get; private set; }
    }

    public sealed class CardContentDefinition
    {
        private readonly List<string> mTags = new List<string>();
        private readonly List<string> mEffectIds = new List<string>();
        private readonly List<string> mSkillIds = new List<string>();

        public CardContentDefinition(string defId, string displayName, CardKind kind)
        {
            DefId = defId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Kind = kind;
            Stats = new ContentStatLine();
        }

        public string DefId { get; private set; }
        public string DisplayName { get; private set; }
        public CardKind Kind { get; private set; }
        public ContentRarity Rarity { get; set; }
        public int Price { get; set; }
        /// <summary>怪物击杀金币；0 表示回退 Economy.MonsterRemovedGold。</summary>
        public int KillGold { get; set; }
        public int Level { get; set; }
        public bool IsElite { get; set; }
        public bool IsBoss { get; set; }
        public bool IsReserve { get; set; }
        public string DeckId { get; set; }
        public ContentStatLine Stats { get; private set; }

        public IReadOnlyList<string> Tags
        {
            get { return mTags; }
        }

        public IReadOnlyList<string> EffectIds
        {
            get { return mEffectIds; }
        }

        public IReadOnlyList<string> SkillIds
        {
            get { return mSkillIds; }
        }

        public CardContentDefinition WithRarity(ContentRarity rarity)
        {
            Rarity = rarity;
            return this;
        }

        public CardContentDefinition WithPrice(int price)
        {
            Price = price;
            return this;
        }

        /// <summary>
        /// 覆盖显示名（供 CardPresentation JSON overlay；非空白才应由调用方写入）。
        /// </summary>
        public CardContentDefinition WithDisplayName(string displayName)
        {
            DisplayName = displayName ?? string.Empty;
            return this;
        }

        public CardContentDefinition WithStats(int maxHp, int attack, int armor)
        {
            Stats.MaxHp = maxHp;
            Stats.Hp = maxHp;
            Stats.Attack = attack;
            Stats.Armor = armor;
            return this;
        }

        public CardContentDefinition WithLevel(int level)
        {
            Level = level;
            return this;
        }

        public CardContentDefinition AsElite()
        {
            IsElite = true;
            return this;
        }

        public CardContentDefinition AsBoss()
        {
            IsBoss = true;
            return this;
        }

        public CardContentDefinition AsReserve()
        {
            IsReserve = true;
            return this;
        }

        public CardContentDefinition InDeck(string deckId)
        {
            DeckId = deckId ?? string.Empty;
            return this;
        }

        public CardContentDefinition AddTag(string tag)
        {
            if (!string.IsNullOrEmpty(tag) && !mTags.Contains(tag))
            {
                mTags.Add(tag);
            }

            return this;
        }

        public CardContentDefinition AddEffect(string effectId)
        {
            if (!string.IsNullOrEmpty(effectId) && !mEffectIds.Contains(effectId))
            {
                mEffectIds.Add(effectId);
            }

            return this;
        }

        public CardContentDefinition AddSkill(string skillId)
        {
            if (!string.IsNullOrEmpty(skillId) && !mSkillIds.Contains(skillId))
            {
                mSkillIds.Add(skillId);
            }

            return this;
        }
    }

    public sealed class SkillContentDefinition
    {
        private readonly List<string> mEffectIds = new List<string>();

        public SkillContentDefinition(string defId, string displayName, EffectContainerType containerType, string designText)
        {
            DefId = defId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            ContainerType = containerType;
            DesignText = designText ?? string.Empty;
        }

        public string DefId { get; private set; }
        public string DisplayName { get; private set; }
        public EffectContainerType ContainerType { get; private set; }
        public string DesignText { get; private set; }

        public IReadOnlyList<string> EffectIds
        {
            get { return mEffectIds; }
        }

        public SkillContentDefinition AddEffect(string effectId)
        {
            if (!string.IsNullOrEmpty(effectId) && !mEffectIds.Contains(effectId))
            {
                mEffectIds.Add(effectId);
            }

            return this;
        }
    }

    public sealed class RelicContentDefinition
    {
        private readonly List<string> mTags = new List<string>();
        private readonly List<string> mEffectIds = new List<string>();

        public RelicContentDefinition(string defId, string displayName, ContentRarity rarity, string designText)
        {
            DefId = defId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Rarity = rarity;
            DesignText = designText ?? string.Empty;
        }

        public string DefId { get; private set; }
        public string DisplayName { get; private set; }
        public ContentRarity Rarity { get; private set; }
        public string DesignText { get; private set; }

        public IReadOnlyList<string> Tags
        {
            get { return mTags; }
        }

        public IReadOnlyList<string> EffectIds
        {
            get { return mEffectIds; }
        }

        public RelicContentDefinition AddTag(string tag)
        {
            if (!string.IsNullOrEmpty(tag) && !mTags.Contains(tag))
            {
                mTags.Add(tag);
            }

            return this;
        }

        public RelicContentDefinition AddEffect(string effectId)
        {
            if (!string.IsNullOrEmpty(effectId) && !mEffectIds.Contains(effectId))
            {
                mEffectIds.Add(effectId);
            }

            return this;
        }
    }

    public sealed class RewardEntry
    {
        public RewardEntry(string defId, CardKind kind, int weight, int count)
        {
            DefId = defId ?? string.Empty;
            Kind = kind;
            Weight = weight;
            Count = count <= 0 ? 1 : count;
        }

        public string DefId { get; private set; }
        public CardKind Kind { get; private set; }
        public int Weight { get; private set; }
        public int Count { get; private set; }
    }

    public sealed class RewardPoolDefinition
    {
        private readonly List<RewardEntry> mEntries = new List<RewardEntry>();

        public RewardPoolDefinition(string id, int pickCount)
        {
            Id = id ?? string.Empty;
            PickCount = pickCount <= 0 ? 1 : pickCount;
        }

        public string Id { get; private set; }
        public int PickCount { get; private set; }

        public IReadOnlyList<RewardEntry> Entries
        {
            get { return mEntries; }
        }

        public RewardPoolDefinition Add(string defId, CardKind kind, int weight)
        {
            return Add(defId, kind, weight, 1);
        }

        public RewardPoolDefinition Add(string defId, CardKind kind, int weight, int count)
        {
            mEntries.Add(new RewardEntry(defId, kind, weight, count));
            return this;
        }
    }

    public sealed class MonsterDeckDefinition
    {
        private readonly List<string> mMonsterDefIds = new List<string>();

        public MonsterDeckDefinition(string id, string displayName, MonsterDeckKind kind)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Kind = kind;
        }

        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public MonsterDeckKind Kind { get; private set; }

        public IReadOnlyList<string> MonsterDefIds
        {
            get { return mMonsterDefIds; }
        }

        public MonsterDeckDefinition AddMonster(string defId)
        {
            if (!string.IsNullOrEmpty(defId) && !mMonsterDefIds.Contains(defId))
            {
                mMonsterDefIds.Add(defId);
            }

            return this;
        }
    }

    public sealed class NodeDeckRule
    {
        public int NodeIndex { get; set; }
        public int TotalMonsterCount { get; set; }
        public int Level1Min { get; set; }
        public int Level1Max { get; set; }
        public int Level2Min { get; set; }
        public int Level2Max { get; set; }
        public int Level3Min { get; set; }
        public int Level3Max { get; set; }
        public int EliteCount { get; set; }
        public int BossCount { get; set; }
        public MonsterDeckKind DeckKind { get; set; }
    }

    public sealed class RoomDefinition
    {
        public RoomDefinition(RoomKind kind, string displayName)
        {
            Kind = kind;
            DisplayName = displayName ?? string.Empty;
        }

        public RoomKind Kind { get; private set; }
        public string DisplayName { get; private set; }
        public int Weight { get; set; }
        public int GoldDelta { get; set; }
        public int MaxHpDelta { get; set; }
        public bool HealToFull { get; set; }
        public string RewardPoolId { get; set; }
        public int ShopOfferCount { get; set; }
    }

    public sealed class EconomyConfig
    {
        public EconomyConfig()
        {
            MonsterRemovedGold = 5;
            UnusedHelpCardGold = 10;
            SkipHelpChoiceGold = 10;
            SkipRelicChoiceGold = 20;
            DiscardRelicGold = 20;
            ShopDeleteHelpCardGold = 10;
        }

        public int MonsterRemovedGold { get; set; }
        public int UnusedHelpCardGold { get; set; }
        public int SkipHelpChoiceGold { get; set; }
        public int SkipRelicChoiceGold { get; set; }
        public int DiscardRelicGold { get; set; }
        public int ShopDeleteHelpCardGold { get; set; }
    }

    public sealed class RewardConfig
    {
        private readonly Dictionary<string, RewardPoolDefinition> mPools = new Dictionary<string, RewardPoolDefinition>();
        private readonly Dictionary<RoomKind, RoomDefinition> mRooms = new Dictionary<RoomKind, RoomDefinition>();
        private readonly List<NodeDeckRule> mNodeDeckRules = new List<NodeDeckRule>();

        public IReadOnlyDictionary<string, RewardPoolDefinition> Pools
        {
            get { return mPools; }
        }

        public IReadOnlyDictionary<RoomKind, RoomDefinition> Rooms
        {
            get { return mRooms; }
        }

        public IReadOnlyList<NodeDeckRule> NodeDeckRules
        {
            get { return mNodeDeckRules; }
        }

        public RewardConfig AddPool(RewardPoolDefinition pool)
        {
            if (pool != null && !string.IsNullOrEmpty(pool.Id))
            {
                mPools[pool.Id] = pool;
            }

            return this;
        }

        public RewardConfig AddRoom(RoomDefinition room)
        {
            if (room != null)
            {
                mRooms[room.Kind] = room;
            }

            return this;
        }

        public RewardConfig AddNodeRule(NodeDeckRule rule)
        {
            if (rule != null)
            {
                mNodeDeckRules.Add(rule);
            }

            return this;
        }

        public bool TryGetPool(string id, out RewardPoolDefinition pool)
        {
            return mPools.TryGetValue(id ?? string.Empty, out pool);
        }

        public bool TryGetRoom(RoomKind kind, out RoomDefinition room)
        {
            return mRooms.TryGetValue(kind, out room);
        }
    }

    public sealed class GameContentCatalog
    {
        private readonly Dictionary<string, CardContentDefinition> mCards = new Dictionary<string, CardContentDefinition>();
        private readonly Dictionary<string, SkillContentDefinition> mSkills = new Dictionary<string, SkillContentDefinition>();
        private readonly Dictionary<string, RelicContentDefinition> mRelics = new Dictionary<string, RelicContentDefinition>();
        private readonly Dictionary<string, ContentEffectDefinition> mEffects = new Dictionary<string, ContentEffectDefinition>();
        private readonly Dictionary<string, MonsterDeckDefinition> mMonsterDecks = new Dictionary<string, MonsterDeckDefinition>();

        public GameContentCatalog()
        {
            Economy = new EconomyConfig();
            Rewards = new RewardConfig();
        }

        public EconomyConfig Economy { get; private set; }
        public RewardConfig Rewards { get; private set; }

        public IReadOnlyDictionary<string, CardContentDefinition> Cards
        {
            get { return mCards; }
        }

        public IReadOnlyDictionary<string, SkillContentDefinition> Skills
        {
            get { return mSkills; }
        }

        public IReadOnlyDictionary<string, RelicContentDefinition> Relics
        {
            get { return mRelics; }
        }

        public IReadOnlyDictionary<string, ContentEffectDefinition> Effects
        {
            get { return mEffects; }
        }

        public IReadOnlyDictionary<string, MonsterDeckDefinition> MonsterDecks
        {
            get { return mMonsterDecks; }
        }

        public GameContentCatalog AddCard(CardContentDefinition definition)
        {
            if (definition != null && !string.IsNullOrEmpty(definition.DefId))
            {
                mCards[definition.DefId] = definition;
            }

            return this;
        }

        public GameContentCatalog AddSkill(SkillContentDefinition definition)
        {
            if (definition != null && !string.IsNullOrEmpty(definition.DefId))
            {
                mSkills[definition.DefId] = definition;
            }

            return this;
        }

        public GameContentCatalog AddRelic(RelicContentDefinition definition)
        {
            if (definition != null && !string.IsNullOrEmpty(definition.DefId))
            {
                mRelics[definition.DefId] = definition;
            }

            return this;
        }

        public GameContentCatalog AddEffect(ContentEffectDefinition definition)
        {
            if (definition != null && !string.IsNullOrEmpty(definition.Id))
            {
                mEffects[definition.Id] = definition;
            }

            return this;
        }

        public GameContentCatalog AddMonsterDeck(MonsterDeckDefinition definition)
        {
            if (definition != null && !string.IsNullOrEmpty(definition.Id))
            {
                mMonsterDecks[definition.Id] = definition;
            }

            return this;
        }

        public bool TryGetCard(string defId, out CardContentDefinition definition)
        {
            return mCards.TryGetValue(defId ?? string.Empty, out definition);
        }

        public bool TryGetSkill(string defId, out SkillContentDefinition definition)
        {
            return mSkills.TryGetValue(defId ?? string.Empty, out definition);
        }

        public bool TryGetRelic(string defId, out RelicContentDefinition definition)
        {
            return mRelics.TryGetValue(defId ?? string.Empty, out definition);
        }

        public bool TryGetEffect(string effectId, out ContentEffectDefinition definition)
        {
            return mEffects.TryGetValue(effectId ?? string.Empty, out definition);
        }
    }
}
