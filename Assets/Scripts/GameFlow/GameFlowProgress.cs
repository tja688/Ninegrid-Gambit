using UnityEngine;

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 跨局持久进度（PlayerPrefs 占位，后续可换存档系统）。
    /// </summary>
    public static class GameFlowProgress
    {
        const string PrologueCompletedKey = "NineGrid.GameFlow.PrologueCompleted";

        public static bool HasCompletedPrologue
        {
            get => PlayerPrefs.GetInt(PrologueCompletedKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(PrologueCompletedKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
