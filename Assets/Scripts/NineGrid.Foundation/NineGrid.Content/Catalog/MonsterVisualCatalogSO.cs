using UnityEngine;

namespace NineGrid.Content
{
    [CreateAssetMenu(
        fileName = "MonsterVisualCatalog",
        menuName = "NineGrid/Content/Monster Visual Catalog")]
    public sealed class MonsterVisualCatalogSO : ContentVisualSpriteCatalogSO
    {
        public override ContentVisualKind Kind
        {
            get { return ContentVisualKind.Monster; }
        }
    }
}
