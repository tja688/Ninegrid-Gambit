using UnityEngine;

namespace NineGrid.Presentation.Ui
{
    /// <summary>
    /// 开局选择状态（选人界面写、结算面板读）：当前只有难度选择。
    /// 难度写入 <see cref="RunModel.DifficultyId"/>，影响怪物数值叠加与环境变体（ADR-0053）。
    /// </summary>
    public static class RunSetupSelection
    {
        public const string DefaultDifficultyId = NineGrid.Core.Content.RunDifficultyIds.Default;
        public const string NormalDifficultyId = NineGrid.Core.Content.RunDifficultyIds.Normal;

        public static string DifficultyId { get; private set; } = DefaultDifficultyId;
        public static string DifficultyLabel { get; private set; } = "冒险";

        /// <summary>选人界面难度选项的图标精灵（结算面板「此次对局所选的难度」直接复用）。</summary>
        public static Sprite DifficultyIcon { get; private set; }

        /// <summary>选人界面所选职业 id（默认战士）。</summary>
        public static string ProfessionId { get; private set; } = NineGrid.Core.ProfessionCatalog.Jester;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ResetToDefault();
        }

        public static void SetDifficulty(string id, string label, Sprite icon)
        {
            DifficultyId = string.IsNullOrEmpty(id) ? DefaultDifficultyId : id;
            DifficultyLabel = !string.IsNullOrEmpty(label) ? label : GetDefaultLabel(DifficultyId);
            DifficultyIcon = icon;
        }

        public static void SetProfession(string id)
        {
            ProfessionId = string.IsNullOrEmpty(id) ? NineGrid.Core.ProfessionCatalog.Jester : id;
        }

        public static void ResetToDefault()
        {
            DifficultyId = DefaultDifficultyId;
            DifficultyLabel = GetDefaultLabel(DefaultDifficultyId);
            DifficultyIcon = null;
            ProfessionId = NineGrid.Core.ProfessionCatalog.Jester;
        }

        public static string GetDefaultLabel(string difficultyId)
        {
            if (string.Equals(difficultyId, NineGrid.Core.Content.RunDifficultyIds.Hard, System.StringComparison.OrdinalIgnoreCase))
            {
                return "血色";
            }

            if (string.Equals(difficultyId, NineGrid.Core.Content.RunDifficultyIds.Normal, System.StringComparison.OrdinalIgnoreCase))
            {
                return "旅途";
            }

            return "冒险";
        }
    }
}
