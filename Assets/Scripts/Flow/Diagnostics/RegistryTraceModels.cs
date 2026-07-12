using System;
using System.Collections.Generic;

namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// CardManager 注册表专项诊断会话：Release / Audit / 生命周期快照。
    /// 独立于 PerfLog，便于「缺卡」类 bug 单线分析。
    /// </summary>
    [Serializable]
    public sealed class RegistryTraceSession
    {
        public int schemaVersion = 1;
        public string seed = "0";
        public string sessionId = string.Empty;
        public List<RegistryTraceEvent> events = new List<RegistryTraceEvent>();
    }

    [Serializable]
    public sealed class RegistryTraceEvent
    {
        public int index;
        public int beatId;
        public int tMs;
        public string kind = string.Empty;
        public int uid = -1;
        public string site = string.Empty;
        public Dictionary<string, string> payload = new Dictionary<string, string>();
    }

    public static class RegistryTraceKinds
    {
        public const string RegistryDelta = "RegistryDelta";
        public const string RegistryAudit = "RegistryAudit";
        public const string RegistryMiss = "RegistryMiss";
        public const string Despawn = "Despawn";
        public const string Vacate = "Vacate";
        public const string BoardSnap = "BoardSnap";
        public const string Checkpoint = "Checkpoint";
        public const string Anomaly = "Anomaly";
    }

    public static class RegistryTraceTriggers
    {
        public const string OpeningSettled = "Opening.Settled";
        public const string InteractionLoopIdle = "InteractionLoop.Idle";
        public const string IdleWatchPrefix = "IdleWatch.";
        public const string AuditMismatchPrefix = "AuditMismatch.";
        public const string UserMarkPrefix = "UserMark.";
    }
}
