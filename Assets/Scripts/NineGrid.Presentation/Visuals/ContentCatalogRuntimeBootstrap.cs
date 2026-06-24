using NineGrid.Content;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using QFramework;

namespace NineGrid.Presentation.Visuals
{
    /// <summary>
    /// 运行时注入 Luban 内核表与表现视觉表，供 Presenter / ContentSystem 共用。
    /// </summary>
    public static class ContentCatalogRuntimeBootstrap
    {
        private static ContentVisualCatalog sVisualCatalog;
        private static VisualAssetCatalog sVisualAssetCatalog;
        private static CardFrameStyleCatalog sFrameStyleCatalog;
        private static bool sCoreLoaded;

        public static ContentVisualCatalog VisualCatalog => sVisualCatalog;
        public static VisualAssetCatalog VisualAssetCatalog => sVisualAssetCatalog;
        public static CardFrameStyleCatalog FrameStyleCatalog => sFrameStyleCatalog;

        public static bool EnsureLoaded(IArchitecture architecture)
        {
            if (architecture == null)
            {
                return false;
            }

            if (!sCoreLoaded)
            {
                var configUtility = architecture.GetUtility<IConfigUtility>();
                var contentSystem = architecture.GetSystem<IContentSystem>();

                GameContentCatalog coreCatalog;
                if (!configUtility.TryGet(ContentConfigKeys.DefaultCatalog, out coreCatalog)
                    || coreCatalog == null)
                {
                    coreCatalog = ContentCatalogBootstrap.Load();
                    configUtility.Set(ContentConfigKeys.DefaultCatalog, coreCatalog);
                }

                contentSystem.Load(coreCatalog);
                sCoreLoaded = true;
            }

            if (sVisualCatalog == null || sVisualAssetCatalog == null || sFrameStyleCatalog == null)
            {
                var directory = ContentVisualBootstrap.ResolveLubanDataDirectory();
                ContentVisualCatalog visualCatalog;
                VisualAssetCatalog visualAssetCatalog;
                CardFrameStyleCatalog frameStyleCatalog;
                if (ContentVisualBootstrap.TryLoadAll(directory, out visualCatalog, out visualAssetCatalog, out frameStyleCatalog))
                {
                    sVisualCatalog = visualCatalog;
                    sVisualAssetCatalog = visualAssetCatalog;
                    sFrameStyleCatalog = frameStyleCatalog;
                    ContentVisualSpriteLoader.Configure(visualAssetCatalog);
                }
            }

            return sVisualCatalog != null
                && sVisualAssetCatalog != null
                && sFrameStyleCatalog != null
                && architecture.GetSystem<IContentSystem>().HasCatalog;
        }

        public static void ResetForTests()
        {
            sVisualCatalog = null;
            sVisualAssetCatalog = null;
            sFrameStyleCatalog = null;
            sCoreLoaded = false;
            ContentVisualSpriteLoader.Configure(null);
        }
    }
}
