using UnityEngine;

namespace NineGrid.Content
{
    [CreateAssetMenu(
        fileName = "MiscVisualCatalog",
        menuName = "NineGrid/Content/Misc Visual Catalog")]
    public sealed class MiscVisualCatalogSO : ContentVisualSpriteCatalogSO
    {
        public override ContentVisualKind Kind
        {
            get { return ContentVisualKind.Avatar; }
        }
    }
}
