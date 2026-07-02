using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Presentation.Visuals;
using QFramework;

namespace NineGrid.Presentation.Shell
{
    public static class ShellChoiceLabelResolver
    {
        public static string ResolveRewardLabel(IArchitecture architecture, RewardEntry entry)
        {
            if (entry == null)
            {
                return "?";
            }

            if (TryResolveDisplayName(architecture, entry.DefId, out string displayName))
            {
                return displayName;
            }

            return entry.DefId;
        }

        public static string ResolveRoomLabel(IArchitecture architecture, RoomKind kind)
        {
            if (kind == RoomKind.None)
            {
                return "?";
            }

            if (architecture != null && ContentCatalogRuntimeBootstrap.EnsureLoaded(architecture))
            {
                var catalog = architecture.GetSystem<IContentSystem>().Catalog;
                if (catalog?.Rewards != null
                    && catalog.Rewards.TryGetRoom(kind, out RoomDefinition room)
                    && !string.IsNullOrEmpty(room.DisplayName))
                {
                    return room.DisplayName;
                }
            }

            return kind.ToString();
        }

        private static bool TryResolveDisplayName(IArchitecture architecture, string contentId, out string displayName)
        {
            displayName = null;
            if (architecture == null
                || string.IsNullOrEmpty(contentId)
                || !ContentCatalogRuntimeBootstrap.EnsureLoaded(architecture))
            {
                return false;
            }

            var coreCatalog = architecture.GetSystem<IContentSystem>().Catalog;
            var visualCatalog = ContentCatalogRuntimeBootstrap.VisualCatalog;
            var frameCatalog = ContentCatalogRuntimeBootstrap.FrameStyleCatalog;
            if (coreCatalog == null || visualCatalog == null)
            {
                return false;
            }

            if (!ContentVisualResolver.TryResolve(
                    contentId,
                    coreCatalog,
                    visualCatalog,
                    frameCatalog,
                    out ContentVisualResolvedView view)
                || string.IsNullOrEmpty(view.DisplayName))
            {
                return false;
            }

            displayName = view.DisplayName;
            return true;
        }
    }
}
