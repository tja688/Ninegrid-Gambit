using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 跑图存档落盘后端（按槽位读写原始 JSON）。
    /// 生产实现是 Easy Save 3 桥（Assembly-CSharp，见 <c>Assets/Scripts/NineGrid.SaveBridge/</c>）；
    /// ES3 插件无 asmdef、工程程序集无法直接引用，故经 <see cref="RunSaveStoreHook"/> 装配缝注册。
    /// </summary>
    public interface IRunSaveStore
    {
        bool Exists(string slotId);

        bool TryRead(string slotId, out string json);

        void Write(string slotId, string json);

        void Delete(string slotId);
    }

    /// <summary>
    /// 装配缝（对齐仓库既有静态 Hook 范式）：桥程序集在 RuntimeInitializeOnLoad 时注册后端。
    /// 不是业务 Sink——只承载「谁来落盘」的装配，不读写规则状态。
    /// </summary>
    public static class RunSaveStoreHook
    {
        private static IRunSaveStore sStore;
        private static bool sMissingWarned;

        public static void Set(IRunSaveStore store)
        {
            sStore = store;
            if (store != null)
            {
                sMissingWarned = false;
            }
        }

        public static void SuppressMissingWarningForTests()
        {
            sMissingWarned = true;
        }

        public static IRunSaveStore StoreOrNull()
        {
            if (sStore == null && !sMissingWarned)
            {
                sMissingWarned = true;
                Debug.LogWarning(
                    "[RunSave] 存档后端未注册：Easy Save 3 桥（NineGrid.SaveBridge）缺失，存读档不可用。");
            }

            return sStore;
        }
    }
}
