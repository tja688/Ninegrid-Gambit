using NineGrid.Core;
using QFramework;

namespace NineGrid.Presentation.Interaction
{
    /// <summary>
    /// 校验棋盘卡是否满足道具点选目标条件。
    /// </summary>
    public static class ItemUseTargetValidator
    {
        public static bool IsValidBoardTarget(IArchitecture architecture, int cardUid, ItemUseRequirement requirement)
        {
            if (architecture == null || cardUid <= 0 || requirement == null)
            {
                return false;
            }

            if (requirement.Kind != ItemUseRequirementKind.BoardTarget || requirement.TargetCount <= 0)
            {
                return false;
            }

            CoreViewSnapshot snapshot = CoreViewSnapshotFactory.Capture(architecture);
            if (!snapshot.TryGetCard(cardUid, out CardView card))
            {
                return false;
            }

            if (card.Zone != ZoneId.Board || card.Kind == CardKind.Avatar)
            {
                return false;
            }

            if (requirement.TargetKind != CardKind.Unknown && card.Kind != requirement.TargetKind)
            {
                return false;
            }

            if (!requirement.ExcludeElite && !requirement.ExcludeBoss)
            {
                return true;
            }

            var registry = architecture.GetModel<CardRegistry>();
            if (!registry.TryGet(cardUid, out CardInstance instance))
            {
                return false;
            }

            if (requirement.ExcludeElite && instance.Counters.Get(CoreCounterKeys.Elite) > 0)
            {
                return false;
            }

            return !requirement.ExcludeBoss || instance.Counters.Get(CoreCounterKeys.Boss) <= 0;
        }

        public static bool HasAnyValidTarget(IArchitecture architecture, ItemUseRequirement requirement)
        {
            if (architecture == null || requirement == null || requirement.Kind != ItemUseRequirementKind.BoardTarget)
            {
                return false;
            }

            CoreViewSnapshot snapshot = CoreViewSnapshotFactory.Capture(architecture);
            if (snapshot?.BoardSlots == null)
            {
                return false;
            }

            var found = 0;
            for (var i = 0; i < snapshot.BoardSlots.Count; i++)
            {
                BoardSlotView slot = snapshot.BoardSlots[i];
                if (slot.CardUid <= 0)
                {
                    continue;
                }

                if (!IsValidBoardTarget(architecture, slot.CardUid, requirement))
                {
                    continue;
                }

                found++;
                if (found >= requirement.TargetCount)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
