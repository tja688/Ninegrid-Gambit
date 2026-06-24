using System.Collections.Generic;
using NineGrid.Core;
using QFramework;

namespace NineGrid.Presentation.FSM
{
    internal static class ItemApplyZoneTargetRules
    {
        public static bool IsValidTarget(
            IArchitecture architecture,
            ItemUseProfile profile,
            int targetUid,
            IReadOnlyList<int> alreadySelected)
        {
            if (architecture == null || targetUid <= 0)
            {
                return false;
            }

            if (alreadySelected != null)
            {
                for (var i = 0; i < alreadySelected.Count; i++)
                {
                    if (alreadySelected[i] == targetUid)
                    {
                        return false;
                    }
                }
            }

            var registry = architecture.GetModel<CardRegistry>();
            if (!registry.TryGet(targetUid, out CardInstance card) || card.Kind == CardKind.Avatar)
            {
                return false;
            }

            if (profile.AllowedKind != CardKind.Unknown && card.Kind != profile.AllowedKind)
            {
                return false;
            }

            if (profile.ExcludeElite && card.Counters.Get(CoreCounterKeys.Elite) > 0)
            {
                return false;
            }

            if (profile.ExcludeBoss && card.Counters.Get(CoreCounterKeys.Boss) > 0)
            {
                return false;
            }

            var board = architecture.GetModel<BoardModel>();
            return BoardInteractionRules.FindSlotForCardUid(board, targetUid).IsBoardSlot;
        }
    }
}
