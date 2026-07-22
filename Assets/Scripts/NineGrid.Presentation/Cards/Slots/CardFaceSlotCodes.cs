namespace NineGrid.Cards.Slots
{
    /// <summary>
    /// 卡面装配槽稳定代号（代码内部键；编辑器悬停显示中文注释）。
    /// 与内容视觉 Catalog 字段映射：Main_Icon↔icon，Face_Background↔face，
    /// Back_*↔backBorder/backShirt/backLogo。
    /// </summary>
    public static class CardFaceSlotCodes
    {
        public const string MainIcon = "Main_Icon";
        public const string FaceBackground = "Face_Background";
        public const string BackBorder = "Back_Border";
        public const string BackShirt = "Back_Shirt";
        public const string BackLogo = "Back_Logo";
        public const string ActionIcon = "Action_Icon";
        public const string Name = "Name";
        public const string Attack = "Attack";
        public const string Armor = "Armor";
        public const string Hp = "Hp";
        public const string ActionCount = "Action_Count";
        public const string BasicDescription = "Basic_Description";
    }
}
