using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Cards.Slots
{
    /// <summary>
    /// 旁路纯数据 seam：按槽代号解析内容覆盖与模板兜底（无全局卡背源）。
    /// </summary>
    public static class CardFaceSlotResolver
    {
        /// <summary>
        /// 图标/贴图槽：内容有自定则用内容；否则回退该卡面模板默认 Sprite；皆无则 null。
        /// </summary>
        public static Sprite ResolveIcon(
            string slotCode,
            IReadOnlyDictionary<string, Sprite> contentOverrides,
            IReadOnlyDictionary<string, Sprite> templateDefaults)
        {
            if (string.IsNullOrEmpty(slotCode))
            {
                return null;
            }

            Sprite content;
            if (contentOverrides != null &&
                contentOverrides.TryGetValue(slotCode, out content) &&
                content != null)
            {
                return content;
            }

            Sprite template;
            if (templateDefaults != null &&
                templateDefaults.TryGetValue(slotCode, out template) &&
                template != null)
            {
                return template;
            }

            return null;
        }

        /// <summary>
        /// 数值槽缺省统一为 0（无内容覆盖时）。
        /// </summary>
        public static int ResolveNumeric(
            string slotCode,
            IReadOnlyDictionary<string, int> contentOverrides)
        {
            if (string.IsNullOrEmpty(slotCode) || contentOverrides == null)
            {
                return 0;
            }

            int value;
            return contentOverrides.TryGetValue(slotCode, out value) ? value : 0;
        }

        /// <summary>
        /// 从内容视觉直暴露字段构建覆盖表（无全局卡背；缺字段不写入）。
        /// </summary>
        public static Dictionary<string, Sprite> BuildContentOverrides(
            Sprite mainIcon,
            Sprite faceBackground,
            Sprite backBorder,
            Sprite backShirt,
            Sprite backLogo)
        {
            var map = new Dictionary<string, Sprite>();
            PutIfNotNull(map, CardFaceSlotCodes.MainIcon, mainIcon);
            PutIfNotNull(map, CardFaceSlotCodes.FaceBackground, faceBackground);
            PutIfNotNull(map, CardFaceSlotCodes.BackBorder, backBorder);
            PutIfNotNull(map, CardFaceSlotCodes.BackShirt, backShirt);
            PutIfNotNull(map, CardFaceSlotCodes.BackLogo, backLogo);
            return map;
        }

        private static void PutIfNotNull(IDictionary<string, Sprite> map, string code, Sprite sprite)
        {
            if (sprite != null)
            {
                map[code] = sprite;
            }
        }
    }
}
