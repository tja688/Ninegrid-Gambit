using System.Text;
using NineGrid.Cards;
using NineGrid.Core;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// 场地「肉眼可见满场」审计：按槽位统计可见 Ground 卡，与 Opening 基线对比得出 visualMissing。
    /// </summary>
    public static class DiagFieldVisualCapture
    {
        public struct FieldVisualReport
        {
            public int BaselineVisible;
            public int VisibleCount;
            public int OccupiedCount;
            public int CoreCount;
            public int MissingVisible;
            public int EmptyVisualSlotCount;
            public string EmptyVisualSlots;
            public string HiddenOccupied;
            public string OffModeOccupied;
            public string SlotDetail;
        }

        public static FieldVisualReport Capture(int baselineVisible = -1)
        {
            var report = new FieldVisualReport
            {
                BaselineVisible = baselineVisible,
            };

            try
            {
                CountCoreOccupants(out report.CoreCount);

                var field = GroundFieldManagerSingleton.TryGetInstance();
                var cards = CardManagerSingleton.TryGetInstance();
                if (field == null)
                {
                    return report;
                }

                var snap = field.GetSnapshot();
                var emptySb = new StringBuilder(16);
                var hiddenSb = new StringBuilder(32);
                var offModeSb = new StringBuilder(32);
                var detailSb = new StringBuilder(128);

                for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
                {
                    if (GroundSlotTopology.IsAvatarReserved(slot))
                    {
                        continue;
                    }

                    var occUid = 0;
                    if (snap?.Slots != null)
                    {
                        var idx = slot - 1;
                        if (idx >= 0 && idx < snap.Slots.Length)
                        {
                            var occ = snap.Slots[idx];
                            if (!occ.IsEmpty && !occ.IsAvatarReserved)
                            {
                                occUid = occ.Uid;
                                report.OccupiedCount++;
                            }
                        }
                    }

                    if (!TryFindVisibleGroundCardAtSlot(cards, field, slot, out var visibleUid, out var offMode))
                    {
                        report.EmptyVisualSlotCount++;
                        if (emptySb.Length > 0)
                        {
                            emptySb.Append(',');
                        }

                        emptySb.Append(slot);

                        if (occUid > 0)
                        {
                            if (hiddenSb.Length > 0)
                            {
                                hiddenSb.Append(';');
                            }

                            hiddenSb.Append(occUid).Append('@').Append(slot);
                        }
                    }
                    else
                    {
                        report.VisibleCount++;
                        if (offMode && occUid > 0)
                        {
                            if (offModeSb.Length > 0)
                            {
                                offModeSb.Append(';');
                            }

                            offModeSb.Append(occUid).Append('@').Append(slot);
                        }
                    }

                    if (detailSb.Length > 0)
                    {
                        detailSb.Append('|');
                    }

                    detailSb.Append(slot)
                        .Append(":O").Append(occUid)
                        .Append("/V").Append(visibleUid);
                }

                report.EmptyVisualSlots = emptySb.ToString();
                report.HiddenOccupied = hiddenSb.ToString();
                report.OffModeOccupied = offModeSb.ToString();
                report.SlotDetail = detailSb.ToString();

                if (baselineVisible > 0)
                {
                    report.MissingVisible = Mathf.Max(0, baselineVisible - report.VisibleCount);
                }
            }
            catch
            {
                // swallow
            }

            return report;
        }

        private static bool TryFindVisibleGroundCardAtSlot(
            CardManagerSingleton cards,
            GroundFieldManagerSingleton field,
            int slot,
            out int visibleUid,
            out bool offModeMismatch)
        {
            visibleUid = 0;
            offModeMismatch = false;
            if (cards == null || field == null)
            {
                return false;
            }

            foreach (var card in cards.EnumerateCards())
            {
                if (card == null || card.IsFieldDead)
                {
                    continue;
                }

                if (!field.TryGetSlotOf(card.Uid, out var cardSlot) || cardSlot != slot)
                {
                    continue;
                }

                if (card.DisplayMode != CardDisplayMode.GroundCardMode)
                {
                    offModeMismatch = true;
                    continue;
                }

                var go = card.GameObject;
                if (go == null || !go.activeInHierarchy)
                {
                    continue;
                }

                if (!CardPresentationProbe.HasEnabledRenderer(go))
                {
                    continue;
                }

                visibleUid = card.Uid;
                return true;
            }

            return false;
        }

        private static void CountCoreOccupants(out int coreCount)
        {
            coreCount = 0;
            try
            {
                var board = NineGridArchitecture.Current?.GetModel<BoardModel>();
                if (board == null)
                {
                    return;
                }

                for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
                {
                    if (GroundSlotTopology.IsAvatarReserved(slot))
                    {
                        continue;
                    }

                    if (board.GetCardUid(SlotId.Board(slot)) > 0)
                    {
                        coreCount++;
                    }
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}
