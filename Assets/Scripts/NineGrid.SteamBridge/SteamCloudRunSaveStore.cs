#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

#if !DISABLESTEAMWORKS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NineGrid.Flow;
using Steamworks;
using UnityEngine;

namespace NineGrid.SteamBridge
{
    /// <summary>
    /// 跑图存档的 Steam Cloud 镜像装饰器（ISteamRemoteStorage）：
    /// 包住既有本地后端（ES3 桥，ADR-0041），写入双写（本地 + 云）、读取本地优先、
    /// 云端兜底回填；启动时做一次双向同步，冲突按「文件时间戳新者胜」。
    /// 账号 / App 云功能被关闭时纯透传本地，行为与无云完全一致。
    /// 云文件名：<c>saves/slot_{id}.json</c>（存原始快照 JSON，与 ES3 格式解耦）。
    /// </summary>
    internal sealed class SteamCloudRunSaveStore : IRunSaveStore
    {
        private const string CloudPrefix = "saves/slot_";
        private const string CloudSuffix = ".json";

        /// <summary>本地 ES3 落盘布局（ADR-0041 固定约定），仅用于取文件时间戳比较新旧。</summary>
        private const string LocalFolder = "NineGridSaves";

        /// <summary>时间戳容差（秒）：差距以内视为同版本，不同步。</summary>
        private const long TimestampToleranceSeconds = 2;

        private readonly IRunSaveStore mInner;

        public SteamCloudRunSaveStore(IRunSaveStore inner)
        {
            mInner = inner;
        }

        private static bool CloudAvailable =>
            SteamRemoteStorage.IsCloudEnabledForAccount() && SteamRemoteStorage.IsCloudEnabledForApp();

        public bool Exists(string slotId)
        {
            if (mInner.Exists(slotId))
            {
                return true;
            }

            return CloudAvailable && SteamRemoteStorage.FileExists(CloudNameFor(slotId));
        }

        public bool TryRead(string slotId, out string json)
        {
            if (mInner.TryRead(slotId, out json))
            {
                return true;
            }

            if (!CloudAvailable || !TryReadCloud(CloudNameFor(slotId), out json))
            {
                return false;
            }

            // 云端有、本地没有：回填本地，后续读写回到本地快路径。
            mInner.Write(slotId, json);
            Debug.Log("[Steam] 云存档回填本地：" + slotId);
            return true;
        }

        public void Write(string slotId, string json)
        {
            mInner.Write(slotId, json);

            if (!CloudAvailable)
            {
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(json);
            if (!SteamRemoteStorage.FileWrite(CloudNameFor(slotId), bytes, bytes.Length))
            {
                Debug.LogWarning("[Steam] 云存档写入失败（配额不足或云服务异常）：" + slotId + "，本地存档不受影响。");
            }
        }

        public void Delete(string slotId)
        {
            mInner.Delete(slotId);

            if (CloudAvailable && SteamRemoteStorage.FileExists(CloudNameFor(slotId)))
            {
                SteamRemoteStorage.FileDelete(CloudNameFor(slotId));
            }
        }

        /// <summary>
        /// 启动时双向同步：对本地 / 云端槽位取并集，仅一侧有则补齐另一侧，
        /// 两侧都有则时间戳新者胜（容差内不动）。任何异常只降级为本地模式，不阻断启动。
        /// </summary>
        public void SyncOnBoot()
        {
            if (!CloudAvailable)
            {
                Debug.Log("[Steam] Steam Cloud 未启用（账号或 App 设置），存档仅本地。");
                return;
            }

            try
            {
                var cloudSlots = ListCloudSlots();
                var localSlots = ListLocalSlots();

                var all = new HashSet<string>(cloudSlots.Keys);
                all.UnionWith(localSlots.Keys);

                foreach (var slotId in all)
                {
                    var hasCloud = cloudSlots.TryGetValue(slotId, out var cloudTime);
                    var hasLocal = localSlots.TryGetValue(slotId, out var localTime);

                    if (hasCloud && (!hasLocal || cloudTime > localTime + TimestampToleranceSeconds))
                    {
                        if (TryReadCloud(CloudNameFor(slotId), out var json))
                        {
                            mInner.Write(slotId, json);
                            Debug.Log("[Steam] 云端较新，拉取存档：" + slotId);
                        }
                    }
                    else if (hasLocal && (!hasCloud || localTime > cloudTime + TimestampToleranceSeconds))
                    {
                        if (mInner.TryRead(slotId, out var json) && !string.IsNullOrEmpty(json))
                        {
                            var bytes = Encoding.UTF8.GetBytes(json);
                            SteamRemoteStorage.FileWrite(CloudNameFor(slotId), bytes, bytes.Length);
                            Debug.Log("[Steam] 本地较新，上传存档：" + slotId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Steam] 云存档启动同步异常（已跳过，存档仅本地）：" + ex.Message);
            }
        }

        private static bool TryReadCloud(string cloudName, out string json)
        {
            json = null;
            if (!SteamRemoteStorage.FileExists(cloudName))
            {
                return false;
            }

            var size = SteamRemoteStorage.GetFileSize(cloudName);
            if (size <= 0)
            {
                return false;
            }

            var buffer = new byte[size];
            var read = SteamRemoteStorage.FileRead(cloudName, buffer, size);
            if (read != size)
            {
                Debug.LogWarning("[Steam] 云存档读取不完整：" + cloudName + " (" + read + "/" + size + ")");
                return false;
            }

            json = Encoding.UTF8.GetString(buffer);
            return !string.IsNullOrEmpty(json);
        }

        /// <summary>云端槽位 → 时间戳（unix 秒）。</summary>
        private static Dictionary<string, long> ListCloudSlots()
        {
            var result = new Dictionary<string, long>();
            var count = SteamRemoteStorage.GetFileCount();
            for (var i = 0; i < count; i++)
            {
                var name = SteamRemoteStorage.GetFileNameAndSize(i, out _);
                if (string.IsNullOrEmpty(name)
                    || !name.StartsWith(CloudPrefix, StringComparison.Ordinal)
                    || !name.EndsWith(CloudSuffix, StringComparison.Ordinal))
                {
                    continue;
                }

                var slotId = name.Substring(
                    CloudPrefix.Length, name.Length - CloudPrefix.Length - CloudSuffix.Length);
                if (slotId.Length > 0)
                {
                    result[slotId] = SteamRemoteStorage.GetFileTimestamp(name);
                }
            }

            return result;
        }

        /// <summary>本地槽位 → 文件最后写入时间（unix 秒）。按 ADR-0041 的 ES3 落盘布局扫描。</summary>
        private static Dictionary<string, long> ListLocalSlots()
        {
            var result = new Dictionary<string, long>();
            var folder = Path.Combine(Application.persistentDataPath, LocalFolder);
            if (!Directory.Exists(folder))
            {
                return result;
            }

            foreach (var file in Directory.GetFiles(folder, "slot_*.es3"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var slotId = name.Substring("slot_".Length);
                if (slotId.Length > 0)
                {
                    result[slotId] = new DateTimeOffset(File.GetLastWriteTimeUtc(file)).ToUnixTimeSeconds();
                }
            }

            return result;
        }

        private static string CloudNameFor(string slotId)
        {
            return CloudPrefix + Sanitize(slotId) + CloudSuffix;
        }

        /// <summary>与 ES3 桥相同的净化规则，保证云文件名与本地文件名按同一 slotId 对齐。</summary>
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
#endif
