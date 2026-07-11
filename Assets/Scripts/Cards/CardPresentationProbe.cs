using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Cards
{
    /// <summary>
    /// 表现层诊断探针：在 Cards 喉点调用，经 <see cref="PerfTraceSink"/> 旁路到 Flow PerfLog。
    /// 失败一律吞掉。
    /// </summary>
    public static class CardPresentationProbe
    {
        private static int sNextMotionId = 1;

        public static int NextMotionId()
        {
            return sNextMotionId++;
        }

        public static void ResetMotionIds()
        {
            sNextMotionId = 1;
        }

        public static void SnapSet(
            int uid,
            Vector3 world,
            string site,
            int? slot = null,
            bool killedTween = false,
            string reason = null)
        {
            Emit(
                "SnapSet",
                uid,
                site,
                "x", FormatXy(world.x),
                "y", FormatXy(world.y),
                "slot", slot.HasValue ? slot.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                "killedTween", killedTween ? "1" : "0",
                "reason", reason ?? string.Empty);
        }

        public static void MotionBegin(
            int uid,
            int motionId,
            Vector3 from,
            Vector3 to,
            string site,
            string reason = null,
            float expectMs = 0f)
        {
            Emit(
                "MotionBegin",
                uid,
                site,
                "motionId", motionId.ToString(CultureInfo.InvariantCulture),
                "fromX", FormatXy(from.x),
                "fromY", FormatXy(from.y),
                "toX", FormatXy(to.x),
                "toY", FormatXy(to.y),
                "reason", reason ?? string.Empty,
                "expectMs", ((int)(expectMs * 1000f)).ToString(CultureInfo.InvariantCulture));
        }

        public static void MotionEnd(
            int uid,
            int motionId,
            Vector3 at,
            string endHow,
            string site,
            string killedBySite = null)
        {
            Emit(
                "MotionEnd",
                uid,
                site,
                "motionId", motionId.ToString(CultureInfo.InvariantCulture),
                "endHow", endHow ?? string.Empty,
                "x", FormatXy(at.x),
                "y", FormatXy(at.y),
                "killedBySite", killedBySite ?? string.Empty);
        }

        public static void MotionPlan(
            int uid,
            int fromSlot,
            int toSlot,
            Vector3 from,
            Vector3 to,
            string site,
            string reason = null,
            float expectMs = 0f)
        {
            Emit(
                "MotionPlan",
                uid,
                site,
                "fromSlot", fromSlot.ToString(CultureInfo.InvariantCulture),
                "toSlot", toSlot.ToString(CultureInfo.InvariantCulture),
                "fromX", FormatXy(from.x),
                "fromY", FormatXy(from.y),
                "toX", FormatXy(to.x),
                "toY", FormatXy(to.y),
                "reason", reason ?? string.Empty,
                "expectMs", ((int)(expectMs * 1000f)).ToString(CultureInfo.InvariantCulture));
        }

        public static void VisChange(
            int uid,
            string site,
            bool? active = null,
            float? alpha = null,
            string mode = null,
            bool? renderOn = null,
            int? sortOrder = null)
        {
            Emit(
                "VisChange",
                uid,
                site,
                "active", active.HasValue ? (active.Value ? "1" : "0") : string.Empty,
                "alpha", alpha.HasValue ? alpha.Value.ToString("0.###", CultureInfo.InvariantCulture) : string.Empty,
                "mode", mode ?? string.Empty,
                "renderOn", renderOn.HasValue ? (renderOn.Value ? "1" : "0") : string.Empty,
                "sortOrder", sortOrder.HasValue ? sortOrder.Value.ToString(CultureInfo.InvariantCulture) : string.Empty);
        }

        /// <summary>Timeline 命中帧采样：world 坐标 + SortingGroup + renderer 状态。</summary>
        public static void CombatHitFrame(int uid, string site, Transform transform)
        {
            if (transform == null)
            {
                return;
            }

            var pos = transform.position;
            Emit(
                "CombatHitFrame",
                uid,
                site,
                "x", FormatXy(pos.x),
                "y", FormatXy(pos.y),
                "sortOrder", ReadSortingOrder(transform).ToString(CultureInfo.InvariantCulture),
                "renderOn", HasEnabledRenderer(transform.gameObject) ? "1" : "0");
        }

        public static int ReadSortingOrder(Transform transform)
        {
            if (transform == null)
            {
                return 0;
            }

            var sortingGroup = transform.GetComponent<SortingGroup>();
            return sortingGroup != null ? sortingGroup.sortingOrder : 0;
        }

        public static bool HasEnabledRenderer(GameObject go)
        {
            if (go == null)
            {
                return false;
            }

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].enabled)
                {
                    return true;
                }
            }

            return false;
        }

        public static void Spawn(int uid, string defId, string site, int? slot = null, string parent = null)
        {
            Emit(
                "Spawn",
                uid,
                site,
                "defId", defId ?? string.Empty,
                "slot", slot.HasValue ? slot.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                "parent", parent ?? string.Empty);
        }

        public static void Despawn(int uid, string site, string reason = null)
        {
            Emit(
                "Despawn",
                uid,
                site,
                "reason", reason ?? string.Empty);
        }

        /// <summary>占格表有 uid 但 CardManager 查无视图（幽灵占格）。</summary>
        public static void RegistryMiss(int uid, string site, string detail = null)
        {
            Emit(
                "RegistryMiss",
                uid,
                site,
                "detail", detail ?? string.Empty);
        }

        /// <summary>场地占格注销：仅清 _uidBySlot/_slotByUid，不一定 Release 视图。</summary>
        public static void Vacate(int uid, int slot, string site, string caller = null)
        {
            Emit(
                "Vacate",
                uid,
                site,
                "slot", slot.ToString(CultureInfo.InvariantCulture),
                "caller", caller ?? string.Empty);
        }

        /// <summary>外圈旋转：整环 Vacate → 重登记计划摘要。</summary>
        public static void RingShift(string phase, string plan, string site)
        {
            Emit(
                "RingShift",
                -1,
                site,
                "phase", phase ?? string.Empty,
                "plan", plan ?? string.Empty);
        }

        /// <summary>
        /// 注册表完整性审计：对比 CardManager 与场地占格。
        /// ghosts=占格有 uid 无视图；orphans=有视图无占格（GroundCardMode）。
        /// </summary>
        public static void RegistryAudit(
            string site,
            int registryCount,
            int fieldOccupantCount,
            string ghosts,
            string orphans,
            string trigger = null)
        {
            Emit(
                "RegistryAudit",
                -1,
                site,
                "registryCount", registryCount.ToString(CultureInfo.InvariantCulture),
                "fieldCount", fieldOccupantCount.ToString(CultureInfo.InvariantCulture),
                "ghosts", ghosts ?? string.Empty,
                "orphans", orphans ?? string.Empty,
                "trigger", trigger ?? string.Empty);
        }

        /// <summary>战斗编排路由：Intent / Profile / 死亡回调开关 / 预估击杀。</summary>
        public static void BattleBindResolve(
            int attackerUid,
            int victimUid,
            string site,
            string intent,
            string profileId,
            bool bindDeathCallback,
            bool estimatedWillKill,
            bool isCounter)
        {
            Emit(
                "BattleBind",
                attackerUid,
                site,
                "victimUid", victimUid.ToString(CultureInfo.InvariantCulture),
                "intent", intent ?? string.Empty,
                "profileId", profileId ?? string.Empty,
                "bindDeath", bindDeathCallback ? "1" : "0",
                "estKill", estimatedWillKill ? "1" : "0",
                "counter", isCounter ? "1" : "0");
        }

        /// <summary>Timeline 死亡回调：armed=绑定了死亡链；fired/skipped 见 outcome。</summary>
        public static void DeathCallback(
            int victimUid,
            string site,
            string outcome,
            bool armed,
            bool confirmedKill,
            int? attackerUid = null)
        {
            Emit(
                "DeathCallback",
                victimUid,
                site,
                "outcome", outcome ?? string.Empty,
                "armed", armed ? "1" : "0",
                "confirmedKill", confirmedKill ? "1" : "0",
                "attackerUid", attackerUid.HasValue
                    ? attackerUid.Value.ToString(CultureInfo.InvariantCulture)
                    : string.Empty);
        }

        public static void ParentChange(int uid, string site, string parent, bool worldStays)
        {
            Emit(
                "ParentChange",
                uid,
                site,
                "parent", parent ?? string.Empty,
                "worldStays", worldStays ? "1" : "0");
        }

        /// <summary>厘米级量化（×100 取整再 /100 字符串）。</summary>
        public static string FormatXy(float v)
        {
            var q = Mathf.RoundToInt(v * 100f);
            return (q / 100f).ToString("0.##", CultureInfo.InvariantCulture);
        }

        public static int QuantizeCm(float v)
        {
            return Mathf.RoundToInt(v * 100f);
        }

        private static void Emit(string kind, int uid, string site, params string[] pairs)
        {
            try
            {
                PerfTraceSink.Record?.Invoke(kind, uid, site ?? string.Empty, pairs);
            }
            catch
            {
                // swallow
            }
        }
    }
}
