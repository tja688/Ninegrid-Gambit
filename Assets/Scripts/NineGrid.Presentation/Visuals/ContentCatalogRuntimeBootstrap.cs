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
        private static bool sCoreLoaded;

        public static ContentVisualCatalog VisualCatalog => sVisualCatalog;

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

            if (sVisualCatalog == null)
            {
                ContentVisualCatalog visualCatalog;
                if (ContentVisualBootstrap.TryLoad(ContentVisualBootstrap.ResolveLubanDataDirectory(), out visualCatalog))
                {
                    sVisualCatalog = visualCatalog;
                }
            }

            return sVisualCatalog != null && architecture.GetSystem<IContentSystem>().HasCatalog;
        }

        public static void ResetForTests()
        {
            sVisualCatalog = null;
            sCoreLoaded = false;
        }
    }
}
