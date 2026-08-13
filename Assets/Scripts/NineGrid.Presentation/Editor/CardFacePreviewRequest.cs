#if UNITY_EDITOR
using NineGrid.Cards.Presentation;
using UnityEngine;
using NineGrid.Cards;

namespace NineGrid.Presentation.Editor
{
    /// <summary>
    /// 编辑器假投影输入：会话草稿 Sprite / 描述可直接灌入，不必先保存 xlsx。
    /// </summary>
    public sealed class CardFacePreviewRequest
    {
        public string DefId = string.Empty;
        public CardPresentationKind Kind = CardPresentationKind.Unknown;
        public string DisplayName = string.Empty;
        public string BasicDescription = string.Empty;
        /// <summary>Room 图标预制体路径；空则按 DefId 默认映射。</summary>
        public string RoomIconPrefabPath = string.Empty;

        public Sprite MainIcon;
        public Sprite FaceBackground;
        public Sprite BackBorder;
        public Sprite BackShirt;
        public Sprite BackLogo;

        public int Attack;
        public int Armor;
        public int Hp;
        public int ActionCount;
        public bool ShowActionCount;
        public bool FaceUp = true;

        /// <summary>内容稀有度（驱动卡框/横幅变体与道具卡副图标档位预览）；None = 不切换。</summary>
        public NineGrid.Core.Content.ContentRarity Rarity = NineGrid.Core.Content.ContentRarity.None;

        public CardPresentationSnapshot ToSnapshot()
        {
            return new CardPresentationSnapshot
            {
                Kind = Kind,
                DefId = DefId ?? string.Empty,
                DisplayName = DisplayName ?? string.Empty,
                BasicDescription = BasicDescription ?? string.Empty,
                MainIcon = MainIcon,
                FaceBackground = FaceBackground,
                BackBorder = BackBorder,
                BackShirt = BackShirt,
                BackLogo = BackLogo,
                Attack = Mathf.Max(0, Attack),
                Armor = Mathf.Max(0, Armor),
                Hp = Mathf.Max(0, Hp),
                ActionCount = Mathf.Max(0, ActionCount),
                ShowActionCount = ShowActionCount,
                FaceUp = FaceUp,
                Rarity = Rarity,
            };
        }
    }
}
#endif
