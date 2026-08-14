namespace NineGrid.Flow.BattleLog
{
    /// <summary>
    /// 战斗日志富文本配色。色相跟着飘字走（伤害红 / 治疗绿 / 护甲灰绿，见
    /// <see cref="NineGrid.Flow.DamageNumberManagerSingleton"/>），但明度按面板底图压暗：
    /// 飘字打在暗色场地上，日志打在浅米色羊皮纸面板上，照搬飘字的亮绿亮黄会糊成一片。
    /// </summary>
    public static class BattleLogPalette
    {
        /// <summary>血量伤害，飘字 hpDamageColor 原值（本身够深，浅底可读）。</summary>
        public const string Damage = "#B62F2B";

        /// <summary>治疗，飘字 healColor 的压暗版。</summary>
        public const string Heal = "#1E7A2C";

        /// <summary>护甲增减，飘字 armorDamageColor 原值。</summary>
        public const string Armor = "#4C6B61";

        public const string Kill = "#8E1B0C";
        public const string Gold = "#A87508";
        public const string Attack = "#9A5410";
        public const string Relic = "#61389A";

        /// <summary>攻击力以外的基础数值增减（生命上限 / 回复 / 互动范围等）。</summary>
        public const string BaseStat = "#1F5C86";

        /// <summary>效果 / 技能 / 遗物等来源名。</summary>
        public const string Source = "#7A4E0A";

        /// <summary>正文默认色（角色名与连接词），也是 TMP 组件的基色。</summary>
        public const string Body = "#3A2E22";

        /// <summary>楼层房间分段标题。</summary>
        public const string Section = "#4A3B2A";

        /// <summary>剩余值、括号补充等次要信息。</summary>
        public const string Muted = "#7A6A55";

        public static string Wrap(string hex, string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : "<color=" + hex + ">" + text + "</color>";
        }
    }
}
