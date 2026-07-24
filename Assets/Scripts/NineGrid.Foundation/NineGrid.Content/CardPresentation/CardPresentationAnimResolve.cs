using System;

namespace NineGrid.Content.CardPresentation
{
    /// <summary>
    /// 纯 DTO 动画槽解析：requested → usable frames → idle → static mainIcon。
    /// </summary>
    public static class CardPresentationAnimResolve
    {
        public enum Kind
        {
            Frames,
            StaticMainIcon,
        }

        public static Kind Resolve(
            CardPresentationConfigDto config,
            string requestedSlot,
            out string resolvedSlotId,
            out CardPresentationAnimSlotDto slotDto)
        {
            resolvedSlotId = CardAnimSlotIds.Normalize(requestedSlot);
            slotDto = FindUsableSlot(config, resolvedSlotId);
            if (slotDto != null)
            {
                return Kind.Frames;
            }

            if (!string.Equals(resolvedSlotId, CardAnimSlotIds.Idle, StringComparison.Ordinal))
            {
                resolvedSlotId = CardAnimSlotIds.Idle;
                slotDto = FindUsableSlot(config, CardAnimSlotIds.Idle);
                if (slotDto != null)
                {
                    return Kind.Frames;
                }
            }

            resolvedSlotId = CardAnimSlotIds.Idle;
            slotDto = null;
            return Kind.StaticMainIcon;
        }

        public static bool IsUsableSlot(CardPresentationAnimSlotDto slot)
        {
            if (slot == null)
            {
                return false;
            }

            var sourceType = NormalizeSourceType(slot.sourceType);
            if (sourceType == "none" || string.IsNullOrEmpty(sourceType))
            {
                return false;
            }

            if (sourceType != "folder" && sourceType != "atlas")
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(slot.path);
        }

        public static string NormalizeSourceType(string sourceType)
        {
            if (string.IsNullOrWhiteSpace(sourceType))
            {
                return "none";
            }

            return sourceType.Trim().ToLowerInvariant();
        }

        private static CardPresentationAnimSlotDto FindUsableSlot(
            CardPresentationConfigDto config,
            string slotId)
        {
            if (config == null
                || config.animations == null
                || config.animations.slots == null
                || string.IsNullOrEmpty(slotId))
            {
                return null;
            }

            var slots = config.animations.slots;
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                if (!string.Equals(
                        CardAnimSlotIds.Normalize(slot.id),
                        slotId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (IsUsableSlot(slot))
                {
                    return slot;
                }
            }

            return null;
        }
    }
}
