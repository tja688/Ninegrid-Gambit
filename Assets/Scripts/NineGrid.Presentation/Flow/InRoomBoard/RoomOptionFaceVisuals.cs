using NineGrid.Cards.Slots;
using NineGrid.Content.CardPresentation;
using UnityEngine;

namespace NineGrid.Flow.InRoomBoard
{
    /// <summary>
    /// 就地选项卡面（商店「道具牌格升级」/ 卡店三项服务 / 刷新）按 defId 应用 JSON 主图标。
    /// 此前商店/卡店特色选项直接 Instantiate 房间选项标准模板，不消费 sprites.mainIcon，
    /// 恒显模板默认图标；真卡货架（SpawnPresentationOnly + ApplyVisualsByDefId）不受影响。
    /// </summary>
    internal static class RoomOptionFaceVisuals
    {
        public static void ApplyMainIcon(string defId, GameObject go)
        {
            if (go == null || string.IsNullOrEmpty(defId))
            {
                return;
            }

            if (!CardPresentationConfigCatalog.TryGet(defId, out var dto)
                || dto?.sprites == null
                || string.IsNullOrWhiteSpace(dto.sprites.mainIcon))
            {
                return;
            }

            var sprite = CardPresentationSpritePath.LoadSprite(dto.sprites.mainIcon);
            if (sprite == null)
            {
                return;
            }

            if (CardFaceSlotNodeMap.TryFindRenderer(go.transform, CardFaceSlotCodes.MainIcon, out var renderer))
            {
                renderer.sprite = sprite;
                renderer.enabled = true;
            }
        }
    }
}
