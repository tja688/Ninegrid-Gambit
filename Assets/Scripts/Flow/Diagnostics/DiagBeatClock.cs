using System;
using UnityEngine;

namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// 诊断共通 Beat：CoreLog / PerfLog / Domain Probe 用同一 beatId 对齐节奏。
    /// </summary>
    public static class DiagBeatClock
    {
        private static int sNextBeatId = 1;
        private static int sCurrentBeatId;
        private static int sLastBeatId;
        private static string sBeatKind = string.Empty;
        private static int sNodeIndex;
        private static bool sOpen;

        public static bool IsOpen => sOpen;

        /// <summary>当前打开的 Beat；未打开时为 0。</summary>
        public static int CurrentBeatId => sOpen ? sCurrentBeatId : 0;

        /// <summary>最近一次 Open 的 Beat（Close 后仍可读，便于收尾事件）。</summary>
        public static int LastBeatId => sLastBeatId;

        public static string CurrentBeatKind => sBeatKind ?? string.Empty;

        public static int CurrentNodeIndex => sNodeIndex;

        /// <summary>写入事件时优先用当前 Beat；已关闭则回落 LastBeatId。</summary>
        public static int ResolveBeatIdForEvent()
        {
            if (sOpen && sCurrentBeatId > 0)
            {
                return sCurrentBeatId;
            }

            return sLastBeatId;
        }

        public static void Reset()
        {
            sNextBeatId = 1;
            sCurrentBeatId = 0;
            sLastBeatId = 0;
            sBeatKind = string.Empty;
            sNodeIndex = 0;
            sOpen = false;
        }

        /// <summary>
        /// 打开新 Beat。若已有打开中的 Beat，先 Close 再 Open。
        /// </summary>
        public static int Open(string beatKind, int nodeIndex = 0)
        {
            try
            {
                if (sOpen)
                {
                    Close();
                }

                sCurrentBeatId = sNextBeatId++;
                sLastBeatId = sCurrentBeatId;
                sBeatKind = beatKind ?? string.Empty;
                sNodeIndex = nodeIndex;
                sOpen = true;
                return sCurrentBeatId;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DiagBeat] Open failed: " + ex.Message);
                return 0;
            }
        }

        public static void Close()
        {
            try
            {
                if (!sOpen)
                {
                    return;
                }

                sOpen = false;
                sBeatKind = string.Empty;
                sNodeIndex = 0;
                sCurrentBeatId = 0;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DiagBeat] Close failed: " + ex.Message);
            }
        }
    }

    /// <summary>稳定 beatKind，与 batchTag 场景对齐。</summary>
    public static class DiagBeatKinds
    {
        public const string OpeningDeal = "OpeningDeal";
        public const string PostKillDrain = "PostKillDrain";
        public const string SyncBoard = "SyncBoard";
        public const string StartNode = "StartNode";
        public const string CombatHit = "CombatHit";
        public const string Reward = "Reward";
        public const string Room = "Room";
    }
}
