using System;
using UnityEngine;

namespace NineGrid.Flow.Tutorial
{
    /// <summary>
    /// 教学档案与进度槽（三路标记）：
    /// 1. 步骤 1–9 完成（<see cref="IsSteps1To9Completed"/> / <see cref="MarkSteps1To9Completed"/>）
    /// 2. 门教学已见（<see cref="IsDoorTutorialSeen"/> / <see cref="MarkDoorTutorialSeen"/>）
    /// 3. 血色难度已解锁（<see cref="IsScarletUnlocked"/> / <see cref="MarkScarletUnlocked"/>）
    ///
    /// 经既有存档后端（<see cref="RunSaveStoreHook"/> → ES3 桥）持久化于独立槽位 <c>tutorial_profile</c>，
    /// 绝不与跑图检查点槽混用。后端缺失时所有状态按「未完成/未解锁」处理且不落盘。
    /// </summary>
    public static class TutorialProgressStore
    {
        public const string SlotId = "tutorial_profile";

        [Serializable]
        public sealed class TutorialProfile
        {
            public int schemaVersion = 1;
            public bool completed;
            public bool steps1To9Completed;
            public bool doorTutorialSeen;
            public bool scarletUnlocked;
        }

        private static TutorialProfile ReadProfileOrNull()
        {
            var store = RunSaveStoreHook.StoreOrNull();
            if (store == null || !store.TryRead(SlotId, out var json) || string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<TutorialProfile>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Tutorial] 教学进度读取失败，按未完成处理：" + ex.Message);
                return null;
            }
        }

        private static void MutateAndSave(Action<TutorialProfile> mutate, string logSuccess)
        {
            var store = RunSaveStoreHook.StoreOrNull();
            if (store == null)
            {
                Debug.LogWarning("[Tutorial] 存档后端缺失，教学进度标记未落盘。");
                return;
            }

            try
            {
                var profile = ReadProfileOrNull() ?? new TutorialProfile();
                mutate(profile);
                store.Write(SlotId, JsonUtility.ToJson(profile));
                if (!string.IsNullOrEmpty(logSuccess))
                {
                    Debug.Log("[Tutorial] " + logSuccess);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[Tutorial] 教学进度标记写入失败：" + ex.Message);
            }
        }

        /// <summary>步骤 1–9 是否已完成（第 9 步教练播完或自然第一关战败时标记）。</summary>
        public static bool IsSteps1To9Completed()
        {
            var profile = ReadProfileOrNull();
            return profile != null && (profile.completed || profile.steps1To9Completed);
        }

        /// <summary>标记步骤 1–9 已完成。</summary>
        public static void MarkSteps1To9Completed()
        {
            MutateAndSave(p =>
            {
                p.steps1To9Completed = true;
                p.completed = true;
            }, "步骤 1–9 完成标记已写入存档。");
        }

        /// <summary>兼容旧 API：等同于 <see cref="IsSteps1To9Completed"/>。</summary>
        public static bool IsCompleted() => IsSteps1To9Completed();

        /// <summary>兼容旧 API：等同于 <see cref="MarkSteps1To9Completed"/>。</summary>
        public static void MarkCompleted() => MarkSteps1To9Completed();

        /// <summary>门教学是否已见（自然对局初次门就位并提示后标记）。</summary>
        public static bool IsDoorTutorialSeen()
        {
            var profile = ReadProfileOrNull();
            return profile != null && profile.doorTutorialSeen;
        }

        /// <summary>标记门教学已见。</summary>
        public static void MarkDoorTutorialSeen()
        {
            MutateAndSave(p => p.doorTutorialSeen = true, "门教学已见标记已写入存档。");
        }

        /// <summary>血色难度是否已解锁（整趟跑图通关胜利后标记）。</summary>
        public static bool IsScarletUnlocked()
        {
            var profile = ReadProfileOrNull();
            return profile != null && profile.scarletUnlocked;
        }

        /// <summary>标记血色难度已解锁。</summary>
        public static void MarkScarletUnlocked()
        {
            MutateAndSave(p => p.scarletUnlocked = true, "血色难度解锁标记已写入存档。");
        }

        /// <summary>清空所有教学档案进度（Dev/Release 构建清理）。</summary>
        public static void ResetAll()
        {
            var store = RunSaveStoreHook.StoreOrNull();
            if (store == null)
            {
                return;
            }

            try
            {
                store.Delete(SlotId);
                Debug.Log("[Tutorial] 教学档案进度标记已全部清除。");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Tutorial] 教学进度标记清除失败：" + ex.Message);
            }
        }

        /// <summary>兼容旧 API：等同于 <see cref="ResetAll"/>。</summary>
        public static void ResetCompleted() => ResetAll();
    }
}
