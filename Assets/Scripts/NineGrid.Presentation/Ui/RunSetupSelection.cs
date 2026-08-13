using UnityEngine;

namespace NineGrid.Presentation.Ui
{
    /// <summary>
    /// 开局选择状态（选人界面写、结算面板读）：当前只有难度选择。
    /// 难度目前全部路由到默认（普通）数据，仅作展示记录；后续实装难度路由时在此扩展。
    /// </summary>
    public static class RunSetupSelection
    {
        public const string NormalDifficultyId = "normal";

        public static string DifficultyId { get; private set; } = NormalDifficultyId;
        public static string DifficultyLabel { get; private set; } = "普通";

        /// <summary>选人界面难度选项的图标精灵（结算面板「此次对局所选的难度」直接复用）。</summary>
        public static Sprite DifficultyIcon { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ResetToDefault();
        }

        public static void SetDifficulty(string id, string label, Sprite icon)
        {
            DifficultyId = string.IsNullOrEmpty(id) ? NormalDifficultyId : id;
            DifficultyLabel = label ?? string.Empty;
            DifficultyIcon = icon;
        }

        public static void ResetToDefault()
        {
            DifficultyId = NormalDifficultyId;
            DifficultyLabel = "普通";
            DifficultyIcon = null;
        }
    }
}
