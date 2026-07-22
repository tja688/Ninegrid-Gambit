using System.Globalization;
using UnityEngine;

namespace NineGrid.Cards.Convergence
{
    /// <summary>
    /// 收敛范式诊断探针：纪律 B / 租约 / 栅栏 / 就位承诺 / beat 对齐 / 交接。
    /// Perf → <see cref="CardPresentationProbe"/>；Core → <see cref="FlowFieldTraceSink"/>。失败一律吞掉。
    /// </summary>
    public static class ConvergenceDiagProbe
    {
        public static void DisciplineB(
            int uid,
            string code,
            string reason,
            string layer = null,
            string verdict = null)
        {
            try
            {
                var safeCode = string.IsNullOrEmpty(code) ? "DisciplineB" : code;
                ChoreoTraceSink.SafeEmitAnomaly(safeCode, uid, reason ?? string.Empty);
                CardPresentationProbe.Anomaly(
                    uid,
                    safeCode,
                    reason,
                    site: "Lease.Arbiter",
                    layer: layer,
                    verdict: verdict);
                FlowFieldTraceSink.DisciplineBAlarm?.Invoke(
                    uid,
                    safeCode,
                    reason ?? string.Empty,
                    layer ?? string.Empty,
                    verdict ?? string.Empty);
            }
            catch
            {
                // swallow
            }
        }

        public static void LeaseAcquire(
            int uid,
            string layer,
            string verdict,
            string commitment,
            int leaseId,
            float windowStart,
            float windowEnd,
            bool disciplineB = false,
            bool commandeered = false)
        {
            try
            {
                CardPresentationProbe.LeaseAcquire(
                    uid,
                    layer,
                    verdict,
                    commitment,
                    leaseId,
                    windowStart,
                    windowEnd,
                    disciplineB,
                    commandeered);
                FlowFieldTraceSink.LeaseAcquire?.Invoke(
                    uid,
                    layer ?? string.Empty,
                    verdict ?? string.Empty,
                    commitment ?? string.Empty,
                    leaseId,
                    windowStart,
                    windowEnd,
                    disciplineB,
                    commandeered);
            }
            catch
            {
                // swallow
            }
        }

        public static void LeaseRelease(int uid, string layer, int leaseId, string reason = null)
        {
            try
            {
                CardPresentationProbe.LeaseRelease(uid, layer, leaseId, reason);
                FlowFieldTraceSink.LeaseRelease?.Invoke(
                    uid,
                    layer ?? string.Empty,
                    leaseId,
                    reason ?? string.Empty);
            }
            catch
            {
                // swallow
            }
        }

        public static void CommitmentArrive(
            int uid,
            string layer,
            string commitment,
            int leaseId = 0,
            string site = null)
        {
            try
            {
                CardPresentationProbe.CommitmentArrive(uid, layer, commitment, leaseId, site);
                FlowFieldTraceSink.CommitmentArrive?.Invoke(
                    uid,
                    layer ?? string.Empty,
                    commitment ?? string.Empty,
                    leaseId,
                    site ?? string.Empty);
            }
            catch
            {
                // swallow
            }
        }

        public static void BarrierPlace(
            int presBeatId,
            float barrierWallTime,
            float sharedSourceTime,
            float startWallTime,
            int registrationHint = 0)
        {
            try
            {
                CardPresentationProbe.BarrierPlace(
                    presBeatId,
                    barrierWallTime,
                    sharedSourceTime,
                    startWallTime,
                    registrationHint);
                FlowFieldTraceSink.BarrierPlace?.Invoke(
                    presBeatId,
                    barrierWallTime,
                    sharedSourceTime,
                    startWallTime,
                    registrationHint);
            }
            catch
            {
                // swallow
            }
        }

        public static void BarrierSatisfied(
            int presBeatId,
            bool satisfied,
            int registrationCount,
            float nowWallTime)
        {
            try
            {
                CardPresentationProbe.BarrierSatisfied(
                    presBeatId,
                    satisfied,
                    registrationCount,
                    nowWallTime);
                FlowFieldTraceSink.BarrierSatisfied?.Invoke(
                    presBeatId,
                    satisfied,
                    registrationCount,
                    nowWallTime);
            }
            catch
            {
                // swallow
            }
        }

        public static void BeatAlign(int presBeatId, float sharedSourceTime, float startWallTime)
        {
            try
            {
                CardPresentationProbe.BeatAlign(presBeatId, sharedSourceTime, startWallTime);
                FlowFieldTraceSink.BeatAlign?.Invoke(presBeatId, sharedSourceTime, startWallTime);
            }
            catch
            {
                // swallow
            }
        }

        public static void Handoff(
            int uid,
            string layer,
            string phase,
            Vector3 localPosition,
            Vector3 localVelocity,
            string site = null)
        {
            try
            {
                CardPresentationProbe.Handoff(
                    uid,
                    layer,
                    phase,
                    localPosition,
                    localVelocity,
                    site);
                FlowFieldTraceSink.Handoff?.Invoke(
                    uid,
                    layer ?? string.Empty,
                    phase ?? string.Empty,
                    FormatVel(localVelocity.x),
                    FormatVel(localVelocity.y),
                    site ?? string.Empty);
            }
            catch
            {
                // swallow
            }
        }

        private static string FormatVel(float v) =>
            v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
