using System;
using System.Collections.Generic;
using NineGrid.Core.Systems;

namespace NineGrid.Core.Effects
{
    /// <summary>
    /// ADR-0010 / #73：未触发探查失败类别。requires 失败属装配/适用错误；conditions 失败属正常玩法。
    /// 与 ADR-0003 正向 ChainId 管线并存，不改写已触发路径。
    /// </summary>
    public enum EffectNonTriggerFailureKind
    {
        None = 0,
        Requires = 1,
        Conditions = 2,
        TriggerMismatch = 3,
        Other = 4
    }

    public readonly struct EffectNonTriggerProbeResult
    {
        public EffectNonTriggerProbeResult(
            EffectNonTriggerFailureKind kind,
            string code,
            string message)
        {
            Kind = kind;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public EffectNonTriggerFailureKind Kind { get; }
        public string Code { get; }
        public string Message { get; }

        public bool WouldHaveTriggered
        {
            get { return Kind == EffectNonTriggerFailureKind.None; }
        }

        public static EffectNonTriggerProbeResult WouldTrigger()
        {
            return new EffectNonTriggerProbeResult(
                EffectNonTriggerFailureKind.None,
                "probe.would-trigger",
                "Effect would trigger under the given context.");
        }

        public static EffectNonTriggerProbeResult Failed(
            EffectNonTriggerFailureKind kind,
            string code,
            string message)
        {
            return new EffectNonTriggerProbeResult(kind, code, message);
        }
    }

    /// <summary>
    /// 对单条已激活效果 + 触发上下文做只读分诊（尽量不留下 Matches 副作用）。
    /// </summary>
    public static class EffectNonTriggerProbe
    {
        public static EffectNonTriggerProbeResult Evaluate(
            EffectInstance instance,
            EffectRuntimeContext runtime)
        {
            if (instance == null || runtime == null)
            {
                return EffectNonTriggerProbeResult.Failed(
                    EffectNonTriggerFailureKind.Other,
                    "probe.invalid-args",
                    "Instance or runtime is null.");
            }

            if (instance.Trigger == null || instance.Action == null)
            {
                return EffectNonTriggerProbeResult.Failed(
                    EffectNonTriggerFailureKind.Other,
                    "effect.incomplete",
                    "Effect is missing trigger or action.");
            }

            string failedRequire;
            if (!EffectRequiresRuntime.TryExplainFailure(instance, runtime, out failedRequire))
            {
                return EffectNonTriggerProbeResult.Failed(
                    EffectNonTriggerFailureKind.Requires,
                    "requires.failed",
                    "Requires token '" + failedRequire + "' is not satisfied.");
            }

            var counterSnapshot = SnapshotOwnerCounters(runtime);
            try
            {
                if (!instance.Trigger.Matches(runtime))
                {
                    return EffectNonTriggerProbeResult.Failed(
                        EffectNonTriggerFailureKind.TriggerMismatch,
                        "trigger.mismatch",
                        "Trigger.Matches returned false.");
                }
            }
            finally
            {
                RestoreOwnerCounters(runtime, counterSnapshot);
            }

            for (var i = 0; i < instance.Conditions.Count; i++)
            {
                if (!instance.Conditions[i].IsMet(runtime))
                {
                    return EffectNonTriggerProbeResult.Failed(
                        EffectNonTriggerFailureKind.Conditions,
                        "conditions.failed",
                        "Condition at index " + i + " is not met.");
                }
            }

            if (!FragmentRecombineDedup.Passes(instance, runtime))
            {
                return EffectNonTriggerProbeResult.Failed(
                    EffectNonTriggerFailureKind.Other,
                    "dedup.fragment-recombine",
                    "Fragment recombine dedup blocked the effect.");
            }

            return EffectNonTriggerProbeResult.WouldTrigger();
        }

        private static Dictionary<string, int> SnapshotOwnerCounters(EffectRuntimeContext runtime)
        {
            var owner = runtime.OwnerCard ?? runtime.AvatarCard;
            if (owner == null)
            {
                return null;
            }

            var snapshot = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var pair in owner.Counters.Values)
            {
                snapshot[pair.Key] = pair.Value;
            }

            return snapshot;
        }

        private static void RestoreOwnerCounters(
            EffectRuntimeContext runtime,
            Dictionary<string, int> snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            var owner = runtime.OwnerCard ?? runtime.AvatarCard;
            if (owner == null)
            {
                return;
            }

            owner.Counters.Clear();
            foreach (var pair in snapshot)
            {
                owner.Counters.Set(pair.Key, pair.Value);
            }
        }
    }
}
