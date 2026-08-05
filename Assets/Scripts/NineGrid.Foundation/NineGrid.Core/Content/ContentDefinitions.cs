using System.Collections.Generic;
using System.Linq;
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

    /// <summary>
    /// 功能角色粗轴（ADR-0009）：攻 / 防 / 功能；投放均衡用，代号主键。
    /// </summary>
    public enum ContentRole
    {
        None,
        Attack,
        Defense,
        Utility
    }

    public enum ContentImplementationState
    {
        Implemented,
        PendingAtom,
        RawDesignOnly
    }

    /// <summary>
    /// 主题怪物卡组标记（ADR-0022）。仅 <see cref="Reserve"/> 有语义（不参与每层随机）；
    /// WeakElite/StrongElite/Boss 为历史分档残留，加载后视同可参与主题池（与 <see cref="Unknown"/> 同等）。
    /// </summary>
    public enum MonsterDeckKind
    {
        Unknown = 0,
        /// <summary>历史残留；不再决定哪层用哪档。</summary>
        WeakElite = 1,
        /// <summary>历史残留；不再决定哪层用哪档。</summary>
        StrongElite = 2,
        /// <summary>历史残留；不再决定哪层用哪档。</summary>
        Boss = 3,
        /// <summary>不参与每层主题随机。</summary>
        Reserve = 4
    }

    /// <summary>怪物等级（ADR-0022）：只区分普通 / 层主；梯队用 <see cref="CardContentDefinition.Sequence"/>。</summary>
    public enum MonsterRank
    {
        Normal = 0,
        FloorBoss = 1
    }

    public sealed class ContentStatLine
    {
        public int MaxHp { get; set; }
        public int Hp { get; set; }
        public int Attack { get; set; }
        public int Armor { get; set; }
        public int Recovery { get; set; }
        /// <summary>攻击模式频率 N（ADR-0011）；「无」为 0，近战系 3，远程 5。</summary>
        public int Action { get; set; }

        public bool IsEmpty
        {
            get
            {
                return MaxHp == 0 && Hp == 0 && Attack == 0 && Armor == 0 && Recovery == 0 && Action == 0;
            }
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
        /// <summary>运行时 Level Counter 来源；怪物侧与 <see cref="Sequence"/> 对齐。</summary>
        public int Level { get; set; }
        /// <summary>主题卡组内序列 1–5；0 表示未赋。</summary>
        public int Sequence { get; set; }
        /// <summary>普通 / 层主（与 <see cref="IsBoss"/> 对齐）。</summary>
        public MonsterRank Rank { get; set; }
        public bool IsElite { get; set; }
        public bool IsBoss { get; set; }
        public bool IsReserve { get; set; }
        public string DeckId { get; set; }
        /// <summary>功能角色（攻/防/功能）；代号主键，中文仅显示名。</summary>
        public ContentRole Role { get; set; }
        public ContentStatLine Stats { get; private set; }
        /// <summary>怪物攻击模式（ADR-0011）；非 Monster 保持 Unspecified。</summary>
        public AttackPattern AttackPattern { get; set; }

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

        public CardContentDefinition WithSequence(int sequence)
        {
            Sequence = sequence;
            if (sequence > 0)
            {
                Level = sequence;
            }

            return this;
        }

        public CardContentDefinition WithRank(MonsterRank rank)
        {
            Rank = rank;
            if (rank == MonsterRank.FloorBoss)
            {
                IsBoss = true;
            }

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
            Rank = MonsterRank.FloorBoss;
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

        public CardContentDefinition WithRole(ContentRole role)
        {
            Role = role;
            return this;
        }

        public CardContentDefinition WithAttackPattern(AttackPattern attackPattern)
        {
            AttackPattern = attackPattern;
            if (Kind == CardKind.Monster)
            {
                Stats.Action = AttackPatternRules.Frequency(attackPattern);
            }

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

    /// <summary>
    /// 遗物卡组约定：live 进奖池/授予；archive 仅保留 JSON 参考（#115）。
    /// </summary>
    public static class RelicDecks
    {
        public const string Live = "deck.relic";
        public const string Archive = "deck.relic_archive";

        public static bool IsArchive(string deckId)
        {
            return !string.IsNullOrEmpty(deckId)
                && string.Equals(deckId, Archive, System.StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 道具卡组约定（#139）：live 进奖池/道具来源池/房间注入；archive 仅保留 JSON 参考。
    /// 非策划现行道具卡（破击锤/血液转换/倍增塔/盾击教程/属性提升/庇佑/瞭望塔）归归档卡组，
    /// 不参与奖池展开与职业道具来源池（<see cref="RewardPoolQueryExpander"/> 与 ProfessionCatalog 均按此约定过滤）。
    /// </summary>
    public static class HelpCardDecks
    {
        public const string Live = "deck.help";
        public const string Archive = "deck.help_archive";

        public static bool IsArchive(string deckId)
        {
            return !string.IsNullOrEmpty(deckId)
                && string.Equals(deckId, Archive, System.StringComparison.OrdinalIgnoreCase);
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
        /// <summary>功能角色（攻/防/功能）；代号主键。</summary>
        public ContentRole Role { get; set; }
        /// <summary>卡组归属（决定卡背）；遗物默认 <see cref="RelicDecks.Live"/>。</summary>
        public string DeckId { get; set; }
        public string DesignText { get; private set; }

        public IReadOnlyList<string> Tags
        {
            get { return mTags; }
        }

        public IReadOnlyList<string> EffectIds
        {
            get { return mEffectIds; }
        }

        public RelicContentDefinition WithRole(ContentRole role)
        {
            Role = role;
            return this;
        }

        public RelicContentDefinition InDeck(string deckId)
        {
            DeckId = deckId ?? string.Empty;
            return this;
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
            : this(defId, kind, weight, count, 0, 0, 0)
        {
        }

        public RewardEntry(string defId, CardKind kind, int weight, int count, int attack, int armor, int hp)
        {
            DefId = defId ?? string.Empty;
            Kind = kind;
            Weight = weight;
            Count = count <= 0 ? 1 : count;
            Attack = attack < 0 ? 0 : attack;
            Armor = armor < 0 ? 0 : armor;
            Hp = hp < 0 ? 0 : hp;
        }

        public string DefId { get; private set; }
        public CardKind Kind { get; private set; }
        public int Weight { get; private set; }
        public int Count { get; private set; }

        /// <summary>候选项展示用攻击绝对值（「拿了就是」）。</summary>
        public int Attack { get; private set; }

        /// <summary>候选项展示用护甲绝对值（「拿了就是」）。</summary>
        public int Armor { get; private set; }

        /// <summary>候选项展示用生命绝对值（「拿了就是」）。</summary>
        public int Hp { get; private set; }

        public RewardEntry WithFaceProjection(int attack, int armor, int hp)
        {
            return new RewardEntry(DefId, Kind, Weight, Count, attack, armor, hp);
        }
    }

    /// <summary>
    /// 奖池查询规则（ADR-0009 / #71）：按 kind/rarity/role/tags 从 Catalog 展开候选，替代手工白名单。
    /// </summary>
    public sealed class RewardPoolQueryRule
    {
        private readonly List<ContentRarity> mRarities = new List<ContentRarity>();
        private readonly List<ContentRole> mRoles = new List<ContentRole>();
        private readonly List<string> mTagsAny = new List<string>();

        public CardKind Kind { get; set; }
        public int DefaultWeight { get; set; }
        public int RarityWeightWhite { get; set; }
        public int RarityWeightBlue { get; set; }
        public int RarityWeightGold { get; set; }
        public int RarityWeightRed { get; set; }
        public int BalanceMinAttack { get; set; }
        public int BalanceMinDefense { get; set; }

        public IReadOnlyList<ContentRarity> Rarities
        {
            get { return mRarities; }
        }

        public IReadOnlyList<ContentRole> Roles
        {
            get { return mRoles; }
        }

        public IReadOnlyList<string> TagsAny
        {
            get { return mTagsAny; }
        }

        public bool HasRarityWeights
        {
            get
            {
                return RarityWeightWhite > 0
                    || RarityWeightBlue > 0
                    || RarityWeightGold > 0
                    || RarityWeightRed > 0;
            }
        }

        public RewardPoolQueryRule AllowRarity(ContentRarity rarity)
        {
            if (rarity != ContentRarity.None && !mRarities.Contains(rarity))
            {
                mRarities.Add(rarity);
            }

            return this;
        }

        public RewardPoolQueryRule AllowRole(ContentRole role)
        {
            if (role != ContentRole.None && !mRoles.Contains(role))
            {
                mRoles.Add(role);
            }

            return this;
        }

        public RewardPoolQueryRule AllowTag(string tag)
        {
            if (!string.IsNullOrEmpty(tag) && !mTagsAny.Contains(tag))
            {
                mTagsAny.Add(tag);
            }

            return this;
        }
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

        /// <summary>非空时由 <see cref="RewardPoolQueryExpander"/> 从 Catalog 展开 Entries。</summary>
        public RewardPoolQueryRule Query { get; set; }

        public IReadOnlyList<RewardEntry> Entries
        {
            get { return mEntries; }
        }

        public RewardPoolDefinition WithQuery(RewardPoolQueryRule query)
        {
            Query = query;
            return this;
        }

        public RewardPoolDefinition ClearEntries()
        {
            mEntries.Clear();
            return this;
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

    /// <summary>按查询规则从 Catalog 展开奖池候选条目。</summary>
    public static class RewardPoolQueryExpander
    {
        public static void ExpandAll(GameContentCatalog catalog)
        {
            if (catalog == null)
            {
                return;
            }

            foreach (var pair in catalog.Rewards.Pools)
            {
                Expand(catalog, pair.Value);
            }
        }

        public static void Expand(GameContentCatalog catalog, RewardPoolDefinition pool)
        {
            if (catalog == null || pool == null || pool.Query == null)
            {
                return;
            }

            pool.ClearEntries();
            var query = pool.Query;
            var weight = query.DefaultWeight > 0 ? query.DefaultWeight : 1;

            if (query.Kind == CardKind.Relic)
            {
                foreach (var pair in catalog.Relics)
                {
                    var relic = pair.Value;
                    if (relic == null || !MatchesRelic(relic, query))
                    {
                        continue;
                    }

                    pool.Add(relic.DefId, CardKind.Relic, weight, 1);
                }

                return;
            }

            foreach (var pair in catalog.Cards)
            {
                var card = pair.Value;
                if (card == null || card.Kind != query.Kind || !MatchesCard(card, query))
                {
                    continue;
                }

                pool.Add(card.DefId, card.Kind, weight, 1);
            }
        }

        private static bool MatchesCard(CardContentDefinition card, RewardPoolQueryRule query)
        {
            if (card == null)
            {
                return false;
            }

            if (card.Kind == CardKind.HelpCard && HelpCardDecks.IsArchive(card.DeckId))
            {
                return false;
            }

            if (query.Rarities.Count > 0 && !query.Rarities.Contains(card.Rarity))
            {
                return false;
            }

            if (query.Roles.Count > 0 && !query.Roles.Contains(card.Role))
            {
                return false;
            }

            return MatchesTags(card.Tags, query.TagsAny);
        }

        private static bool MatchesRelic(RelicContentDefinition relic, RewardPoolQueryRule query)
        {
            if (RelicDecks.IsArchive(relic.DeckId))
            {
                return false;
            }

            if (query.Rarities.Count > 0 && !query.Rarities.Contains(relic.Rarity))
            {
                return false;
            }

            if (query.Roles.Count > 0 && !query.Roles.Contains(relic.Role))
            {
                return false;
            }

            return MatchesTags(relic.Tags, query.TagsAny);
        }

        private static bool MatchesTags(IReadOnlyList<string> tags, IReadOnlyList<string> tagsAny)
        {
            if (tagsAny == null || tagsAny.Count == 0)
            {
                return true;
            }

            if (tags == null || tags.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < tagsAny.Count; i++)
            {
                var want = tagsAny[i];
                for (var j = 0; j < tags.Count; j++)
                {
                    if (string.Equals(tags[j], want, System.StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
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

    /// <summary>节点 → 序列 1..5 各抽几张（ADR-0022）。节点 4/7 无行。</summary>
    public sealed class NodeDeckRule
    {
        public int NodeIndex { get; set; }
        public int Seq1Count { get; set; }
        public int Seq2Count { get; set; }
        public int Seq3Count { get; set; }
        public int Seq4Count { get; set; }
        public int Seq5Count { get; set; }

        public int TotalMonsterCount
        {
            get { return Seq1Count + Seq2Count + Seq3Count + Seq4Count + Seq5Count; }
        }

        public int GetSequenceCount(int sequence)
        {
            switch (sequence)
            {
                case 1: return Seq1Count;
                case 2: return Seq2Count;
                case 3: return Seq3Count;
                case 4: return Seq4Count;
                case 5: return Seq5Count;
                default: return 0;
            }
        }
    }

    /// <summary>房间开局注入目标侧（ADR-0022）。</summary>
    public enum RoomInjectSide
    {
        Player = 1,
        Monster = 2
    }

    /// <summary>房间开局注入来源（ADR-0022）。由 <c>RewardSystem.BuildNodeDeckOptions</c> 执行。</summary>
    public enum RoomInjectSourceKind
    {
        FixedCard = 1,
        WeightedPool = 2,
        FloorMonsterSequence = 3
    }

    public sealed class RoomInjectPoolOption
    {
        public RoomInjectPoolOption(string cardDefId, int weight)
        {
            CardDefId = cardDefId ?? string.Empty;
            Weight = weight;
        }

        public string CardDefId { get; private set; }
        public int Weight { get; private set; }
    }

    /// <summary>房间开局注入声明：往玩家侧 / 怪物侧塞哪些卡（ADR-0022）。</summary>
    public sealed class RoomInjectDeclaration
    {
        private readonly List<RoomInjectPoolOption> mPool = new List<RoomInjectPoolOption>();

        public RoomInjectSide Side { get; set; }
        public RoomInjectSourceKind SourceKind { get; set; }
        public string CardDefId { get; set; }
        public int Count { get; set; }
        public bool AllowDuplicates { get; set; }
        public int MonsterSequence { get; set; }

        public IReadOnlyList<RoomInjectPoolOption> Pool
        {
            get { return mPool; }
        }

        public RoomInjectDeclaration AddPoolOption(string cardDefId, int weight)
        {
            mPool.Add(new RoomInjectPoolOption(cardDefId, weight));
            return this;
        }

        /// <summary>复制既有池选项（构造临时抽取声明用，不改动原声明）。</summary>
        public RoomInjectDeclaration AddPoolOptions(IReadOnlyList<RoomInjectPoolOption> options)
        {
            if (options != null)
            {
                for (var i = 0; i < options.Count; i++)
                {
                    var option = options[i];
                    if (option != null)
                    {
                        mPool.Add(new RoomInjectPoolOption(option.CardDefId, option.Weight));
                    }
                }
            }

            return this;
        }
    }

    public sealed class RoomDefinition
    {
        private readonly List<RoomInjectDeclaration> mOpeningInjects = new List<RoomInjectDeclaration>();

        public RoomDefinition(RoomKind kind, string displayName)
        {
            Kind = kind;
            DisplayName = displayName ?? string.Empty;
        }

        public RoomKind Kind { get; private set; }
        public string DisplayName { get; private set; }
        public int Weight { get; set; }
        public string RewardPoolId { get; set; }
        public int ShopOfferCount { get; set; }

        /// <summary>开局注入声明；空列表表示显式无注入。</summary>
        public IReadOnlyList<RoomInjectDeclaration> OpeningInjects
        {
            get { return mOpeningInjects; }
        }

        public RoomDefinition AddOpeningInject(RoomInjectDeclaration inject)
        {
            if (inject != null)
            {
                mOpeningInjects.Add(inject);
            }

            return this;
        }
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
            RecycleItemSlotGold = 10;
        }

        public int MonsterRemovedGold { get; set; }
        public int UnusedHelpCardGold { get; set; }
        public int SkipHelpChoiceGold { get; set; }
        public int SkipRelicChoiceGold { get; set; }
        public int DiscardRelicGold { get; set; }
        public int ShopDeleteHelpCardGold { get; set; }
        /// <summary>#110 / ADR-0025：道具卡格主动回收兑金（每张固定）。</summary>
        public int RecycleItemSlotGold { get; set; }
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
