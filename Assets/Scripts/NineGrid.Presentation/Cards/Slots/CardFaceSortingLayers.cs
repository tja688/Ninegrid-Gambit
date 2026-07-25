namespace NineGrid.Cards.Slots
{
    /// <summary>
    /// 卡面模板通层 sortingOrder 分区（0–99）。卡级 SortingGroup 仍由底盘黑盒独占；
    /// 此处仅组内相对序。编辑器 <see cref="CardFaceSortingOrderValidator"/> 禁止同层重复。
    /// </summary>
    public static class CardFaceSortingLayers
    {
        public const int Min = 0;
        public const int Max = 99;

        // 卡背三件套
        public const int BackBorder = 0;
        public const int BackShirt = 5;
        public const int BackLogo = 8;
        public const int BackDecorA = 10;
        public const int BackDecorB = 12;

        // 正面底图与框
        public const int FaceBackground = 15;
        public const int CardFrame = 22;
        public const int Banner = 26;

        /// <summary>主视图 Mask 精灵（运行时会与 <see cref="MainIcon"/> 对齐）。</summary>
        public const int MainIconMask = 34;

        /// <summary>主图标；Mask 运行时会与此 order 同步。</summary>
        public const int MainIcon = 35;

        public const int IntroPanel = 40;
        public const int DescriptionPanel = 45;
        public const int InlineIconSlot = 48;

        public const int RankBadgeA = 50;
        public const int RankBadgeB = 51;
        public const int RankBadgeC = 52;

        public const int AttackIcon = 52;
        public const int AttackIconGem = 53;
        public const int AttackIconObject = 54;

        public const int ArmorIcon = 56;
        public const int HpIcon = 60;
        public const int ActionIcon = 64;
        public const int StatusIcon = 68;

        public const int ActionCountSprite = 72;

        public const int AttackValueText = 76;
        public const int ArmorValueText = 80;
        public const int HpValueText = 84;
        public const int ActionCountText = 88;

        public const int NameText = 92;
        public const int DescriptionText = 96;

        public const int TopOverlay = 99;

        public const int MainIconMaskBackOffset = -32;
        public const int MainIconMaskFrontOffset = 32;
    }
}
