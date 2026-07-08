using UnityEngine;

namespace NineGrid.Content
{
    [CreateAssetMenu(
        fileName = "RelicVisualCatalog",
        menuName = "NineGrid/Content/Relic Visual Catalog")]
    public sealed class RelicVisualCatalogSO : ContentVisualSpriteCatalogSO
    {
        public override ContentVisualKind Kind
        {
            get { return ContentVisualKind.Relic; }
        }
    }
}
