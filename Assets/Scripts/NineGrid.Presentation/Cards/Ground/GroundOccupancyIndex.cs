using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地几何占格表（slot↔uid）与命中认领（slot→认领者）的同一登记体。
    /// 逻辑占格权威仍在 Core BoardModel；认领仅服务命中/悬停（ADR-0023）。
    /// 冲突显式失败并置标志；禁止 force-sync 自愈。
    /// </summary>
    public sealed class GroundOccupancyIndex
    {
        private readonly int[] _uidBySlot = new int[GroundSlotTopology.MaxSlot + 1];
        private readonly Dictionary<int, int> _slotByUid = new();
        private readonly SlotClaimRegistry _claims = new();
        private bool _occupancyConflictSinceClear;

        public bool HasOccupancyConflictSinceClear => _occupancyConflictSinceClear;

        /// <summary>格位认领（几何注册的命中语义扩写，ADR-0023）。</summary>
        public SlotClaimRegistry Claims => _claims;

        public event Action OccupancyMaybeClear;

        public void ClearConflictFlag()
        {
            _occupancyConflictSinceClear = false;
        }

        public bool ConsumeConflictFlag()
        {
            var had = _occupancyConflictSinceClear;
            _occupancyConflictSinceClear = false;
            return had;
        }

        public void Clear()
        {
            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                _uidBySlot[slot] = 0;
            }

            _slotByUid.Clear();
            _claims.Clear();
        }

        public int GetUidAt(int slot)
        {
            return IsValidSlot(slot) ? _uidBySlot[slot] : 0;
        }

        public bool TryGetSlotOf(int uid, out int slot)
        {
            return _slotByUid.TryGetValue(uid, out slot);
        }

        public bool ContainsUid(int uid)
        {
            return _slotByUid.ContainsKey(uid);
        }

        public bool IsEmpty(int slot)
        {
            return IsValidSlot(slot) && _uidBySlot[slot] == 0;
        }

        public bool IsPlaceable(int slot)
        {
            return IsValidSlot(slot)
                   && !GroundSlotTopology.IsAvatarReserved(slot)
                   && _uidBySlot[slot] == 0;
        }

        /// <summary>
        /// 查找 uid 占用的场地格（含双向表短暂不一致时的扫描；不 force-sync 写回）。
        /// </summary>
        public bool TryFindOccupiedSlotForUid(int uid, out int slot)
        {
            slot = 0;
            if (uid <= 0)
            {
                return false;
            }

            if (TryGetSlotOf(uid, out slot))
            {
                return true;
            }

            for (var s = GroundSlotTopology.MinSlot; s <= GroundSlotTopology.MaxSlot; s++)
            {
                if (_uidBySlot[s] != uid)
                {
                    continue;
                }

                slot = s;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 登记占格。目标格已有其他 uid 时失败并告警，禁止静默挤占。
        /// 同 uid 迁格：先清旧格再写新格。
        /// </summary>
        public bool TryRegister(int slot, int uid, bool logConflict = true)
        {
            if (!IsValidSlot(slot) || uid <= 0)
            {
                return false;
            }

            var previousUid = _uidBySlot[slot];
            if (previousUid != 0 && previousUid != uid)
            {
                _occupancyConflictSinceClear = true;
                if (logConflict)
                {
                    Debug.LogError(
                        $"[GroundOccupancyIndex] 禁止静默挤占：slot={slot} 已有 uid={previousUid}，拒绝登记 uid={uid}。"
                        + " 调用方须先 Vacate/Clear 旧占格。");
                }

                try
                {
                    FlowFieldTraceSink.OccupancyConflict?.Invoke(
                        slot,
                        previousUid,
                        uid,
                        logConflict ? "TryRegister" : "TryRegister.Silent");
                }
                catch
                {
                    // ignore
                }

                return false;
            }

            if (_slotByUid.TryGetValue(uid, out var previousSlot) && previousSlot != slot)
            {
                _uidBySlot[previousSlot] = 0;
            }

            _uidBySlot[slot] = uid;
            _slotByUid[uid] = slot;
            return true;
        }

        public void Unregister(int slot, string caller = null)
        {
            if (!IsValidSlot(slot))
            {
                return;
            }

            var uid = _uidBySlot[slot];
            if (uid != 0)
            {
                CardPresentationProbe.Vacate(uid, slot, "Ground.Vacate", caller);
                try
                {
                    FlowFieldTraceSink.OccupancyVacate?.Invoke(slot, uid, caller ?? "Unregister");
                }
                catch
                {
                    // ignore
                }

                _slotByUid.Remove(uid);
            }

            _uidBySlot[slot] = 0;
            OccupancyMaybeClear?.Invoke();
        }

        /// <summary>外圈旋转：成批清空再登记前，直接清双向表项（不触发 MaybeClear）。</summary>
        public void VacateSlotSilent(int slot)
        {
            if (!IsValidSlot(slot))
            {
                return;
            }

            var uid = _uidBySlot[slot];
            if (uid != 0)
            {
                _slotByUid.Remove(uid);
            }

            _uidBySlot[slot] = 0;
        }

        /// <summary>换位：成对卸格（不触发 MaybeClear）。</summary>
        public void VacateUidSilent(int uid)
        {
            if (!_slotByUid.TryGetValue(uid, out var slot))
            {
                return;
            }

            _uidBySlot[slot] = 0;
            _slotByUid.Remove(uid);
        }

        /// <summary>
        /// 批量置换占格：验证通过后一次性更新双向表；失败则原快照不变并置冲突标志。
        /// moves: (uid, toSlot)。参与移动的 uid 视为同一事务；目标格仅允许空或被本批 uid 占用。
        /// </summary>
        public bool TryCommitPermutation(IReadOnlyList<(int uid, int toSlot)> moves, string caller = null)
        {
            if (moves == null || moves.Count == 0)
            {
                return true;
            }

            var batchUids = new HashSet<int>(moves.Count);
            var targetSlots = new HashSet<int>(moves.Count);
            var fromSlotByUid = new Dictionary<int, int>(moves.Count);

            for (var i = 0; i < moves.Count; i++)
            {
                var (uid, toSlot) = moves[i];
                if (uid <= 0)
                {
                    return FailPermutation(
                        $"uid={uid} 无效",
                        0,
                        0,
                        uid,
                        caller);
                }

                if (!IsValidSlot(toSlot))
                {
                    return FailPermutation(
                        $"toSlot={toSlot} 无效",
                        toSlot,
                        0,
                        uid,
                        caller);
                }

                if (!batchUids.Add(uid))
                {
                    return FailPermutation(
                        $"批次内 uid={uid} 重复",
                        0,
                        uid,
                        uid,
                        caller);
                }

                if (!targetSlots.Add(toSlot))
                {
                    return FailPermutation(
                        $"批次内 toSlot={toSlot} 重复",
                        toSlot,
                        0,
                        uid,
                        caller);
                }

                if (!TryFindOccupiedSlotForUid(uid, out var fromSlot))
                {
                    return FailPermutation(
                        $"uid={uid} 不在场地",
                        0,
                        0,
                        uid,
                        caller);
                }

                fromSlotByUid[uid] = fromSlot;
            }

            for (var i = 0; i < moves.Count; i++)
            {
                var (uid, toSlot) = moves[i];
                var occupant = GetUidAt(toSlot);
                if (occupant != 0 && occupant != uid && !batchUids.Contains(occupant))
                {
                    return FailPermutation(
                        $"slot={toSlot} 被外部 uid={occupant} 占用，拒绝 uid={uid} 迁入",
                        toSlot,
                        occupant,
                        uid,
                        caller);
                }
            }

            foreach (var uid in batchUids)
            {
                var fromSlot = fromSlotByUid[uid];
                if (_uidBySlot[fromSlot] == uid)
                {
                    _uidBySlot[fromSlot] = 0;
                }

                _slotByUid.Remove(uid);
            }

            for (var i = 0; i < moves.Count; i++)
            {
                var (uid, toSlot) = moves[i];
                _uidBySlot[toSlot] = uid;
                _slotByUid[uid] = toSlot;
            }

            OccupancyMaybeClear?.Invoke();
            return true;
        }

        public GroundFieldSnapshot BuildSnapshot()
        {
            var slots = new GroundFieldSlotSnapshot[GroundSlotTopology.MaxSlot];
            var occupied = 0;
            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                var uid = _uidBySlot[slot];
                var isEmpty = uid == 0;
                if (!isEmpty)
                {
                    occupied++;
                }

                slots[slot - 1] = new GroundFieldSlotSnapshot(
                    slot,
                    uid,
                    isEmpty,
                    GroundSlotTopology.IsAvatarReserved(slot));
            }

            return new GroundFieldSnapshot(slots, occupied);
        }

        public IReadOnlyList<int> CollectUids(Predicate<int> slotFilter = null)
        {
            var result = new List<int>();
            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                if (slotFilter != null && !slotFilter(slot))
                {
                    continue;
                }

                var uid = _uidBySlot[slot];
                if (uid != 0)
                {
                    result.Add(uid);
                }
            }

            return result;
        }

        public IReadOnlyList<int> GetEmptyPlaceableSlots()
        {
            var result = new List<int>(8);
            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                if (IsPlaceable(slot))
                {
                    result.Add(slot);
                }
            }

            return result;
        }

        public bool HasFullOpeningRing()
        {
            for (var i = 0; i < GroundSlotTopology.ClockwiseRing.Count; i++)
            {
                if (_uidBySlot[GroundSlotTopology.ClockwiseRing[i]] == 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidSlot(int slot)
        {
            return GroundSlotTopology.IsValidSlot(slot);
        }

        private bool FailPermutation(string detail, int slot, int existingUid, int incomingUid, string caller)
        {
            _occupancyConflictSinceClear = true;
            Debug.LogError($"[GroundOccupancyIndex] 批量置换失败：{detail}");
            try
            {
                FlowFieldTraceSink.OccupancyConflict?.Invoke(
                    slot,
                    existingUid,
                    incomingUid,
                    caller ?? "TryCommitPermutation");
            }
            catch
            {
                // ignore
            }

            return false;
        }
    }
}
