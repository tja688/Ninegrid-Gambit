namespace NineGrid.Cards
{
    /// <summary>
    /// 卡牌底盘与四套卡面模板的权威资产路径（#13/#14）。
    /// 旧路径 Assets/Prefabs/Standard Card.prefab 已失效，一律改用本常量。
    /// </summary>
    public static class CardChassisPaths
    {
        public const string ChassisPrefab = "Assets/Prefabs/老Standard Card.prefab";
        public const string AvatarFacePrefab = "Assets/Prefabs/玩家卡标准模板.prefab";
        public const string MonsterFacePrefab = "Assets/Prefabs/怪物卡标准模板.prefab";
        public const string ItemFacePrefab = "Assets/Prefabs/道具卡标准模版.prefab";
        public const string RelicFacePrefab = "Assets/Prefabs/遗物卡标准模版.prefab";
        public const string SlotRegistryAsset = "Assets/Arts/Cards/CardFaceSlotRegistry.asset";
        public const string DescriptionInlineIconStyleAsset =
            "Assets/Arts/Cards/CardFaceDescriptionInlineIconStyle.asset";
    }
}
