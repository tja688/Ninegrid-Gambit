using System;
using NineGrid.Cards;
using NineGrid.Cards.Vfx;
using NineGrid.Core;
using NineGrid.Flow;
using UnityEngine;

namespace NineGrid.Cards.Vfx
{
    /// <summary>
    /// 遗物影响场地目标时弹出徽章的装配与调用门面。
    /// 仅对场地中的怪物/机关生效；玩家本体受影响时不弹出（对齐策划需求）。
    /// </summary>
    public static class RelicImpactBadgeHook
    {
        /// <summary>
        /// 尝试从 SourceDefId 与 Cause 中提取有效的遗物主键。
        /// </summary>
        public static bool TryExtractRelicDefId(string sourceDefId, string cause, out string relicDefId)
        {
            relicDefId = string.Empty;

            if (!string.IsNullOrEmpty(sourceDefId)
                && sourceDefId.StartsWith("relic.", StringComparison.OrdinalIgnoreCase))
            {
                relicDefId = RelicImpactBadgeManager.ExtractCanonicalRelicDefId(sourceDefId);
                return !string.IsNullOrEmpty(relicDefId);
            }

            if (!string.IsNullOrEmpty(cause)
                && cause.StartsWith("relic.", StringComparison.OrdinalIgnoreCase))
            {
                relicDefId = RelicImpactBadgeManager.ExtractCanonicalRelicDefId(cause);
                return !string.IsNullOrEmpty(relicDefId);
            }

            return false;
        }

        /// <summary>
        /// 当遗物对指定卡牌（怪物/机关）造成伤害或破坏时请求弹出遗物图标。
        /// </summary>
        public static void RequestSpawnForTarget(int targetUid, string sourceDefId, string cause = null)
        {
            if (targetUid <= 0)
            {
                return;
            }

            if (!TryExtractRelicDefId(sourceDefId, cause, out var relicDefId))
            {
                return;
            }

            // 过滤玩家本体：仅对怪物与机关生效
            if (IsAvatarTarget(targetUid))
            {
                return;
            }

            var manager = RelicImpactBadgeManager.EnsureRunner();
            if (manager == null)
            {
                return;
            }

            if (TryResolveTargetSlot(targetUid, out var slotIndex))
            {
                manager.SpawnOrRefreshForSlot(slotIndex, relicDefId);
                return;
            }

            var pos = PresentationOutputProjector.ResolveCardWorldPosition(targetUid);
            if (pos.HasValue)
            {
                manager.SpawnOrRefreshAtPosition(pos.Value, relicDefId);
            }
        }

        /// <summary>
        /// 在指定场地格位（1~9）请求弹出遗物图标。
        /// </summary>
        public static void RequestSpawnAtSlot(int slotIndex, string sourceDefId, string cause = null)
        {
            if (slotIndex < GroundSlotTopology.MinSlot || slotIndex > GroundSlotTopology.MaxSlot)
            {
                return;
            }

            if (!TryExtractRelicDefId(sourceDefId, cause, out var relicDefId))
            {
                return;
            }

            var manager = RelicImpactBadgeManager.EnsureRunner();
            manager?.SpawnOrRefreshForSlot(slotIndex, relicDefId);
        }

        /// <summary>
        /// 在指定世界坐标请求弹出遗物图标。
        /// </summary>
        public static void RequestSpawnAtPosition(Vector3 position, string sourceDefId, string cause = null)
        {
            if (!TryExtractRelicDefId(sourceDefId, cause, out var relicDefId))
            {
                return;
            }

            var manager = RelicImpactBadgeManager.EnsureRunner();
            manager?.SpawnOrRefreshAtPosition(position, relicDefId);
        }

        public static void ClearAll()
        {
            RelicImpactBadgeManager.Instance?.ClearAll();
        }

        private static bool IsAvatarTarget(int targetUid)
        {
            var cards = CardEntityLifecycleHook.CardsOrNull();
            if (cards != null && cards.TryGet(targetUid, out var managedCard) && managedCard != null)
            {
                if (managedCard.CoreKind == CardPresentationKind.Avatar)
                {
                    return true;
                }
            }

            var arch = NineGridArchitecture.Current;
            if (arch != null)
            {
                var registry = arch.GetModel<CardRegistry>();
                if (registry != null && registry.TryGet(targetUid, out var coreCard) && coreCard != null)
                {
                    if (coreCard.Kind == CardKind.Avatar)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryResolveTargetSlot(int targetUid, out int slotIndex)
        {
            slotIndex = 0;

            var arch = NineGridArchitecture.Current;
            if (arch != null)
            {
                var registry = arch.GetModel<CardRegistry>();
                if (registry != null && registry.TryGet(targetUid, out var coreCard) && coreCard != null)
                {
                    if (coreCard.Slot.Value.IsBoardSlot)
                    {
                        slotIndex = coreCard.Slot.Value.Index;
                        return true;
                    }
                }
            }

            var cards = CardEntityLifecycleHook.CardsOrNull();
            var field = GroundFieldGeometryHook.FieldOrNull();
            if (cards != null && field != null && cards.TryGet(targetUid, out var managedCard) && managedCard != null)
            {
                if (field.TryGetSlotOf(targetUid, out slotIndex)
                    && slotIndex >= GroundSlotTopology.MinSlot
                    && slotIndex <= GroundSlotTopology.MaxSlot)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
