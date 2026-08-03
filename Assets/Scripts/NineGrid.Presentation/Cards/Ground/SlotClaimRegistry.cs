using System;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 格位 → 认领者登记（几何注册的命中语义扩写，ADR-0023）。
    /// 一格一认领；冲突断言失败并保留先到者，不静默覆盖。
    /// </summary>
    public sealed class SlotClaimRegistry
    {
        private readonly SlotClaimant[] _bySlot =
            new SlotClaimant[GroundSlotTopology.MaxSlot + 1];

        public void Clear()
        {
            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                _bySlot[slot] = null;
            }
        }

        public bool TryGet(int slot, out SlotClaimant claimant)
        {
            if (!GroundSlotTopology.IsValidSlot(slot))
            {
                claimant = null;
                return false;
            }

            claimant = _bySlot[slot];
            return claimant != null;
        }

        /// <summary>
        /// 登记认领。同 owner 刷新同一格允许；他 owner 占用则失败并 Error，保留先到者。
        /// </summary>
        public bool TryClaim(int slot, SlotClaimant claimant)
        {
            if (claimant == null)
            {
                return false;
            }

            if (!GroundSlotTopology.IsValidSlot(slot))
            {
                Debug.LogError($"[SlotClaimRegistry] 非法格位 slot={slot}，拒绝认领 owner={Describe(claimant.Owner)}。");
                return false;
            }

            var existing = _bySlot[slot];
            if (existing != null && !ReferenceEquals(existing.Owner, claimant.Owner))
            {
                Debug.LogError(
                    $"[SlotClaimRegistry] 认领冲突：slot={slot} 已有 owner={Describe(existing.Owner)}，"
                    + $"拒绝 owner={Describe(claimant.Owner)}。保留先到者，禁止静默覆盖。");
                Debug.Assert(false, $"[SlotClaimRegistry] 一格一认领冲突 slot={slot}");
                return false;
            }

            // 同 owner 迁格：先卸旧格。
            for (var s = GroundSlotTopology.MinSlot; s <= GroundSlotTopology.MaxSlot; s++)
            {
                if (s == slot)
                {
                    continue;
                }

                var other = _bySlot[s];
                if (other != null && ReferenceEquals(other.Owner, claimant.Owner))
                {
                    _bySlot[s] = null;
                }
            }

            _bySlot[slot] = claimant;
            return true;
        }

        /// <summary>仅当 owner 匹配时注销。</summary>
        public bool Release(int slot, object owner)
        {
            if (!GroundSlotTopology.IsValidSlot(slot) || owner == null)
            {
                return false;
            }

            var existing = _bySlot[slot];
            if (existing == null || !ReferenceEquals(existing.Owner, owner))
            {
                return false;
            }

            _bySlot[slot] = null;
            return true;
        }

        /// <summary>注销该 owner 占用的所有格（起飞 / 销毁）。</summary>
        public void ReleaseAllForOwner(object owner)
        {
            if (owner == null)
            {
                return;
            }

            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                var existing = _bySlot[slot];
                if (existing != null && ReferenceEquals(existing.Owner, owner))
                {
                    _bySlot[slot] = null;
                }
            }
        }

        private static string Describe(object owner)
        {
            if (owner == null)
            {
                return "null";
            }

            if (owner is UnityEngine.Object uo)
            {
                return uo != null ? uo.name : "destroyed";
            }

            return owner.GetType().Name;
        }
    }
}
