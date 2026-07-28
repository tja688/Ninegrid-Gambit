using System;

namespace NineGrid.Content.CardPresentation
{
    /// <summary>
    /// 卡牌内容/表现 JSON（JsonUtility 友好：无 Dictionary，字段 camelCase）。
    /// 帮助卡（schemaVersion≥2）：身份、数值、效果挂载与表现引用同文件，经
    /// <see cref="NineGrid.Content.HelpCardJsonCatalogProjector"/> 投影进
    /// <c>GameContentCatalog</c>；Overlay 不再盖写帮助卡 stats/gold/displayName。
    /// 非帮助卡：重叠表现字段权威见 <see cref="CardPresentationAuthority"/>；
    /// kind/deckId/rarity/tags/effectIds 玩法身份仍以 Luban 为准（#68/#69 迁移）。
    /// </summary>
    [Serializable]
    public sealed class CardPresentationConfigDto
    {
        public int schemaVersion = 1;
        public string contentId;
        /// <summary>卡种类；帮助卡投影以本字段为准（须为 HelpCard）。</summary>
        public string kind;
        /// <summary>deck 归属；帮助卡多为空；非帮助卡玩法仍以 Luban 为准。</summary>
        public string deckId;
        /// <summary>显示名；帮助卡经投影写入 Catalog，非帮助卡可由 Overlay 覆盖。</summary>
        public string displayName;
        /// <summary>描述权威（有值时覆盖 TbContentVisual.description）。</summary>
        public string description;
        /// <summary>金币：帮助卡→Price（投影）；非帮助卡 Overlay 怪物→KillGold、其它→Price（&gt;0）。</summary>
        public int gold;
        /// <summary>基础数值：帮助卡投影写入；非帮助卡 Overlay 对 hp/attack/armor &gt;0 才覆盖；action 仅表现。</summary>
        public CardPresentationStatsDto stats;
        /// <summary>槽位图权威（有路径则不再读 ContentVisual SO）。</summary>
        public CardPresentationSpritesDto sprites;
        public CardPresentationMainVisualDto mainVisual;
        public CardPresentationAnimationsDto animations;
        public CardPresentationExtraSlotDto[] extraSlots;
        /// <summary>稀有度（如 White/Blue/Gold/Red）；帮助卡 schema≥2 时投影进 Catalog。</summary>
        public string rarity;
        /// <summary>标签列表；帮助卡 schema≥2 时投影进 Catalog。</summary>
        public string[] tags;
        /// <summary>效果挂载 id 列表；帮助卡 schema≥2 时投影进 Catalog（效果 DSL 本体仍可在 Luban）。</summary>
        public string[] effectIds;
    }

    [Serializable]
    public sealed class CardPresentationStatsDto
    {
        public int hp;
        public int armor;
        public int attack;
        public int action;
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
}
