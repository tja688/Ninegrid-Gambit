using UnityEngine;

namespace NineGrid.Content
{
    [CreateAssetMenu(
        fileName = "HelpCardVisualCatalog",
        menuName = "NineGrid/Content/Help Card Visual Catalog")]
    public sealed class HelpCardVisualCatalogSO : ContentVisualSpriteCatalogSO
    {
        public override ContentVisualKind Kind
        {
            get { return ContentVisualKind.HelpCard; }
        }
    }
}
