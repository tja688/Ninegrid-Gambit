using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Visuals
{
    /// <summary>
    /// 将运行时对象解析为 content_id，经 <see cref="ContentVisualResolver"/> 展示描述文案。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ContentInfoPresenter : MonoBehaviour
    {
        [SerializeField] private TableNineInfoTextView infoTextView;

        public void ShowForCardUid(IArchitecture architecture, int cardUid)
        {
            if (cardUid <= 0)
            {
                Clear();
                return;
            }

            if (architecture == null || !ContentCatalogRuntimeBootstrap.EnsureLoaded(architecture))
            {
                Clear();
                return;
            }

            var registry = architecture.GetModel<CardRegistry>();
            CardInstance card;
            if (!registry.TryGet(cardUid, out card) || string.IsNullOrEmpty(card.DefId))
            {
                Clear();
                return;
            }

            ShowForContentId(architecture, card.DefId);
        }

        public void ShowForContentId(IArchitecture architecture, string contentId)
        {
            if (string.IsNullOrEmpty(contentId))
            {
                Clear();
                return;
            }

            if (architecture == null || !ContentCatalogRuntimeBootstrap.EnsureLoaded(architecture))
            {
                Clear();
                return;
            }

            var coreCatalog = architecture.GetSystem<IContentSystem>().Catalog;
            var visualCatalog = ContentCatalogRuntimeBootstrap.VisualCatalog;
            var frameStyleCatalog = ContentCatalogRuntimeBootstrap.FrameStyleCatalog;
            if (coreCatalog == null || visualCatalog == null || frameStyleCatalog == null)
            {
                Clear();
                return;
            }

            ContentVisualResolvedView view;
            if (!ContentVisualResolver.TryResolve(
                    contentId,
                    coreCatalog,
                    visualCatalog,
                    frameStyleCatalog,
                    out view)
                || string.IsNullOrEmpty(view.Description))
            {
                infoTextView?.Clear();
                return;
            }

            infoTextView?.SetDescription(view.Description);
        }

        public void ShowForRewardEntry(IArchitecture architecture, RewardEntry entry)
        {
            if (entry == null)
            {
                Clear();
                return;
            }

            ShowForContentId(architecture, entry.DefId);
        }

        public void ShowForRoomKind(IArchitecture architecture, RoomKind roomKind)
        {
            if (roomKind == RoomKind.None)
            {
                Clear();
                return;
            }

            ShowForContentId(architecture, roomKind.ToString());
        }

        public void Clear()
        {
            infoTextView?.Clear();
        }

        private void Reset()
        {
            if (infoTextView == null)
            {
                infoTextView = GetComponent<TableNineInfoTextView>();
            }
        }
    }
}
