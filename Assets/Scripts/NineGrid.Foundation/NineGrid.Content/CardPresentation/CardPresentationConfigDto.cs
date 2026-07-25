using System;

namespace NineGrid.Content.CardPresentation
{
    /// <summary>
    /// 卡牌表现 JSON 权威配置（JsonUtility 友好：无 Dictionary，字段 camelCase）。
    /// 重叠字段权威见 <see cref="CardPresentationAuthority"/>：有本配置时
    /// displayName / description / gold / stats.hp|attack|armor / sprites / animations
    /// 以 JSON 为准；kind/deckId 仅为镜像，玩法身份仍属 Luban。
    /// </summary>
    [Serializable]
    public sealed class CardPresentationConfigDto
    {
        public int schemaVersion = 1;
        public string contentId;
        /// <summary>镜像字段；玩法 CardKind 仍以 Luban TbCard 为准。</summary>
        public string kind;
        /// <summary>镜像字段；玩法 deck 归属仍以 Luban TbCard 为准。</summary>
        public string deckId;
        /// <summary>显示名权威（有值时覆盖 Luban display_name）。</summary>
        public string displayName;
        /// <summary>描述权威（有值时覆盖 TbContentVisual.description）。</summary>
        public string description;
        /// <summary>金币权威：怪物→KillGold，其它→Price（&gt;0 才覆盖）。</summary>
        public int gold;
        /// <summary>基础数值权威：hp/attack/armor（&gt;0 才覆盖）；action 仅表现。</summary>
        public CardPresentationStatsDto stats;
        /// <summary>槽位图权威（有路径则不再读 ContentVisual SO）。</summary>
        public CardPresentationSpritesDto sprites;
        public CardPresentationMainVisualDto mainVisual;
        public CardPresentationAnimationsDto animations;
        public CardPresentationExtraSlotDto[] extraSlots;
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
