using UnityEngine;

namespace NineGrid.Content
{
    /// <summary>
    /// 运行时 Sprite Catalog 引导：放在 Resources 下，引用各 VisualCatalog SO，确保 Player 构建能加载卡面 icon/face。
    /// </summary>
    [CreateAssetMenu(
        fileName = "SpriteCatalogBootstrap",
        menuName = "NineGrid/Content/Sprite Catalog Bootstrap")]
    public sealed class ContentVisualSpriteCatalogBootstrapSO : ScriptableObject
    {
        public const string ResourcePath = "ContentVisual/SpriteCatalogBootstrap";

        [SerializeField]
        [Tooltip("各类型图标/卡面 Catalog 集合；须引用 Arts/ContentVisual 下对应 SO。")]
        private ContentVisualSpriteCatalogSet catalogs = new();

        public ContentVisualSpriteCatalogSet Catalogs => catalogs;

        public static ContentVisualSpriteCatalogSet TryLoadCatalogSet()
        {
            var bootstrap = Resources.Load<ContentVisualSpriteCatalogBootstrapSO>(ResourcePath);
            return bootstrap != null ? bootstrap.catalogs : null;
        }
    }
}
