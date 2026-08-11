using NineGrid.Cards.Slots;
using NineGrid.Content.CardPresentation;
using UnityEngine;

namespace NineGrid.Flow.InRoomBoard
{
    /// <summary>
    /// 就地选项卡面（商店「道具牌格升级」/ 卡店三项服务 / 刷新）按 defId 应用 JSON 图标与文案。
    /// 此前商店/卡店特色选项直接 Instantiate 房间选项标准模板，只换主图标、不写名字/描述；
    /// 真卡货架（SpawnPresentationOnly + ApplyVisualsByDefId）不受影响。
    /// </summary>
    internal static class RoomOptionFaceVisuals
    {
        public static void ApplyMainIcon(string defId, GameObject go)
        {
            ApplyFromDefId(defId, go);
        }

        public static void ApplyFromDefId(string defId, GameObject go)
        {
            if (go == null || string.IsNullOrEmpty(defId))
            {
                return;
            }

            if (!CardPresentationConfigCatalog.TryGet(defId, out var dto) || dto == null)
            {
                return;
            }

            ApplyMainIconFromDto(dto, go.transform);
            ApplyNameAndDescription(dto, defId, go.transform);
        }

        private static void ApplyMainIconFromDto(CardPresentationConfigDto dto, Transform root)
        {
            if (dto?.sprites == null || string.IsNullOrWhiteSpace(dto.sprites.mainIcon))
            {
                return;
            }

            var sprite = CardPresentationSpritePath.LoadSprite(dto.sprites.mainIcon);
            if (sprite == null)
            {
                return;
            }

            if (CardFaceSlotNodeMap.TryFindRenderer(root, CardFaceSlotCodes.MainIcon, out var renderer))
            {
                renderer.sprite = sprite;
                renderer.enabled = true;
            }
        }

        private static void ApplyNameAndDescription(
            CardPresentationConfigDto dto,
            string defId,
            Transform root)
        {
            var displayName = !string.IsNullOrWhiteSpace(dto.displayName) ? dto.displayName : defId;
            if (CardFaceSlotNodeMap.TryFindText(root, CardFaceSlotCodes.Name, out var nameText))
            {
                nameText.text = displayName ?? string.Empty;
            }

            if (!CardFaceSlotNodeMap.TryFindText(root, CardFaceSlotCodes.BasicDescription, out var descText))
            {
                return;
            }

            descText.text = dto.description ?? string.Empty;
            descText.spriteAsset = null;
        }
    }
}
