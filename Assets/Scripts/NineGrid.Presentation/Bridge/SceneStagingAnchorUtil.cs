using System;
using System.Collections.Generic;
using NineGrid.Presentation.Shared;
using UnityEngine;

namespace NineGrid.Presentation.Bridge
{
    /// <summary>
    /// MainScene 舞台锚点查询（牌堆槽位、手牌槽位等）。
    /// </summary>
    public static class SceneStagingAnchorUtil
    {
        public const string DeckEntryPreparationSlotName = "DeckEntryPreparationSlot";
        public const string DeckDealOriginSlotName = "slot1";

        public static Transform[] CollectHandCardSlotAnchors(Transform handCardAnchors, int maxCount = 5)
        {
            if (handCardAnchors == null || maxCount <= 0)
            {
                return Array.Empty<Transform>();
            }

            var slots = new List<Transform>(maxCount);
            for (var i = 0; i < handCardAnchors.childCount && slots.Count < maxCount; i++)
            {
                Transform child = handCardAnchors.GetChild(i);
                if (!IsHandCardSlotAnchor(child.name))
                {
                    continue;
                }

                slots.Add(child);
            }

            return slots.ToArray();
        }

        public static HandCardLayoutSolver CreateHandLayoutSolver(Transform handCardAnchors, Transform layoutRoot)
        {
            var solver = new HandCardLayoutSolver();
            Transform[] refs = CollectHandCardSlotAnchors(handCardAnchors);
            if (refs.Length > 0)
            {
                solver.SetReferenceAnchors(refs, layoutRoot);
            }

            return solver;
        }

        public static bool IsHandCardSlotAnchor(string anchorName)
        {
            if (string.IsNullOrEmpty(anchorName))
            {
                return false;
            }

            return anchorName.StartsWith("handcard", StringComparison.OrdinalIgnoreCase);
        }

        public static Transform FindDeckChild(Transform cardDeckAnchors, string childName)
        {
            if (cardDeckAnchors == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            Transform direct = cardDeckAnchors.Find(childName);
            if (direct != null)
            {
                return direct;
            }

            string trimmed = childName.Trim();
            for (var i = 0; i < cardDeckAnchors.childCount; i++)
            {
                Transform child = cardDeckAnchors.GetChild(i);
                if (string.Equals(child.name.Trim(), trimmed, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }
    }
}
