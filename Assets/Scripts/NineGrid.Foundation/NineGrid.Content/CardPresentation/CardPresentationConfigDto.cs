using System;

namespace NineGrid.Content.CardPresentation
{
    /// <summary>
    /// 卡牌内容/表现 JSON（JsonUtility 友好：无 Dictionary，字段 camelCase）。
    /// schemaVersion≥2：身份、数值、效果挂载与表现引用同文件，经
    /// <see cref="NineGrid.Content.ContentJsonCatalogProjector"/> 投影进
    /// <c>GameContentCatalog</c>（HelpCard/Monster/Skill/Relic/Deck/Room）。
    /// ChoiceOption / Avatar 等仅表现 contentId 可有 JSON，不进玩法 Catalog。
    /// 效果 DSL / 奖励池 / 经济 / 节点规则由 <c>ContentCatalogTableLoader</c> 加载（ADR-0008 / #69）。
    /// </summary>
    [Serializable]
    public sealed class CardPresentationConfigDto
    {
        public int schemaVersion = 1;
        public string contentId;
        /// <summary>卡种类；投影以本字段为准（HelpCard/Monster/Skill/Relic/Deck/Room/…）。</summary>
        public string kind;
        /// <summary>deck 归属；怪物卡投影写入 Catalog.DeckId。</summary>
        public string deckId;
        /// <summary>显示名；schema≥2 投影写入 Catalog，已投影卡种不再由 Overlay 覆盖。</summary>
        public string displayName;
        /// <summary>描述权威（有值时覆盖 TbContentVisual.description）；Relic/Skill 亦作 designText。</summary>
        public string description;
        /// <summary>金币：HelpCard→Price；Monster→KillGold；其它 Price（&gt;0）。</summary>
        public int gold;
        /// <summary>基础数值；schema≥2 投影写入；action 仅表现。</summary>
        public CardPresentationStatsDto stats;
        /// <summary>槽位图权威（有路径则不再读 ContentVisual SO）。</summary>
        public CardPresentationSpritesDto sprites;
        public CardPresentationMainVisualDto mainVisual;
        public CardPresentationAnimationsDto animations;
        public CardPresentationExtraSlotDto[] extraSlots;
        /// <summary>稀有度（如 White/Blue/Gold/Red）；schema≥2 时投影进 Catalog。</summary>
        public string rarity;
        /// <summary>标签列表；schema≥2 时投影进 Catalog。</summary>
        public string[] tags;
        /// <summary>
        /// 效果装配引用（ADR-0009 / #70）：templateId + 实参；投影时解析进 Catalog.Effects，
        /// 并把 mount id 写入 EffectIds。优先于 legacy <see cref="effectIds"/>。
        /// </summary>
        public EffectAssemblyDto[] effectAssemblies;
        /// <summary>legacy 效果挂载 id 列表；无 effectAssemblies 时仍投影。</summary>
        public string[] effectIds;
        /// <summary>怪物技能挂载；Monster schema≥2 投影进 Catalog.SkillIds。</summary>
        public string[] skillIds;
        /// <summary>怪物等级；Monster schema≥2。</summary>
        public int level;
        public bool isElite;
        public bool isBoss;
        public bool isReserve;
        /// <summary>技能容器类型（如 MonsterSkill）；Skill schema≥2。</summary>
        public string containerType;
        /// <summary>牌组种类（WeakElite/StrongElite/Boss/Reserve）；Deck schema≥2。</summary>
        public string deckKind;
        /// <summary>牌组怪物 defId 列表；Deck schema≥2。</summary>
        public string[] monsterDefIds;
        /// <summary>房间权重与效果字段；Room schema≥2。</summary>
        public int weight;
        public int goldDelta;
        public int maxHpDelta;
        public bool healToFull;
        public string rewardPoolId;
        public int shopOfferCount;
    }

    [Serializable]
    public sealed class CardPresentationStatsDto
    {
        public int hp;
        public int armor;
        public int attack;
        public int action;
        /// <summary>怪物恢复；schema≥2 投影进 Catalog.Stats.Recovery。</summary>
        public int recovery;
    }

    [Serializable]
    public sealed class CardPresentationSpritesDto
    {
        public string mainIcon;
        public string faceBackground;
        public string cardFrame;
        public string banner;
        public string backBorder;
        public string backShirt;
        public string backLogo;
    }

    [Serializable]
    public sealed class CardPresentationMainVisualDto
    {
        public float offsetX;
        public float offsetY;
        public float uniformScale = 1f;
    }

    [Serializable]
    public sealed class CardPresentationAnimationsDto
    {
        public float defaultFps = 8f;
        public CardPresentationAnimSlotDto[] slots;
    }

    [Serializable]
    public sealed class CardPresentationAnimSlotDto
    {
        /// <summary>idle / attack / hurt / death / lunch</summary>
        public string id;
        /// <summary>none | folder | atlas</summary>
        public string sourceType;
        public string path;
        public float offsetX;
        public float offsetY;
    }

    [Serializable]
    public sealed class CardPresentationExtraSlotDto
    {
        public string code;
        public string path;
    }

    [Serializable]
    public sealed class CardPresentationVec2Dto
    {
        public float x;
        public float y;
    }

    /// <summary>卡上装配引用：模板 id + 实参（argsJson 为对象字面量，无默认值间接层）。</summary>
    [Serializable]
    public sealed class EffectAssemblyDto
    {
        /// <summary>挂载实例 id（写入 Catalog.EffectIds / Effects 键）。</summary>
        public string id;
        public string templateId;
        /// <summary>分类/检索用；不再做 typeTag 门禁。</summary>
        public string containerType;
        /// <summary>实参 JSON 对象，如 {"amount":10}。</summary>
        public string argsJson;
    }
}
