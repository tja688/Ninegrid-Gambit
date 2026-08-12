using System;
using UnityEngine;

namespace NineGrid.Flow.Tutorial
{
    /// <summary>
    /// 教学完成标记：经既有存档后端（<see cref="RunSaveStoreHook"/> → ES3 桥）持久化，
    /// 独立槽位，不与跑图检查点混用。后端缺失时按「未完成」处理且不落盘。
    /// </summary>
    public static class TutorialProgressStore
    {
        private const string SlotId = "tutorial_profile";

        [Serializable]
        private sealed class TutorialProfile
        {
            public int schemaVersion = 1;
            public bool completed;
        }

        public static bool IsCompleted()
        {
            var store = RunSaveStoreHook.StoreOrNull();
            if (store == null || !store.TryRead(SlotId, out var json) || string.IsNullOrEmpty(json))
            {
                return false;
            }

            try
            {
                var profile = JsonUtility.FromJson<TutorialProfile>(json);
                return profile != null && profile.completed;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Tutorial] 教学进度读取失败，按未完成处理：" + ex.Message);
                return false;
            }
        }

        public static void MarkCompleted()
        {
            var store = RunSaveStoreHook.StoreOrNull();
            if (store == null)
            {
                Debug.LogWarning("[Tutorial] 存档后端缺失，教学完成标记未落盘。");
                return;
            }

            try
            {
                store.Write(SlotId, JsonUtility.ToJson(new TutorialProfile { completed = true }));
                Debug.Log("[Tutorial] 教学完成标记已写入存档。");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Tutorial] 教学完成标记写入失败：" + ex.Message);
            }
        }

        /// <summary>Dev 复位：删除教学完成标记（下次「开始游戏」重新进教学）。</summary>
        public static void ResetCompleted()
        {
            var store = RunSaveStoreHook.StoreOrNull();
            if (store == null)
            {
                return;
            }

            try
            {
                store.Delete(SlotId);
                Debug.Log("[Tutorial] 教学完成标记已清除。");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Tutorial] 教学完成标记清除失败：" + ex.Message);
            }
        }
    }
}
