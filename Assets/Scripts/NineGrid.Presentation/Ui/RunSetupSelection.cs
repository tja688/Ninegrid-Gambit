using UnityEngine;

namespace NineGrid.Presentation.Ui
{
    /// <summary>
    /// 开局选择状态（选人界面写、结算面板读）：当前只有难度选择。
    /// 难度写入 <see cref="RunModel.DifficultyId"/>，影响怪物数值叠加与环境变体（ADR-0053）。
    /// </summary>
    public static class RunSetupSelection
    {
        public const string NormalDifficultyId = NineGrid.Core.Content.RunDifficultyIds.Normal;

        public static string DifficultyId { get; private set; } = NormalDifficultyId;
        public static string DifficultyLabel { get; private set; } = "旅途";

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
            DifficultyLabel = !string.IsNullOrEmpty(label) ? label : GetDefaultLabel(DifficultyId);
            DifficultyIcon = icon;
        }

        public static void ResetToDefault()
        {
            DifficultyId = NormalDifficultyId;
            DifficultyLabel = GetDefaultLabel(NormalDifficultyId);
            DifficultyIcon = null;
        }

        public static string GetDefaultLabel(string difficultyId)
        {
            if (string.Equals(difficultyId, NineGrid.Core.Content.RunDifficultyIds.Hard, System.StringComparison.OrdinalIgnoreCase))
            {
                return "血色";
            }

            if (string.Equals(difficultyId, NineGrid.Core.Content.RunDifficultyIds.Advanced, System.StringComparison.OrdinalIgnoreCase))
            {
                return "冒险";
            }

            return "旅途";
        }
    }
}
