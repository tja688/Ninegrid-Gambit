using NineGrid.Cards;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 局内动态描述 HUD 单例（已退役）：不再写入 Card Info Text / Notice Text。
    /// 卡面基础描述权威在 <c>Basic_Description</c> 槽 + 投影 Commit。
    /// 保留 MonoBehaviour 与场景绑定，避免序列化断裂；公开 API 均为 no-op。
    /// </summary>
    public sealed class DescriptionManagerSingleton : MonoBehaviour
    {
        public const int MaxDescriptionChars = 72;

        [SerializeField] private TMPro.TextMeshProUGUI cardInfoText;
        [SerializeField] private TMPro.TextMeshProUGUI noticeText;

        private int _generation;
        private int _noticeGeneration;

        public int Show(string defId, DescriptionShowRoute route = DescriptionShowRoute.Hover) =>
            ++_generation;

        public int Show(string defId) => Show(defId, DescriptionShowRoute.Hover);

        public int ShowText(string text, DescriptionShowRoute route = DescriptionShowRoute.BoardSelect) =>
            ++_generation;

        public int ShowOnNotice(string defId) => ++_noticeGeneration;

        public void ClearNotice(int generation)
        {
        }

        public void Clear()
        {
        }

        public void Clear(int generation)
        {
        }

        public void ClearRoute(DescriptionShowRoute route)
        {
        }
    }
}
