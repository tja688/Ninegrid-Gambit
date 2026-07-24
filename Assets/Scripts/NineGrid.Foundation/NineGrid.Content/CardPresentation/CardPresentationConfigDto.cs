using System;

namespace NineGrid.Content.CardPresentation
{
    /// <summary>
    /// 卡牌表现 JSON 权威配置（JsonUtility 友好：无 Dictionary，字段 camelCase）。
    /// </summary>
    [Serializable]
    public sealed class CardPresentationConfigDto
    {
        public int schemaVersion = 1;
        public string contentId;
        /// <summary>Monster, Avatar, HelpCard, Relic, etc.</summary>
        public string kind;
        public string deckId;
        public string displayName;
        public string description;
        public int gold;
        public CardPresentationStatsDto stats;
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
