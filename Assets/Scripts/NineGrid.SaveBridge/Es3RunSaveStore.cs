using NineGrid.Flow;
using UnityEngine;

namespace NineGrid.SaveBridge
{
    /// <summary>
    /// 跑图存档的 Easy Save 3 落盘后端。
    /// 本文件所在目录**没有 asmdef**、编入 Assembly-CSharp：ES3 插件同样无 asmdef，
    /// 工程程序集（NineGrid.Presentation）无法直接引用 ES3 API，故由本桥在启动时
    /// 经 <see cref="RunSaveStoreHook"/> 注册。存储布局：persistentDataPath 下
    /// <c>NineGridSaves/slot_{id}.es3</c>，单键 <c>snapshot</c> 存快照 JSON。
    /// </summary>
    public sealed class Es3RunSaveStore : IRunSaveStore
    {
        private const string SnapshotKey = "snapshot";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            RunSaveStoreHook.Set(new Es3RunSaveStore());
        }

        public bool Exists(string slotId)
        {
            return ES3.FileExists(PathFor(slotId));
        }

        public bool TryRead(string slotId, out string json)
        {
            json = null;
            var path = PathFor(slotId);
            if (!ES3.FileExists(path) || !ES3.KeyExists(SnapshotKey, path))
            {
                return false;
            }

            json = ES3.Load<string>(SnapshotKey, path);
            return !string.IsNullOrEmpty(json);
        }

        public void Write(string slotId, string json)
        {
            ES3.Save<string>(SnapshotKey, json, PathFor(slotId));
        }

        public void Delete(string slotId)
        {
            var path = PathFor(slotId);
            if (ES3.FileExists(path))
            {
                ES3.DeleteFile(path);
            }
        }

        private static string PathFor(string slotId)
        {
            return "NineGridSaves/slot_" + Sanitize(slotId) + ".es3";
        }

        private static string Sanitize(string slotId)
        {
            if (string.IsNullOrEmpty(slotId))
            {
                return "unknown";
            }

            var chars = slotId.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                var ok = (c >= 'a' && c <= 'z')
                    || (c >= 'A' && c <= 'Z')
                    || (c >= '0' && c <= '9')
                    || c == '_'
                    || c == '-';
                if (!ok)
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }
    }
}
